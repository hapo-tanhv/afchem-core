using MySql.Data.MySqlClient;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HinoTools.Data.Http
{
    public class BatchesHttpServer
    {
        private HttpListener listener;
        private string connectionString;
        private bool isRunning;
        private readonly object dbLock = new object();
        private int serverPort;
        private string securityToken;
        private string reportTableName;

        public bool IsRunning => isRunning;

        public BatchesHttpServer(string connectionString, int port = 5500, string token = null, string reportTable = "alarmreport")
        {
            this.connectionString = connectionString;
            this.serverPort = port;
            this.securityToken = token;
            this.reportTableName = string.IsNullOrEmpty(reportTable) ? "alarmreport" : reportTable;
            listener = new HttpListener();
            // Listen on port from any source IP
            listener.Prefixes.Add($"http://*:{port}/");
        }

        public void Start()
        {
            if (isRunning) return;

            // Đợt 3B: KHÔNG mở cổng khi chưa có token.
            // Server này phục vụ cả đường GHI (POST /api/batches/create), nên "không có token"
            // phải nghĩa là TỪ CHỐI CHẠY, chứ không phải "chạy mà không cần xác thực".
            if (string.IsNullOrEmpty(securityToken))
            {
                string msg = $"[BatchesHttpServer] TỪ CHỐI KHỞI ĐỘNG trên cổng {serverPort}: chưa cấu hình token " +
                             $"(khoá Api.ReadApiToken trong scada-runtime.config). Không mở cổng không xác thực.";
                System.Diagnostics.Debug.WriteLine(msg);
                LogSystemError(msg);
                return;
            }

            isRunning = true;
            try
            {
                listener.Start();
                Task.Run(() => ListenLoop());
                System.Diagnostics.Debug.WriteLine($"[BatchesHttpServer] Started successfully on port {serverPort}.");
            }
            catch (HttpListenerException ex)
            {
                if (ex.ErrorCode == 5) // Access Denied
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[BatchesHttpServer] Access denied for wildcard prefix. Falling back to localhost/127.0.0.1...");
                        
                        // Recreate the listener because the previous one is in a faulted/disposed state
                        try { listener.Close(); } catch { }
                        
                        listener = new HttpListener();
                        listener.Prefixes.Add($"http://localhost:{serverPort}/");
                        listener.Prefixes.Add($"http://127.0.0.1:{serverPort}/");
                        listener.Start();
                        
                        Task.Run(() => ListenLoop());
                        System.Diagnostics.Debug.WriteLine($"[BatchesHttpServer] Started successfully on localhost/127.0.0.1 port {serverPort}.");
                    }
                    catch (Exception fallbackEx)
                    {
                        isRunning = false;
                        System.Diagnostics.Debug.WriteLine($"[BatchesHttpServer] ERROR starting fallback server: {fallbackEx.Message}");
                    }
                }
                else
                {
                    isRunning = false;
                    System.Diagnostics.Debug.WriteLine($"[BatchesHttpServer] ERROR starting server: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                isRunning = false;
                System.Diagnostics.Debug.WriteLine($"[BatchesHttpServer] ERROR starting server: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            try
            {
                listener.Stop();
                System.Diagnostics.Debug.WriteLine("[BatchesHttpServer] Stopped successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BatchesHttpServer] ERROR stopping server: {ex.Message}");
            }
        }

        private async Task ListenLoop()
        {
            while (isRunning)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    // Process each request asynchronously to avoid blocking the listener thread
                    _ = Task.Run(() => ProcessRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Server stopped
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BatchesHttpServer] Error in listen loop: {ex.Message}");
                    await Task.Delay(100);
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Enable CORS by default
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Api-Token");

            // Handle pre-flight OPTIONS request
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
                return;
            }

            // Đợt 3B: xác thực token cho MỌI endpoint (X-Api-Token header hoặc ?token=).
            // Server không khởi động khi thiếu token nên tới đây securityToken luôn khác rỗng.
            string requestToken = request.Headers["X-Api-Token"];
            if (string.IsNullOrEmpty(requestToken))
            {
                requestToken = request.QueryString["token"];
            }

            if (string.IsNullOrEmpty(requestToken) || !FixedTimeEquals(requestToken, securityToken))
            {
                string errorJson = "{\n  \"success\": false,\n  \"message\": \"Unauthorized. Thiếu hoặc sai API token.\"\n}";
                SendJsonResponse(response, HttpStatusCode.Unauthorized, errorJson);
                return;
            }

            try
            {
                if (request.HttpMethod == "POST" && request.Url.AbsolutePath.Equals("/api/batches/create", StringComparison.OrdinalIgnoreCase))
                {
                    string body = "";
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        body = reader.ReadToEnd();
                    }

                    // Parse device_name from JSON
                    // Đợt 3B: KHÔNG còn mặc định "TX01". Không nêu rõ máy thì từ chối,
                    // vì đoán máy nghĩa là gán sai dữ liệu sản xuất cho một máy có thật.
                    string deviceName = "";
                    var match = Regex.Match(body, "\"device_name\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        deviceName = match.Groups[1].Value.Trim();
                    }
                    else
                    {
                        // Fallback: check query parameter or URL form-urlencoded
                        var queryDevice = request.QueryString["device_name"];
                        if (!string.IsNullOrEmpty(queryDevice))
                        {
                            deviceName = queryDevice.Trim();
                        }
                    }

                    if (string.IsNullOrEmpty(deviceName))
                    {
                        string errorJson = "{\n  \"success\": false,\n  \"message\": \"Thiếu tham số device_name. " +
                                           "Phải nêu rõ máy sản xuất, hệ không tự đoán máy.\"\n}";
                        SendJsonResponse(response, HttpStatusCode.BadRequest, errorJson);
                        return;
                    }

                    // Máy phải có trong bảng machines (nạp từ danh mục máy của Core).
                    string machineError;
                    if (!Helper.MachineRegistry.IsKnownDevice(connectionString, deviceName, out machineError))
                    {
                        string errorJson = "{\n  \"success\": false,\n  \"message\": \"" +
                                           EscapeJsonString(machineError) + "\"\n}";
                        SendJsonResponse(response, HttpStatusCode.BadRequest, errorJson);
                        return;
                    }

                    // Parse quantity (default is 1 for creation via this new workflow)
                    int quantity = 1;
                    var quantityMatch = Regex.Match(body, "\"quantity\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase);
                    if (quantityMatch.Success)
                    {
                        if (int.TryParse(quantityMatch.Groups[1].Value, out int q))
                        {
                            quantity = q;
                        }
                    }
                    else
                    {
                        var queryQuantity = request.QueryString["quantity"];
                        if (!string.IsNullOrEmpty(queryQuantity) && int.TryParse(queryQuantity, out int q))
                        {
                            quantity = q;
                        }
                    }

                    if (quantity < 1)
                    {
                        quantity = 1;
                    }

                    // Parse runs_count or runs (default is 1)
                    int runsCount = 1;
                    var runsCountMatch = Regex.Match(body, "\"(runs_count|runs)\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase);
                    if (runsCountMatch.Success)
                    {
                        if (int.TryParse(runsCountMatch.Groups[2].Value, out int rc))
                        {
                            runsCount = rc;
                        }
                    }
                    else
                    {
                        var queryRunsCount = request.QueryString["runs_count"];
                        if (string.IsNullOrEmpty(queryRunsCount))
                        {
                            queryRunsCount = request.QueryString["runs"];
                        }
                        if (!string.IsNullOrEmpty(queryRunsCount) && int.TryParse(queryRunsCount, out int rc))
                        {
                            runsCount = rc;
                        }
                    }

                    if (runsCount < 1)
                    {
                        runsCount = 1;
                    }

                    var createdBatches = new System.Collections.Generic.List<string>();

                    lock (dbLock)
                    {
                        // 1. Generate unique batch name: device_name-yyyyMMdd-stt
                        string todayStr = DateTime.Now.ToString("yyyyMMdd");
                        int nextStt = 1;

                        using (var conn = new MySqlConnection(connectionString))
                        {
                            conn.Open();

                            // Ensure database and batches table exist first
                            EnsureBatchesTableExists(conn);

                            // Find the last batch created today for this device to determine next sequence
                            string selectQuery = "SELECT `name` FROM `batches` " +
                                                "WHERE `device_name` = @device_name AND DATE(`created_at`) = CURDATE() " +
                                                "ORDER BY `id` DESC LIMIT 1";

                            using (var cmd = new MySqlCommand(selectQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@device_name", deviceName);
                                var obj = cmd.ExecuteScalar();
                                if (obj != null && obj != DBNull.Value)
                                {
                                    string lastBatchName = obj.ToString();
                                    // Extract the stt suffix (e.g. TX01-20260520-01 -> 01)
                                    var parts = lastBatchName.Split('-');
                                    if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 1], out int lastStt))
                                    {
                                        nextStt = lastStt + 1;
                                    }
                                }
                            }

                            // Query max execution_order for this device
                            int maxOrder = 0;
                            string selectMaxOrderQuery = "SELECT IFNULL(MAX(r.execution_order), 0) FROM `runs` r " +
                                                         "JOIN `batches` b ON r.batch_id = b.id " +
                                                         "WHERE b.device_name = @device_name";
                            using (var cmdMax = new MySqlCommand(selectMaxOrderQuery, conn))
                            {
                                cmdMax.Parameters.AddWithValue("@device_name", deviceName);
                                var maxObj = cmdMax.ExecuteScalar();
                                if (maxObj != null && maxObj != DBNull.Value)
                                {
                                    maxOrder = Convert.ToInt32(maxObj);
                                }
                            }

                            // 2. Insert quantity number of batches sequentially
                            for (int i = 0; i < quantity; i++)
                            {
                                int currentStt = nextStt + i;
                                string batchName = $"{deviceName}-{todayStr}-{currentStt:D2}";

                                string insertQuery = "INSERT INTO `batches` (`name`, `device_name`, `status`, `total_runs`, `created_at`) " +
                                                    "VALUES (@name, @device_name, 'Pending', @total_runs, NOW())";
                                using (var cmd = new MySqlCommand(insertQuery, conn))
                                {
                                    cmd.Parameters.AddWithValue("@name", batchName);
                                    cmd.Parameters.AddWithValue("@device_name", deviceName);
                                    cmd.Parameters.AddWithValue("@total_runs", runsCount);
                                    cmd.ExecuteNonQuery();
                                    int insertedId = (int)cmd.LastInsertedId;

                                    // Insert the runs inside this batch
                                    var createdRuns = new System.Collections.Generic.List<string>();
                                    for (int r = 1; r <= runsCount; r++)
                                    {
                                        int currentExecutionOrder = maxOrder + r;
                                        string runName = $"{batchName}-Me{r:D2}";
                                        string insertRunQuery = "INSERT INTO `runs` (`batch_id`, `run_number`, `name`, `status`, `execution_order`, `created_at`) " +
                                                                "VALUES (@batch_id, @run_number, @name, 'Pending', @execution_order, NOW())";
                                        using (var runCmd = new MySqlCommand(insertRunQuery, conn))
                                        {
                                            runCmd.Parameters.AddWithValue("@batch_id", insertedId);
                                            runCmd.Parameters.AddWithValue("@run_number", r);
                                            runCmd.Parameters.AddWithValue("@name", runName);
                                            runCmd.Parameters.AddWithValue("@execution_order", currentExecutionOrder);
                                            runCmd.ExecuteNonQuery();
                                            int runInsertedId = (int)runCmd.LastInsertedId;

                                            string runJson = $"{{\n" +
                                                             $"        \"id\": {runInsertedId},\n" +
                                                             $"        \"run_number\": {r},\n" +
                                                             $"        \"name\": \"{runName}\",\n" +
                                                             $"        \"status\": \"Pending\",\n" +
                                                             $"        \"execution_order\": {currentExecutionOrder}\n" +
                                                             $"      }}";
                                            createdRuns.Add(runJson);
                                        }
                                    }

                                    maxOrder += runsCount;

                                    string runsArrayJson = string.Join(",\n", createdRuns);
                                    string itemJson = $"{{\n" +
                                                       $"      \"id\": {insertedId},\n" +
                                                       $"      \"name\": \"{batchName}\",\n" +
                                                       $"      \"device_name\": \"{deviceName}\",\n" +
                                                       $"      \"status\": \"Pending\",\n" +
                                                       $"      \"total_runs\": {runsCount},\n" +
                                                       $"      \"runs\": [\n{runsArrayJson}\n      ]\n" +
                                                       $"    }}";
                                    createdBatches.Add(itemJson);
                                }
                            }
                        }
                    }

                    // 3. Send Success JSON Response with list of created batches
                    string dataArrayJson = string.Join(",\n", createdBatches);
                    string jsonResponse = $"{{\n" +
                                          $"  \"success\": true,\n" +
                                          $"  \"message\": \"{quantity} batch(es) created successfully with {runsCount} run(s) each\",\n" +
                                          $"  \"data\": [\n{dataArrayJson}\n  ]\n" +
                                          $"}}";

                    SendJsonResponse(response, HttpStatusCode.OK, jsonResponse);
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath.TrimEnd('/').Equals("/api/batches/cancel", StringComparison.OrdinalIgnoreCase))
                {
                    HandleCancelBatch(request, response);
                }
                else if (request.HttpMethod == "GET" && Regex.IsMatch(request.Url.AbsolutePath, "^/api/batches/by-lot/.+$", RegexOptions.IgnoreCase))
                {
                    // Đợt 3B — hợp đồng đọc cho MES: tra lô theo MÃ HIỂN THỊ.
                    // MES dùng mã lô chính thức (custom_lotno / BAT-), KHÔNG dùng id auto-increment
                    // của SCADA (id là chi tiết nội bộ, không phải định danh nghiệp vụ).
                    string rawLot = request.Url.AbsolutePath.Substring("/api/batches/by-lot/".Length).TrimEnd('/');
                    string lotCode = WebUtility.UrlDecode(rawLot);

                    string batchJson = null;
                    lock (dbLock)
                    {
                        using (var conn = new MySqlConnection(connectionString))
                        {
                            conn.Open();
                            EnsureBatchesTableExists(conn);
                            batchJson = BuildBatchJsonByLotCode(conn, lotCode);
                        }
                    }

                    if (batchJson == null)
                    {
                        SendJsonResponse(response, HttpStatusCode.NotFound,
                            "{\n  \"success\": false,\n  \"message\": \"Không tìm thấy lô với mã lô chính thức '" +
                            EscapeJsonString(lotCode) + "'.\"\n}");
                    }
                    else
                    {
                        SendJsonResponse(response, HttpStatusCode.OK,
                            "{\n  \"success\": true,\n  \"data\":\n" + batchJson + "\n}");
                    }
                }
                else if (request.HttpMethod == "GET" && request.Url.AbsolutePath.Equals("/api/batches", StringComparison.OrdinalIgnoreCase))
                {
                    string deviceName = request.QueryString["device_name"];
                    string lotCodeFilter = request.QueryString["lot_code"];
                    string mcCodeFilter = request.QueryString["mc_code"];
                    int limit = 50;
                    var limitStr = request.QueryString["limit"];
                    if (!string.IsNullOrEmpty(limitStr))
                    {
                        int.TryParse(limitStr, out limit);
                    }
                    if (limit < 1) limit = 50;

                    var batchList = new System.Collections.Generic.List<string>();
                    lock (dbLock)
                    {
                        using (var conn = new MySqlConnection(connectionString))
                        {
                            conn.Open();
                            EnsureBatchesTableExists(conn);

                            // Đợt 3B: join sang machines để trả mã máy MC- của Core,
                            // và trả official_lot_code tách bạch khỏi mã phiên chạy máy.
                            string selectQuery =
                                "SELECT b.`id`, b.`name`, b.`official_lot_code`, b.`device_name`, m.`mc_code`, " +
                                "       b.`status`, b.`total_runs`, b.`start_time`, b.`end_time`, b.`created_at`, b.`product_lot_code` " +
                                "FROM `batches` b LEFT JOIN `machines` m ON m.`device_name` = b.`device_name` ";

                            var where = new System.Collections.Generic.List<string>();
                            if (!string.IsNullOrEmpty(deviceName)) where.Add("b.`device_name` = @device_name");
                            if (!string.IsNullOrEmpty(lotCodeFilter)) where.Add("b.`official_lot_code` = @lot_code");
                            if (!string.IsNullOrEmpty(mcCodeFilter)) where.Add("m.`mc_code` = @mc_code");
                            if (where.Count > 0) selectQuery += "WHERE " + string.Join(" AND ", where) + " ";

                            selectQuery += "ORDER BY b.`id` DESC LIMIT @limit";

                            using (var cmd = new MySqlCommand(selectQuery, conn))
                            {
                                if (!string.IsNullOrEmpty(deviceName)) cmd.Parameters.AddWithValue("@device_name", deviceName);
                                if (!string.IsNullOrEmpty(lotCodeFilter)) cmd.Parameters.AddWithValue("@lot_code", lotCodeFilter);
                                if (!string.IsNullOrEmpty(mcCodeFilter)) cmd.Parameters.AddWithValue("@mc_code", mcCodeFilter);
                                cmd.Parameters.AddWithValue("@limit", limit);

                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        batchList.Add(BuildBatchSummaryJson(reader, "      "));
                                    }
                                }
                            }
                        }
                    }
                    string dataArrayJson = string.Join(",\n", batchList);
                    string jsonResponse = $"{{\n  \"success\": true,\n  \"data\": [\n{dataArrayJson}\n  ]\n}}";
                    SendJsonResponse(response, HttpStatusCode.OK, jsonResponse);
                }
                else if (request.HttpMethod == "GET" && request.Url.AbsolutePath.Equals("/api/runs", StringComparison.OrdinalIgnoreCase))
                {
                    var batchIdStr = request.QueryString["batch_id"];
                    if (string.IsNullOrEmpty(batchIdStr) || !int.TryParse(batchIdStr, out int batchId))
                    {
                        string errorJson = "{\n  \"success\": false,\n  \"message\": \"Missing or invalid batch_id parameter\"\n}";
                        SendJsonResponse(response, HttpStatusCode.BadRequest, errorJson);
                        return;
                    }

                    var runList = new System.Collections.Generic.List<string>();
                    lock (dbLock)
                    {
                        using (var conn = new MySqlConnection(connectionString))
                        {
                            conn.Open();
                            EnsureBatchesTableExists(conn);

                            string selectQuery = "SELECT `id`, `batch_id`, `run_number`, `name`, `status`, `execution_order`, `start_time`, `end_time`, `created_at` FROM `runs` WHERE `batch_id` = @batch_id ORDER BY `run_number` ASC";
                            using (var cmd = new MySqlCommand(selectQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@batch_id", batchId);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        int id = reader.GetInt32(0);
                                        int bId = reader.GetInt32(1);
                                        int runNum = reader.GetInt32(2);
                                        string name = reader.GetString(3);
                                        string status = reader.GetString(4);
                                        int execOrder = reader.GetInt32(5);
                                        // Đợt 3B: thời gian đối ngoại luôn ISO 8601 kèm offset +07:00
                                        string startTime = reader.IsDBNull(6) ? "null" : $"\"{Helper.TimeFormat.ToIso8601(reader.GetDateTime(6))}\"";
                                        string endTime = reader.IsDBNull(7) ? "null" : $"\"{Helper.TimeFormat.ToIso8601(reader.GetDateTime(7))}\"";
                                        string createdAt = Helper.TimeFormat.ToIso8601(reader.GetDateTime(8));

                                        string item = $"{{\n" +
                                                      $"      \"id\": {id},\n" +
                                                      $"      \"batch_id\": {bId},\n" +
                                                      $"      \"run_number\": {runNum},\n" +
                                                      $"      \"name\": \"{name}\",\n" +
                                                      $"      \"status\": \"{status}\",\n" +
                                                      $"      \"execution_order\": {execOrder},\n" +
                                                      $"      \"start_time\": {startTime},\n" +
                                                      $"      \"end_time\": {endTime},\n" +
                                                      $"      \"created_at\": \"{createdAt}\"\n" +
                                                      $"    }}";
                                        runList.Add(item);
                                    }
                                }
                            }
                        }
                    }
                    string dataArrayJson = string.Join(",\n", runList);
                    string jsonResponse = $"{{\n  \"success\": true,\n  \"data\": [\n{dataArrayJson}\n  ]\n}}";
                    SendJsonResponse(response, HttpStatusCode.OK, jsonResponse);
                }
                else
                {
                    // Endpoint not found
                    string jsonResponse = "{\n  \"success\": false,\n  \"message\": \"Endpoint not found. Use POST /api/batches/create or GET /api/batches or GET /api/runs\"\n}";
                    SendJsonResponse(response, HttpStatusCode.NotFound, jsonResponse);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BatchesHttpServer] ERROR processing request: {ex.Message}");
                string jsonResponse = $"{{\n  \"success\": false,\n  \"message\": \"{EscapeJsonString(ex.Message)}\"\n}}";
                SendJsonResponse(response, HttpStatusCode.InternalServerError, jsonResponse);
            }
        }

        /// <summary>
        /// Huỷ mẻ chờ theo mã lô chính thức (MES huỷ mẻ sau khi đã đẩy sang SCADA).
        /// 200: đã huỷ hoặc trước đó đã huỷ rồi. 404: không có mã. 409: máy đã chạy mẻ này.
        /// </summary>
        private void HandleCancelBatch(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                body = reader.ReadToEnd();
            }

            string lotCode = "";
            try
            {
                var json = new System.Web.Script.Serialization.JavaScriptSerializer()
                    .DeserializeObject(body) as System.Collections.Generic.Dictionary<string, object>;
                if (json != null && json.ContainsKey("official_lot_code"))
                {
                    lotCode = Convert.ToString(json["official_lot_code"]).Trim();
                }
            }
            catch (ArgumentException)
            {
                // Body không phải JSON: rơi xuống nhánh thiếu mã bên dưới.
            }

            if (string.IsNullOrEmpty(lotCode))
            {
                SendJsonResponse(response, HttpStatusCode.BadRequest,
                    "{\n  \"success\": false,\n  \"message\": \"Thiếu official_lot_code trong body JSON.\"\n}");
                return;
            }

            int cancelledRuns = 0;
            HttpStatusCode result;
            lock (dbLock)
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    Db.SchemaMigrator.EnsureDot3BSchema(conn);
                    result = CancelByLotCode(conn, lotCode, out cancelledRuns);
                }
            }

            if (result == HttpStatusCode.NotFound)
            {
                SendJsonResponse(response, HttpStatusCode.NotFound,
                    "{\n  \"success\": false,\n  \"message\": \"Không tìm thấy lô với mã lô chính thức '" +
                    EscapeJsonString(lotCode) + "'.\"\n}");
            }
            else if (result == HttpStatusCode.Conflict)
            {
                SendJsonResponse(response, HttpStatusCode.Conflict,
                    "{\n  \"success\": false,\n  \"message\": \"Mã lô '" + EscapeJsonString(lotCode) +
                    "' đã có mẻ đang chạy hoặc đã xong trên máy, không huỷ được.\"\n}");
            }
            else
            {
                SendJsonResponse(response, HttpStatusCode.OK,
                    "{\n  \"success\": true,\n  \"cancelled_runs\": " + cancelledRuns + "\n}");
            }
        }

        private static HttpStatusCode CancelByLotCode(MySqlConnection conn, string lotCode, out int cancelledRuns)
        {
            cancelledRuns = 0;
            using (var tx = conn.BeginTransaction())
            {
                // Khoá dòng lô trước: webhook tạo run sau khi đã trả lời, lệnh INSERT của nó
                // phải chờ ở đây rồi thấy lô đã huỷ, không để run Pending lọt sau khi huỷ.
                bool found = false;
                using (var cmd = new MySqlCommand(
                    "SELECT `id` FROM `batches` WHERE `official_lot_code` = @lot FOR UPDATE", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@lot", lotCode);
                    using (var reader = cmd.ExecuteReader())
                    {
                        found = reader.Read();
                    }
                }
                if (!found)
                {
                    tx.Rollback();
                    return HttpStatusCode.NotFound;
                }

                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM `runs` r JOIN `batches` b ON b.`id` = r.`batch_id` " +
                    "WHERE b.`official_lot_code` = @lot AND r.`status` IN ('Active', 'Completed')", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@lot", lotCode);
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    {
                        tx.Rollback();
                        return HttpStatusCode.Conflict;
                    }
                }

                using (var cmd = new MySqlCommand(
                    "UPDATE `runs` r JOIN `batches` b ON b.`id` = r.`batch_id` " +
                    "SET r.`status` = 'Cancelled' " +
                    "WHERE b.`official_lot_code` = @lot AND r.`status` = 'Pending'", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@lot", lotCode);
                    cancelledRuns = cmd.ExecuteNonQuery();
                }

                // Giữ nguyên giờ huỷ của các lần trước: chỉ đụng lô chưa huỷ.
                using (var cmd = new MySqlCommand(
                    "UPDATE `batches` SET `status` = 'Cancelled', `end_time` = NOW() " +
                    "WHERE `official_lot_code` = @lot AND `status` <> 'Cancelled'", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@lot", lotCode);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                return HttpStatusCode.OK;
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
                System.Diagnostics.Debug.WriteLine($"[BatchesHttpServer] ERROR writing response stream: {ex.Message}");
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        private void EnsureBatchesTableExists(MySqlConnection conn)
        {
            EnsureBatchesTableExistsCore(conn);

            // Đợt 3B: bảng machines + cột official_lot_code phải có mặt kể cả khi
            // Read API khởi động TRƯỚC khi có webhook nào chạy (nếu không sẽ lỗi 500 với MES).
            Db.SchemaMigrator.EnsureDot3BSchema(conn);
        }

        private void EnsureBatchesTableExistsCore(MySqlConnection conn)
        {
            string createTableSql = "CREATE TABLE IF NOT EXISTS `batches` (" +
                                    "  `id` INT AUTO_INCREMENT PRIMARY KEY," +
                                    "  `name` VARCHAR(100) NOT NULL UNIQUE," +
                                    "  `device_name` VARCHAR(100) NOT NULL," +
                                    "  `status` VARCHAR(50) NOT NULL DEFAULT 'Pending'," +
                                    "  `start_time` DATETIME NULL," +
                                    "  `end_time` DATETIME NULL," +
                                    "  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP" +
                                    ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
            using (var cmd = new MySqlCommand(createTableSql, conn))
            {
                cmd.ExecuteNonQuery();
            }

            // Task 1.2: Check and add total_runs column to batches table
            try
            {
                string checkColQuery = "SHOW COLUMNS FROM `batches` LIKE 'total_runs'";
                using (var cmd = new MySqlCommand(checkColQuery, conn))
                {
                    var result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                    {
                        string alterQuery = "ALTER TABLE `batches` ADD COLUMN `total_runs` INT NOT NULL DEFAULT 1 AFTER `status`";
                        using (var alterCmd = new MySqlCommand(alterQuery, conn))
                        {
                            alterCmd.ExecuteNonQuery();
                        }
                        System.Diagnostics.Debug.WriteLine("[Migration] Added total_runs column to batches table successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] ERROR adding total_runs column to batches: {ex.Message}");
            }

            // Task 1.1 & 1.2: Create runs table
            string createRunsTableSql = "CREATE TABLE IF NOT EXISTS `runs` (" +
                                        "  `id` INT AUTO_INCREMENT PRIMARY KEY," +
                                        "  `batch_id` INT NOT NULL," +
                                        "  `run_number` INT NOT NULL," +
                                        "  `name` VARCHAR(150) NOT NULL UNIQUE," +
                                        "  `status` VARCHAR(50) NOT NULL DEFAULT 'Pending'," +
                                        "  `execution_order` INT NOT NULL DEFAULT 0," +
                                        "  `start_time` DATETIME NULL," +
                                        "  `end_time` DATETIME NULL," +
                                        "  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP," +
                                        "  FOREIGN KEY (`batch_id`) REFERENCES `batches`(`id`) ON DELETE CASCADE," +
                                        "  INDEX `idx_runs_batch` (`batch_id`)," +
                                        "  INDEX `idx_runs_status` (`status`)" +
                                        ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
            using (var cmd = new MySqlCommand(createRunsTableSql, conn))
            {
                cmd.ExecuteNonQuery();
            }

            // Check and add execution_order column to runs table if it doesn't exist
            try
            {
                string checkColQuery = "SHOW COLUMNS FROM `runs` LIKE 'execution_order'";
                using (var cmd = new MySqlCommand(checkColQuery, conn))
                {
                    var result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                    {
                        string alterQuery = "ALTER TABLE `runs` ADD COLUMN `execution_order` INT NOT NULL DEFAULT 0 AFTER `status`";
                        using (var alterCmd = new MySqlCommand(alterQuery, conn))
                        {
                            alterCmd.ExecuteNonQuery();
                        }
                        System.Diagnostics.Debug.WriteLine("[Migration] Added column execution_order to runs table successfully.");

                        // One-time backfill migration for historical runs
                        string backfillSql = "UPDATE `runs` SET `execution_order` = `id` WHERE `execution_order` = 0";
                        using (var backfillCmd = new MySqlCommand(backfillSql, conn))
                        {
                            int backfillRows = backfillCmd.ExecuteNonQuery();
                            System.Diagnostics.Debug.WriteLine($"[Migration] Backfilled {backfillRows} historical runs with execution_order.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] ERROR adding execution_order column to runs: {ex.Message}");
            }

            // Task 1.4: Historical data migration (One-time check and execution)
            try
            {
                // Create a run for each batch that doesn't have runs yet
                string migrateRunsSql = "INSERT INTO `runs` (`batch_id`, `run_number`, `name`, `status`, `start_time`, `end_time`, `created_at`) " +
                                        "SELECT b.id, 1, CONCAT(b.name, '-Me01'), b.status, b.start_time, b.end_time, b.created_at " +
                                        "FROM `batches` b " +
                                        "WHERE NOT EXISTS (SELECT 1 FROM `runs` r WHERE r.batch_id = b.id)";
                using (var cmd = new MySqlCommand(migrateRunsSql, conn))
                {
                    int rows = cmd.ExecuteNonQuery();
                    if (rows > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Migration] Migrated {rows} historical batches to the new runs structure.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] ERROR migrating historical batches to runs: {ex.Message}");
            }
        }

        private static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        /// <summary>Chuỗi JSON cho một giá trị text: "giá trị" hoặc null.</summary>
        private static string JsonText(object value)
        {
            if (value == null || value == DBNull.Value) return "null";
            return "\"" + EscapeJsonString(Convert.ToString(value)) + "\"";
        }

        /// <summary>
        /// Đợt 3B: một dòng tóm tắt lô cho hệ ngoài.
        ///
        /// Ba mã được trả TÁCH BẠCH để không ai nhầm nữa:
        ///   - official_lot_code    : MÃ LÔ nghiệp vụ (từ Base/MES) — đây là thứ MES dùng
        ///   - product_lot_code     : mã lô thành phẩm MES gửi kèm mẻ BAT-, NULL với lô cũ
        ///   - machine_session_code : mã phiên chạy máy kỹ thuật (batches.name) — KHÔNG phải mã lô
        ///   - mc_code              : mã máy của Core
        /// Trường "name" và "id" được giữ lại để bên gọi cũ không vỡ.
        /// </summary>
        private static string BuildBatchSummaryJson(MySqlDataReader reader, string indent)
        {
            return BuildBatchSummaryJson(reader, indent, false);
        }

        /// <summary>
        /// <paramref name="leaveOpen"/> = true: bỏ dấu ngoặc đóng và thêm dấu phẩy,
        /// để người gọi nối thêm thuộc tính (ví dụ mảng "runs").
        /// </summary>
        private static string BuildBatchSummaryJson(MySqlDataReader reader, string indent, bool leaveOpen)
        {
            int id = reader.GetInt32(0);
            string name = reader.IsDBNull(1) ? "" : reader.GetString(1);
            object officialLot = reader.IsDBNull(2) ? null : reader.GetValue(2);
            string devName = reader.IsDBNull(3) ? "" : reader.GetString(3);
            object mcCode = reader.IsDBNull(4) ? null : reader.GetValue(4);
            string status = reader.IsDBNull(5) ? "" : reader.GetString(5);
            int totalRuns = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
            string startTime = reader.IsDBNull(7) ? "null" : "\"" + Helper.TimeFormat.ToIso8601(reader.GetDateTime(7)) + "\"";
            string endTime = reader.IsDBNull(8) ? "null" : "\"" + Helper.TimeFormat.ToIso8601(reader.GetDateTime(8)) + "\"";
            string createdAt = reader.IsDBNull(9) ? "null" : "\"" + Helper.TimeFormat.ToIso8601(reader.GetDateTime(9)) + "\"";
            object productLot = reader.IsDBNull(10) ? null : reader.GetValue(10);

            return indent + "{\n" +
                   indent + "  \"official_lot_code\": " + JsonText(officialLot) + ",\n" +
                   indent + "  \"product_lot_code\": " + JsonText(productLot) + ",\n" +
                   indent + "  \"machine_session_code\": " + JsonText(name) + ",\n" +
                   indent + "  \"mc_code\": " + JsonText(mcCode) + ",\n" +
                   indent + "  \"device_name\": " + JsonText(devName) + ",\n" +
                   indent + "  \"status\": " + JsonText(status) + ",\n" +
                   indent + "  \"total_runs\": " + totalRuns + ",\n" +
                   indent + "  \"start_time\": " + startTime + ",\n" +
                   indent + "  \"end_time\": " + endTime + ",\n" +
                   indent + "  \"created_at\": " + createdAt + ",\n" +
                   indent + "  \"id\": " + id + ",\n" +
                   indent + "  \"name\": " + JsonText(name) + "\n" +
                   (leaveOpen ? indent + "  ," : indent + "}");
        }

        /// <summary>
        /// Chi tiết một lô tra theo mã lô chính thức: header + các mẻ + BOM từng mẻ.
        /// Trả null nếu không có lô nào mang mã đó. Mã đã đẩy lại sau khi huỷ có nhiều lô:
        /// ưu tiên lô chưa huỷ, rồi lô mới nhất.
        /// </summary>
        private string BuildBatchJsonByLotCode(MySqlConnection conn, string lotCode)
        {
            int batchId = -1;
            var sb = new StringBuilder();

            const string headerSql =
                "SELECT b.`id`, b.`name`, b.`official_lot_code`, b.`device_name`, m.`mc_code`, " +
                "       b.`status`, b.`total_runs`, b.`start_time`, b.`end_time`, b.`created_at`, b.`product_lot_code` " +
                "FROM `batches` b LEFT JOIN `machines` m ON m.`device_name` = b.`device_name` " +
                "WHERE b.`official_lot_code` = @lot " +
                "ORDER BY (b.`status` = 'Cancelled') ASC, b.`id` DESC LIMIT 1";

            using (var cmd = new MySqlCommand(headerSql, conn))
            {
                cmd.Parameters.AddWithValue("@lot", lotCode);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    batchId = reader.GetInt32(0);
                    sb.Append(BuildBatchSummaryJson(reader, "  ", true));
                }
            }

            sb.Append("\n  \"runs\": [\n");

            var runIds = new System.Collections.Generic.List<int>();
            var runJsons = new System.Collections.Generic.List<string>();

            using (var cmd = new MySqlCommand(
                "SELECT `id`, `name`, `run_number`, `status`, `start_time`, `end_time` " +
                "FROM `runs` WHERE `batch_id` = @id ORDER BY `run_number` ASC", conn))
            {
                cmd.Parameters.AddWithValue("@id", batchId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        runIds.Add(reader.GetInt32(0));
                        string runStart = reader.IsDBNull(4) ? "null" : "\"" + Helper.TimeFormat.ToIso8601(reader.GetDateTime(4)) + "\"";
                        string runEnd = reader.IsDBNull(5) ? "null" : "\"" + Helper.TimeFormat.ToIso8601(reader.GetDateTime(5)) + "\"";

                        runJsons.Add(
                            "    {\n" +
                            "      \"run_name\": " + JsonText(reader.IsDBNull(1) ? null : reader.GetValue(1)) + ",\n" +
                            "      \"run_number\": " + (reader.IsDBNull(2) ? 0 : reader.GetInt32(2)) + ",\n" +
                            "      \"status\": " + JsonText(reader.IsDBNull(3) ? null : reader.GetValue(3)) + ",\n" +
                            "      \"start_time\": " + runStart + ",\n" +
                            "      \"end_time\": " + runEnd + "\n" +
                            "    }");
                    }
                }
            }

            sb.Append(string.Join(",\n", runJsons));
            sb.Append("\n  ]\n}");
            return sb.ToString();
        }

        /// <summary>
        /// So sánh chuỗi với thời gian chạy không phụ thuộc vị trí ký tự khác nhau,
        /// tránh rò rỉ thông tin token qua đo thời gian phản hồi.
        /// </summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;

            int diff = a.Length ^ b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        private void LogSystemError(string errorMessage)
        {
            try
            {
                string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "batches_api_errors.log");
                string logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {errorMessage}{Environment.NewLine}";
                File.AppendAllText(logFilePath, logContent);
            }
            catch { }
        }
    }
}
