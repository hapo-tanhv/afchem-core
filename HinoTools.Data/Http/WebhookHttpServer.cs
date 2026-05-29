using MySql.Data.MySqlClient;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HinoTools.Data.Http
{
    public class WebhookHttpServer
    {
        private HttpListener listener;
        private string connectionString;
        private bool isRunning;
        private int serverPort;
        private string securityToken;
        private readonly object dbLock = new object();

        public bool IsRunning => isRunning;

        public WebhookHttpServer(string connectionString, int port = 5600, string token = "wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b")
        {
            this.connectionString = connectionString;
            this.serverPort = port;
            this.securityToken = token;
            this.listener = new HttpListener();
            
            // Lắng nghe trên port từ mọi IP nguồn gửi tới
            this.listener.Prefixes.Add($"http://*:{port}/");
        }

        public void Start()
        {
            if (isRunning) return;
            isRunning = true;
            try
            {
                listener.Start();
                Task.Run(() => ListenLoop());
                System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] Started successfully on port {serverPort}.");
            }
            catch (HttpListenerException ex)
            {
                if (ex.ErrorCode == 5) // Access Denied (Thường xảy ra khi không chạy quyền Admin với tiền tố wildcard *)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[WebhookHttpServer] Access denied for wildcard prefix. Falling back to safe localhost/127.0.0.1...");
                        
                        try { listener.Close(); } catch { }
                        
                        listener = new HttpListener();
                        listener.Prefixes.Add($"http://localhost:{serverPort}/");
                        listener.Prefixes.Add($"http://127.0.0.1:{serverPort}/");
                        listener.Start();
                        
                        Task.Run(() => ListenLoop());
                        System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] Started successfully on localhost/127.0.0.1 port {serverPort}.");
                    }
                    catch (Exception fallbackEx)
                    {
                        isRunning = false;
                        System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] ERROR starting fallback server: {fallbackEx.Message}");
                        LogSystemError($"Error starting fallback Webhook server: {fallbackEx.Message}");
                    }
                }
                else
                {
                    isRunning = false;
                    System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] ERROR starting server: {ex.Message}");
                    LogSystemError($"Error starting Webhook server: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                isRunning = false;
                System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] ERROR starting server: {ex.Message}");
                LogSystemError($"Error starting Webhook server: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            try
            {
                listener.Stop();
                System.Diagnostics.Debug.WriteLine("[WebhookHttpServer] Stopped successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] ERROR stopping server: {ex.Message}");
            }
        }

        private async Task ListenLoop()
        {
            while (isRunning)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    // Xử lý request bất đồng bộ trên ThreadPool để không block luồng lắng nghe chính
                    _ = Task.Run(() => ProcessRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Server đã dừng
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] Error in listen loop: {ex.Message}");
                    await Task.Delay(100);
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Kích hoạt CORS hỗ trợ gọi từ các ứng dụng web khác nếu cần
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Webhook-Token");

            // Xử lý tiền kiểm CORS OPTIONS
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
                return;
            }

            try
            {
                // Kiểm tra Endpoint và HTTP Method (Yêu cầu POST tại endpoint /api/webhook)
                if (request.Url.AbsolutePath.Equals("/api/webhook", StringComparison.OrdinalIgnoreCase) || 
                    request.Url.AbsolutePath.Equals("/api/webhook/", StringComparison.OrdinalIgnoreCase))
                {
                    if (request.HttpMethod != "POST")
                    {
                        string errorJson = "{\n  \"success\": false,\n  \"message\": \"Only HTTP POST is allowed\"\n}";
                        SendJsonResponse(response, HttpStatusCode.MethodNotAllowed, errorJson);
                        return;
                    }

                    // Kiểm tra Content-Type (nếu có truyền phải chứa application/json)
                    string contentType = request.ContentType;
                    if (!string.IsNullOrEmpty(contentType) && !contentType.ToLower().Contains("application/json"))
                    {
                        string errorJson = "{\n  \"success\": false,\n  \"message\": \"Content-Type must be application/json\"\n}";
                        SendJsonResponse(response, HttpStatusCode.UnsupportedMediaType, errorJson);
                        return;
                    }

                    // Xác thực Token bảo mật qua Header X-Webhook-Token hoặc Query Parameter (?token=...)
                    string requestToken = request.Headers["X-Webhook-Token"];
                    if (string.IsNullOrEmpty(requestToken))
                    {
                        requestToken = request.QueryString["token"];
                    }

                    if (string.IsNullOrEmpty(requestToken) || requestToken != securityToken)
                    {
                        string errorJson = "{\n  \"success\": false,\n  \"message\": \"Unauthorized. Invalid or missing webhook token.\"\n}";
                        SendJsonResponse(response, HttpStatusCode.Unauthorized, errorJson);
                        return;
                    }

                    // Đọc nội dung JSON payload với giới hạn kích thước tối đa 5MB để phòng ngừa tấn công DDoS
                    if (request.ContentLength64 > 5 * 1024 * 1024)
                    {
                        string errorJson = "{\n  \"success\": false,\n  \"message\": \"Payload size exceeds maximum limit of 5MB\"\n}";
                        SendJsonResponse(response, HttpStatusCode.RequestEntityTooLarge, errorJson);
                        return;
                    }

                    string rawPayloadJson = "";
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        rawPayloadJson = reader.ReadToEnd();
                    }

                    if (string.IsNullOrEmpty(rawPayloadJson))
                    {
                        string errorJson = "{\n  \"success\": false,\n  \"message\": \"Request body cannot be empty\"\n}";
                        SendJsonResponse(response, HttpStatusCode.BadRequest, errorJson);
                        return;
                    }

                    // Bàn giao việc lưu DB cho một background Task để phản hồi nhanh ngay lập tức cho hệ thống gửi
                    _ = Task.Run(() => SaveWebhookPayloadAsync(rawPayloadJson));

                    // Phản hồi 200 OK thành công ngay cho client
                    string successJson = "{\n  \"success\": true,\n  \"message\": \"Webhook payload received\"\n}";
                    SendJsonResponse(response, HttpStatusCode.OK, successJson);
                }
                else
                {
                    // Sai Endpoint
                    string notFoundJson = "{\n  \"success\": false,\n  \"message\": \"Endpoint not found. Use POST /api/webhook\"\n}";
                    SendJsonResponse(response, HttpStatusCode.NotFound, notFoundJson);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] ERROR processing request: {ex.Message}");
                string errorResponse = $"{{\n  \"success\": false,\n  \"message\": \"{EscapeJsonString(ex.Message)}\"\n}}";
                SendJsonResponse(response, HttpStatusCode.InternalServerError, errorResponse);
            }
        }

        private void SendJsonResponse(HttpListenerResponse response, HttpStatusCode statusCode, string jsonString)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(jsonString);
            response.StatusCode = (int)statusCode;
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;

            try
            {
                using (var output = response.OutputStream)
                {
                    output.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] ERROR writing response stream: {ex.Message}");
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        private void SaveWebhookPayloadAsync(string rawJson)
        {
            lock (dbLock)
            {
                try
                {
                    using (var conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();

                        // Đảm bảo bảng webhook_logs tồn tại trước khi ghi nhận
                        EnsureWebhookTableExists(conn);

                        string insertQuery = "INSERT INTO `webhook_logs` (`received_at`, `payload`, `status`) " +
                                             "VALUES (NOW(), @payload, 'Pending')";
                        
                        using (var cmd = new MySqlCommand(insertQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@payload", rawJson);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    System.Diagnostics.Debug.WriteLine("[WebhookHttpServer] Webhook payload successfully logged to database.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] ERROR saving webhook to database: {ex.Message}");
                    LogSystemError($"Error saving webhook payload to database. Exception: {ex.Message}. Payload: {rawJson}");
                }
            }
        }

        private void EnsureWebhookTableExists(MySqlConnection conn)
        {
            string createTableSql = "CREATE TABLE IF NOT EXISTS `webhook_logs` (" +
                                    "  `id` INT AUTO_INCREMENT PRIMARY KEY," +
                                    "  `received_at` DATETIME NOT NULL," +
                                    "  `payload` LONGTEXT NOT NULL," +
                                    "  `status` VARCHAR(50) NOT NULL DEFAULT 'Pending'," +
                                    "  `error_message` LONGTEXT NULL," +
                                    "  INDEX `idx_webhook_logs_status` (`status`)" +
                                    ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
            
            using (var cmd = new MySqlCommand(createTableSql, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void LogSystemError(string errorMessage)
        {
            try
            {
                // Ghi lỗi thô ra file log debug của hệ thống
                string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "webhook_errors.log");
                string logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {errorMessage}{Environment.NewLine}";
                File.AppendAllText(logFilePath, logContent);
            }
            catch { }
        }

        private static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}
