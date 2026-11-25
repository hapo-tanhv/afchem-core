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

        /// <summary>
        /// Đợt 3B: bắt buộc phiếu từ Base phải có mã lô chính thức (custom_lotno).
        ///
        /// Mặc định BẬT — đây chính là điều đợt này cần chấm dứt: lô không mã chính thức
        /// rồi mã máy tự sinh bị dùng làm số lô trên hồ sơ QA/QC.
        ///
        /// Chỉ tắt tạm trong giai đoạn chuyển tiếp nếu phát hiện Base còn gửi phiếu cũ
        /// thiếu trường này; khi tắt, lô vẫn được tạo nhưng official_lot_code để trống
        /// và hồ sơ QA/QC vẫn bị chặn xuất (xem BatchRecordExportService).
        /// </summary>
        public bool RequireOfficialLotCode { get; set; } = true;

        /// <summary>
        /// Đợt 3B: token KHÔNG còn giá trị mặc định trong mã nguồn (token cũ đã lộ trong repo).
        /// Token thật đặt ở scada-runtime.config, khoá Api.WebhookToken.
        /// </summary>
        public WebhookHttpServer(string connectionString, int port = 5605, string token = null)
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

            // Đợt 3B: không có token thì TỪ CHỐI mở cổng.
            // Cổng này tạo lô sản xuất, để nó chạy không xác thực là mở đường ghi dữ liệu tự do.
            if (string.IsNullOrEmpty(securityToken))
            {
                string msg = $"[WebhookHttpServer] TỪ CHỐI KHỞI ĐỘNG trên cổng {serverPort}: chưa cấu hình token " +
                             $"(khoá Api.WebhookToken trong scada-runtime.config). Không mở cổng không xác thực.";
                System.Diagnostics.Debug.WriteLine(msg);
                LogSystemError(msg);
                return;
            }

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

                    // Save webhook log synchronously to get its ID
                    int logId = 0;
                    try
                    {
                        logId = SaveWebhookPayloadSync(rawPayloadJson);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] ERROR logging webhook: {ex.Message}");
                        string errorResponse = $"{{\n  \"success\": false,\n  \"message\": \"Failed to write webhook log: {EscapeJsonString(ex.Message)}\"\n}}";
                        SendJsonResponse(response, HttpStatusCode.InternalServerError, errorResponse);
                        return;
                    }

                    // Parse payload
                    var paramsDict = ParseFormUrlEncoded(rawPayloadJson);

                    // Extract ngay_san_xuat (date)
                    string ngaySanXuatRaw = "";
                    if (paramsDict.ContainsKey("custom_ngay_san_xuat")) ngaySanXuatRaw = paramsDict["custom_ngay_san_xuat"];
                    else if (paramsDict.ContainsKey("ngay_san_xuat")) ngaySanXuatRaw = paramsDict["ngay_san_xuat"];

                    if (string.IsNullOrEmpty(ngaySanXuatRaw))
                    {
                        string errorResponse = "{\n  \"success\": false,\n  \"message\": \"Missing custom_ngay_san_xuat parameter\"\n}";
                        UpdateWebhookStatus(logId, "Failed", "Missing custom_ngay_san_xuat parameter");
                        SendJsonResponse(response, HttpStatusCode.BadRequest, errorResponse);
                        return;
                    }

                    DateTime prodDate;
                    try
                    {
                        prodDate = DateTime.ParseExact(ngaySanXuatRaw.Trim(), "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    catch (Exception ex)
                    {
                        string errorResponse = $"{{\n  \"success\": false,\n  \"message\": \"Invalid custom_ngay_san_xuat format. Expected dd/MM/yyyy. Error: {EscapeJsonString(ex.Message)}\"\n}}";
                        UpdateWebhookStatus(logId, "Failed", $"Invalid custom_ngay_san_xuat format: {ex.Message}");
                        SendJsonResponse(response, HttpStatusCode.BadRequest, errorResponse);
                        return;
                    }

                    // ---- Đợt 3B: MÃ LÔ CHÍNH THỨC LÀ BẮT BUỘC ----
                    // SCADA không được tự sinh mã lô nghiệp vụ. Mã chính thức đến từ Base
                    // (custom_lotno), sau này là mã BAT- từ MES. Thiếu mã => từ chối tạo lô,
                    // trả lỗi cho Base để người lập kế hoạch bổ sung, thay vì lặng lẽ
                    // dùng mã máy tự sinh rồi mã đó chui lên hồ sơ QA/QC như số lô.
                    string officialLotCode = "";
                    if (paramsDict.ContainsKey("custom_lotno")) officialLotCode = paramsDict["custom_lotno"].Trim();
                    else if (paramsDict.ContainsKey("lotno")) officialLotCode = paramsDict["lotno"].Trim();

                    if (string.IsNullOrEmpty(officialLotCode) && RequireOfficialLotCode)
                    {
                        string errorResponse = "{\n  \"success\": false,\n  \"message\": \"Thiếu mã lô chính thức (custom_lotno). " +
                                               "Lô sản xuất phải có mã lô chính thức từ Base/MES; SCADA không tự sinh mã lô. " +
                                               "Vui lòng bổ sung trường custom_lotno rồi gửi lại.\"\n}";
                        UpdateWebhookStatus(logId, "Failed", "Thiếu mã lô chính thức (custom_lotno) — từ chối tạo lô");
                        SendJsonResponse(response, HttpStatusCode.BadRequest, errorResponse);
                        return;
                    }

                    // Extract other properties
                    // Đợt 3B: KHÔNG còn mặc định "TX01" — đoán máy nghĩa là gán dữ liệu sản xuất
                    // cho một máy có thật mà không ai biết.
                    string deviceName = "";
                    if (paramsDict.ContainsKey("custom_thiet_bi_su_dung")) deviceName = paramsDict["custom_thiet_bi_su_dung"].Trim();
                    else if (paramsDict.ContainsKey("thiet_bi_su_dung")) deviceName = paramsDict["thiet_bi_su_dung"].Trim();

                    if (string.IsNullOrEmpty(deviceName))
                    {
                        string errorResponse = "{\n  \"success\": false,\n  \"message\": \"Thiếu thiết bị sản xuất (custom_thiet_bi_su_dung). " +
                                               "Phải nêu rõ máy, hệ không tự đoán máy.\"\n}";
                        UpdateWebhookStatus(logId, "Failed", "Thiếu custom_thiet_bi_su_dung — từ chối tạo lô");
                        SendJsonResponse(response, HttpStatusCode.BadRequest, errorResponse);
                        return;
                    }

                    string machineError;
                    if (!Helper.MachineRegistry.IsKnownDevice(connectionString, deviceName, out machineError))
                    {
                        string errorResponse = "{\n  \"success\": false,\n  \"message\": \"" +
                                               EscapeJsonString(machineError) + "\"\n}";
                        UpdateWebhookStatus(logId, "Failed", "Thiết bị không hợp lệ: " + machineError);
                        SendJsonResponse(response, HttpStatusCode.BadRequest, errorResponse);
                        return;
                    }

                    int totalRuns = 1;
                    string totalRunsRaw = "";
                    if (paramsDict.ContainsKey("custom_so_me_san_xuat")) totalRunsRaw = paramsDict["custom_so_me_san_xuat"];
                    else if (paramsDict.ContainsKey("so_me_san_xuat")) totalRunsRaw = paramsDict["so_me_san_xuat"];
                    if (!string.IsNullOrEmpty(totalRunsRaw))
                    {
                        int.TryParse(totalRunsRaw, out totalRuns);
                    }
                    if (totalRuns < 1) totalRuns = 1;

                    // Product metadata
                    string productName = "";
                    if (paramsDict.ContainsKey("custom_ten_hang_hoa")) productName = paramsDict["custom_ten_hang_hoa"].Trim();
                    else if (paramsDict.ContainsKey("ten_hang_hoa")) productName = paramsDict["ten_hang_hoa"].Trim();

                    string productCode = "";
                    if (paramsDict.ContainsKey("custom_ma_dinh_danh")) productCode = paramsDict["custom_ma_dinh_danh"].Trim();
                    else if (paramsDict.ContainsKey("ma_dinh_danh")) productCode = paramsDict["ma_dinh_danh"].Trim();

                    string manufacturer = "";
                    if (paramsDict.ContainsKey("custom_nha_san_xuat")) manufacturer = paramsDict["custom_nha_san_xuat"].Trim();
                    else if (paramsDict.ContainsKey("nha_san_xuat")) manufacturer = paramsDict["nha_san_xuat"].Trim();

                    double targetWeight = 0;
                    string targetWeightRaw = "";
                    if (paramsDict.ContainsKey("custom_khoi_luong_muc_tieu")) targetWeightRaw = paramsDict["custom_khoi_luong_muc_tieu"];
                    else if (paramsDict.ContainsKey("khoi_luong_muc_tieu")) targetWeightRaw = paramsDict["khoi_luong_muc_tieu"];
                    if (!string.IsNullOrEmpty(targetWeightRaw))
                    {
                        double.TryParse(targetWeightRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out targetWeight);
                    }

                    string formula = "";
                    if (paramsDict.ContainsKey("custom_cong_thuc")) formula = paramsDict["custom_cong_thuc"].Trim();
                    else if (paramsDict.ContainsKey("cong_thuc")) formula = paramsDict["cong_thuc"].Trim();

                    string productLotCode = "";
                    if (paramsDict.ContainsKey("custom_ma_lo_thanh_pham")) productLotCode = paramsDict["custom_ma_lo_thanh_pham"].Trim();
                    else if (paramsDict.ContainsKey("ma_lo_thanh_pham")) productLotCode = paramsDict["ma_lo_thanh_pham"].Trim();

                    // Synchronously create batch under DB Lock to ensure correct sequence (STT)
                    string batchName = "";
                    int batchId = 0;
                    try
                    {
                        lock (dbLock)
                        {
                            using (var conn = new MySqlConnection(connectionString))
                            {
                                conn.Open();
                                EnsureWebhookTableExists(conn);

                                // Mã lô chính thức phải duy nhất trong các mẻ chưa huỷ. Đẩy lại cùng mã
                                // (MES bấm lại sau lỗi mạng) trả mẻ cũ, không sinh mẻ mới; mẻ đã huỷ
                                // không tính nên đẩy lại sau khi huỷ vẫn tạo được mẻ mới.
                                // Kiểm trong cùng lock với lệnh INSERT để hai lần đẩy đồng thời
                                // không lọt qua nhau. KHÔNG tự thêm hậu tố vào mã nghiệp vụ.
                                if (!string.IsNullOrEmpty(officialLotCode))
                                {
                                    string existingName = null;
                                    string existingStatus = null;
                                    int existingId = 0;
                                    bool existingOrphan = false;
                                    using (var cmdDup = new MySqlCommand(
                                        "SELECT b.`name`, b.`status`, b.`id`, " +
                                        "  b.`status` = 'Pending' AND b.`created_at` < NOW() - INTERVAL 60 SECOND " +
                                        "  AND NOT EXISTS (SELECT 1 FROM `runs` r WHERE r.`batch_id` = b.`id`) " +
                                        "FROM `batches` b " +
                                        "WHERE b.`official_lot_code` = @lot AND b.`status` <> 'Cancelled' " +
                                        "ORDER BY b.`id` DESC LIMIT 1", conn))
                                    {
                                        cmdDup.Parameters.AddWithValue("@lot", officialLotCode);
                                        using (var readerDup = cmdDup.ExecuteReader())
                                        {
                                            if (readerDup.Read())
                                            {
                                                existingName = readerDup.GetString(0);
                                                existingStatus = readerDup.GetString(1);
                                                existingId = readerDup.GetInt32(2);
                                                existingOrphan = readerDup.GetInt64(3) == 1;
                                            }
                                        }
                                    }

                                    // Lần đẩy trước đổ giữa chừng để lại lô không có run: máy không bao giờ
                                    // nhận lô đó, nên đóng lại rồi tạo mẻ mới thay vì trả "đã có".
                                    if (existingOrphan)
                                    {
                                        CancelBatchById(existingId);
                                        existingName = null;
                                    }

                                    if (existingName != null)
                                    {
                                        UpdateWebhookStatus(logId, "Duplicate", $"Trùng mã lô chính thức '{officialLotCode}', trả mẻ cũ '{existingName}'");
                                        string duplicateJson = $"{{\n  \"success\": true,\n  \"message\": \"Batch already exists\",\n" +
                                                               $"  \"duplicate\": true,\n" +
                                                               $"  \"status\": \"{EscapeJsonString(existingStatus)}\",\n" +
                                                               $"  \"official_lot_code\": \"{EscapeJsonString(officialLotCode)}\",\n" +
                                                               $"  \"machine_session_code\": \"{EscapeJsonString(existingName)}\",\n" +
                                                               $"  \"batch_name\": \"{EscapeJsonString(existingName)}\"\n}}";
                                        SendJsonResponse(response, HttpStatusCode.OK, duplicateJson);
                                        return;
                                    }
                                }

                                // Find next STT sequence for this device and production date (for fallback)
                                int nextStt = 1;
                                string namePattern = $"{deviceName}-{prodDate:yyyyMMdd}-%";
                                string selectSttQuery = "SELECT `name` FROM `batches` " +
                                                         "WHERE `name` LIKE @name_pattern";
                                using (var cmd = new MySqlCommand(selectSttQuery, conn))
                                {
                                    cmd.Parameters.AddWithValue("@name_pattern", namePattern);
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            string name = reader.GetString(0);
                                            var parts = name.Split('-');
                                            if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 1], out int stt))
                                            {
                                                if (stt >= nextStt)
                                                {
                                                    nextStt = stt + 1;
                                                }
                                            }
                                        }
                                    }
                                }

                                string dateStr = prodDate.ToString("yyyyMMdd");
                                if (string.IsNullOrEmpty(productCode))
                                {
                                    batchName = $"{deviceName}-{dateStr}-{nextStt:D2}";
                                }
                                else
                                {
                                    string baseBatchName = $"{deviceName}-{dateStr}-{productCode}";
                                    batchName = baseBatchName;

                                    // Check if baseBatchName already exists to handle duplicates
                                    bool exists = false;
                                    using (var cmdCheck = new MySqlCommand("SELECT COUNT(*) FROM `batches` WHERE `name` = @name", conn))
                                    {
                                        cmdCheck.Parameters.AddWithValue("@name", baseBatchName);
                                        exists = Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0;
                                    }

                                    if (exists)
                                    {
                                        int nextSuffix = 2;
                                        string pattern = $"{baseBatchName}-%";
                                        using (var cmdStt = new MySqlCommand("SELECT `name` FROM `batches` WHERE `name` LIKE @pattern ORDER BY `id` DESC LIMIT 1", conn))
                                        {
                                            cmdStt.Parameters.AddWithValue("@pattern", pattern);
                                            var obj = cmdStt.ExecuteScalar();
                                            if (obj != null && obj != DBNull.Value)
                                            {
                                                string lastConflictedName = obj.ToString();
                                                if (lastConflictedName.Length > baseBatchName.Length + 1)
                                                {
                                                    string suffixStr = lastConflictedName.Substring(baseBatchName.Length + 1);
                                                    if (int.TryParse(suffixStr, out int lastStt))
                                                    {
                                                        nextSuffix = lastStt + 1;
                                                    }
                                                }
                                            }
                                        }
                                        batchName = $"{baseBatchName}-{nextSuffix:D2}";
                                    }
                                }

                                // Insert the batch
                                // Đợt 3B: `name` nay chỉ là MÃ PHIÊN CHẠY MÁY kỹ thuật;
                                // mã lô nghiệp vụ nằm ở `official_lot_code`.
                                string insertBatchQuery = "INSERT INTO `batches` (`name`, `official_lot_code`, `product_lot_code`, `device_name`, `date`, `product_name`, `product_code`, `manufacturer`, `target_weight`, `formula`, `status`, `total_runs`, `created_at`) " +
                                                           "VALUES (@name, @official_lot_code, @product_lot_code, @device_name, @date, @product_name, @product_code, @manufacturer, @target_weight, @formula, 'Pending', @total_runs, NOW())";
                                using (var cmd = new MySqlCommand(insertBatchQuery, conn))
                                {
                                    cmd.Parameters.AddWithValue("@name", batchName);
                                    cmd.Parameters.AddWithValue("@official_lot_code",
                                        string.IsNullOrEmpty(officialLotCode) ? (object)DBNull.Value : officialLotCode);
                                    cmd.Parameters.AddWithValue("@product_lot_code",
                                        string.IsNullOrEmpty(productLotCode) ? (object)DBNull.Value : productLotCode);
                                    cmd.Parameters.AddWithValue("@device_name", deviceName);
                                    cmd.Parameters.AddWithValue("@date", prodDate.ToString("yyyy-MM-dd"));
                                    cmd.Parameters.AddWithValue("@product_name", productName);
                                    cmd.Parameters.AddWithValue("@product_code", productCode);
                                    cmd.Parameters.AddWithValue("@manufacturer", manufacturer);
                                    cmd.Parameters.AddWithValue("@target_weight", targetWeight);
                                    cmd.Parameters.AddWithValue("@formula", formula);
                                    cmd.Parameters.AddWithValue("@total_runs", totalRuns);
                                    cmd.ExecuteNonQuery();
                                    batchId = (int)cmd.LastInsertedId;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        string errorResponse = $"{{\n  \"success\": false,\n  \"message\": \"Failed to create batch: {EscapeJsonString(ex.Message)}\"\n}}";
                        UpdateWebhookStatus(logId, "Failed", $"Failed to create batch: {ex.Message}");
                        SendJsonResponse(response, HttpStatusCode.InternalServerError, errorResponse);
                        return;
                    }

                    // Tạo mẻ chạy và BOM TRƯỚC khi trả lời: bên gọi nhận 200 nghĩa là mẻ đã nằm đủ
                    // trong hàng chờ. Lỗi thì đóng lô để đẩy lại tạo được mẻ mới.
                    if (!CreateRunsAndBom(logId, batchId, batchName, totalRuns, deviceName, paramsDict))
                    {
                        CancelBatchById(batchId);
                        string errorResponse = "{\n  \"success\": false,\n  \"message\": \"Không tạo được mẻ chạy hoặc BOM cho lô. " +
                                               "Kiểm tra dữ liệu BOM rồi đẩy lại.\"\n}";
                        SendJsonResponse(response, HttpStatusCode.InternalServerError, errorResponse);
                        return;
                    }

                    // Synchronously return success response.
                    // Đợt 3B: trả rõ hai mã tách bạch — official_lot_code là MÃ LÔ,
                    // machine_session_code là mã phiên chạy máy kỹ thuật (không phải mã lô).
                    // Giữ lại "batch_name" để bên gọi cũ không vỡ.
                    string successJson = $"{{\n  \"success\": true,\n  \"message\": \"Batch created successfully\",\n" +
                                         $"  \"duplicate\": false,\n" +
                                         $"  \"status\": \"Pending\",\n" +
                                         $"  \"official_lot_code\": \"{EscapeJsonString(officialLotCode)}\",\n" +
                                         $"  \"machine_session_code\": \"{EscapeJsonString(batchName)}\",\n" +
                                         $"  \"batch_name\": \"{EscapeJsonString(batchName)}\"\n}}";
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

        private int SaveWebhookPayloadSync(string rawPayload)
        {
            lock (dbLock)
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    EnsureWebhookTableExists(conn);

                    string insertQuery = "INSERT INTO `webhook_logs` (`received_at`, `payload`, `status`) " +
                                         "VALUES (NOW(), @payload, 'Pending')";
                    using (var cmd = new MySqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@payload", rawPayload);
                        cmd.ExecuteNonQuery();
                        return (int)cmd.LastInsertedId;
                    }
                }
            }
        }

        private void UpdateWebhookStatus(int logId, string status, string errorMessage = null)
        {
            if (logId <= 0) return;
            lock (dbLock)
            {
                try
                {
                    using (var conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();
                        string updateQuery = "UPDATE `webhook_logs` SET `status` = @status, `error_message` = @error_message WHERE `id` = @id";
                        using (var cmd = new MySqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@status", status);
                            cmd.Parameters.AddWithValue("@error_message", (object)errorMessage ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@id", logId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] ERROR updating webhook status: {ex.Message}");
                }
            }
        }

        private void CancelBatchById(int batchId)
        {
            lock (dbLock)
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand(
                        "UPDATE `runs` SET `status` = 'Cancelled' WHERE `batch_id` = @id AND `status` = 'Pending'", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", batchId);
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new MySqlCommand(
                        "UPDATE `batches` SET `status` = 'Cancelled', `end_time` = NOW() WHERE `id` = @id AND `status` = 'Pending'", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", batchId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private bool CreateRunsAndBom(int logId, int batchId, string batchName, int totalRuns, string deviceName, System.Collections.Generic.Dictionary<string, string> paramsDict)
        {
            try
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();

                int maxOrder = 0;
                // Query max execution_order for this device
                lock (dbLock)
                {
                    using (var conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();
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
                    }
                }

                for (int r = 1; r <= totalRuns; r++)
                {
                    int currentExecutionOrder = maxOrder + r;
                    string runName = $"{batchName}-Me{r:D2}";
                    int runId = 0;

                    // 1. Create the run record in DB
                    lock (dbLock)
                    {
                        using (var conn = new MySqlConnection(connectionString))
                        {
                            conn.Open();
                            EnsureWebhookTableExists(conn);

                            // Lệnh huỷ có thể tới giữa chừng. Chỉ tạo khi lô chưa huỷ:
                            // run Pending sót lại sẽ bị máy ăn ở lần chạy kế tiếp.
                            string insertRunQuery = "INSERT INTO `runs` (`batch_id`, `run_number`, `name`, `status`, `execution_order`, `created_at`) " +
                                                    "SELECT @batch_id, @run_number, @name, 'Pending', @execution_order, NOW() " +
                                                    "FROM `batches` WHERE `id` = @batch_id AND `status` <> 'Cancelled'";
                            using (var cmd = new MySqlCommand(insertRunQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@batch_id", batchId);
                                cmd.Parameters.AddWithValue("@run_number", r);
                                cmd.Parameters.AddWithValue("@name", runName);
                                cmd.Parameters.AddWithValue("@execution_order", currentExecutionOrder);
                                if (cmd.ExecuteNonQuery() == 0)
                                {
                                    UpdateWebhookStatus(logId, "Cancelled", "Lô đã huỷ trước khi tạo mẻ chạy");
                                    return false;
                                }
                                runId = (int)cmd.LastInsertedId;
                            }
                        }
                    }

                    // 2. Parse and import BOM if available
                    char suffix = (char)('a' + (r - 1));
                    string bomKeyCustom = "custom_thong_tin_bom_san_xuat_" + suffix;
                    string bomKeyClean = "thong_tin_bom_san_xuat_" + suffix;

                    string base64Data = null;
                    if (paramsDict.ContainsKey(bomKeyCustom))
                    {
                        base64Data = paramsDict[bomKeyCustom];
                    }
                    else if (paramsDict.ContainsKey(bomKeyClean))
                    {
                        base64Data = paramsDict[bomKeyClean];
                    }

                    if (!string.IsNullOrEmpty(base64Data))
                    {
                        string jsonString = "";
                        try
                        {
                            byte[] decodedBytes = Convert.FromBase64String(base64Data.Trim());
                            jsonString = Encoding.UTF8.GetString(decodedBytes);
                        }
                        catch (Exception decodeEx)
                        {
                            throw new Exception($"Failed to decode Base64 BOM data for run {r} (suffix {suffix}): {decodeEx.Message}");
                        }

                        if (!string.IsNullOrEmpty(jsonString))
                        {
                            System.Collections.Generic.List<System.Collections.Generic.List<object>> outerList = null;
                            try
                            {
                                outerList = serializer.Deserialize<System.Collections.Generic.List<System.Collections.Generic.List<object>>>(jsonString);
                            }
                            catch (Exception parseEx)
                            {
                                throw new Exception($"Failed to parse JSON BOM array for run {r}: {parseEx.Message}. JSON: {jsonString}");
                            }

                            if (outerList != null)
                            {
                                foreach (var row in outerList)
                                {
                                    if (row == null || row.Count < 6) continue;

                                    string code = row[0]?.ToString() ?? "";
                                    string materialCode = row[1]?.ToString() ?? "";
                                    string quantityStr = row[2]?.ToString() ?? "0";
                                    string valStr = row[3]?.ToString() ?? "";
                                    string unit = row[4]?.ToString() ?? "";
                                    string batchNo = row[5]?.ToString() ?? "";

                                    double quantity = 0;
                                    double.TryParse(quantityStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out quantity);

                                    lock (dbLock)
                                    {
                                        using (var conn = new MySqlConnection(connectionString))
                                        {
                                            conn.Open();
                                            string insertBomQuery = "INSERT INTO `run_info` (`run_id`, `code`, `material_code`, `quantity`, `value`, `unit`, `batch_no`, `created_at`) " +
                                                                    "VALUES (@run_id, @code, @material_code, @quantity, @value, @unit, @batch_no, NOW())";
                                            using (var cmd = new MySqlCommand(insertBomQuery, conn))
                                            {
                                                cmd.Parameters.AddWithValue("@run_id", runId);
                                                cmd.Parameters.AddWithValue("@code", code);
                                                cmd.Parameters.AddWithValue("@material_code", materialCode);
                                                cmd.Parameters.AddWithValue("@quantity", quantity);
                                                cmd.Parameters.AddWithValue("@value", valStr);
                                                cmd.Parameters.AddWithValue("@unit", unit);
                                                cmd.Parameters.AddWithValue("@batch_no", batchNo);
                                                cmd.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // If processing succeeds, update status to Completed
                UpdateWebhookStatus(logId, "Completed");
                System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] Webhook log ID {logId} processed and runs/BOMs created successfully.");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebhookHttpServer] ERROR processing background runs/BOMs for webhook log ID {logId}: {ex.Message}");
                UpdateWebhookStatus(logId, "Failed", ex.ToString());
                return false;
            }
        }

        private static System.Collections.Generic.Dictionary<string, string> ParseFormUrlEncoded(string rawBody)
        {
            var dict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(rawBody)) return dict;
            
            string[] pairs = rawBody.Split('&');
            foreach (string pair in pairs)
            {
                if (string.IsNullOrEmpty(pair)) continue;
                int equalIndex = pair.IndexOf('=');
                if (equalIndex > 0)
                {
                    string key = System.Net.WebUtility.UrlDecode(pair.Substring(0, equalIndex));
                    string value = System.Net.WebUtility.UrlDecode(pair.Substring(equalIndex + 1));
                    dict[key] = value;
                }
                else if (equalIndex == -1)
                {
                    string key = System.Net.WebUtility.UrlDecode(pair);
                    dict[key] = "";
                }
            }
            return dict;
        }

        private void EnsureWebhookTableExists(MySqlConnection conn)
        {
            // 1. Create webhook_logs table
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

            // Đợt 3B: bảng danh mục máy + cột mã lô chính thức (dùng chung 1 nguồn với Read API)
            Db.SchemaMigrator.EnsureDot3BSchema(conn);

            // 2. Add product metadata columns to batches table if they don't exist
            string[] columnsToAdd = new string[]
            {
                "`date` DATE NULL AFTER `device_name`",
                "`product_name` VARCHAR(255) NULL AFTER `date`",
                "`product_code` VARCHAR(100) NULL AFTER `product_name`",
                "`manufacturer` VARCHAR(255) NULL AFTER `product_code`",
                "`target_weight` DOUBLE NOT NULL DEFAULT 0 AFTER `manufacturer`",
                "`formula` VARCHAR(100) NULL AFTER `target_weight`"
            };

            string[] columnNames = new string[] { "date", "product_name", "product_code", "manufacturer", "target_weight", "formula" };

            for (int i = 0; i < columnNames.Length; i++)
            {
                try
                {
                    string checkColQuery = $"SHOW COLUMNS FROM `batches` LIKE '{columnNames[i]}'";
                    using (var cmd = new MySqlCommand(checkColQuery, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result == null || result == DBNull.Value)
                        {
                            string alterQuery = $"ALTER TABLE `batches` ADD COLUMN {columnsToAdd[i]}";
                            using (var alterCmd = new MySqlCommand(alterQuery, conn))
                            {
                                alterCmd.ExecuteNonQuery();
                            }
                            System.Diagnostics.Debug.WriteLine($"[Migration] Added column {columnNames[i]} to batches table.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Migration] ERROR adding column {columnNames[i]} to batches: {ex.Message}");
                }
            }

            // 3. Create run_info table
            try
            {
                string createRunInfoSql = "CREATE TABLE IF NOT EXISTS `run_info` (" +
                                          "  `id` INT AUTO_INCREMENT PRIMARY KEY," +
                                          "  `run_id` INT NOT NULL," +
                                          "  `code` VARCHAR(100) NULL," +
                                          "  `material_code` VARCHAR(100) NULL," +
                                          "  `quantity` DOUBLE NOT NULL DEFAULT 0," +
                                          "  `value` VARCHAR(100) NULL," +
                                          "  `unit` VARCHAR(50) NULL," +
                                          "  `batch_no` VARCHAR(100) NULL," +
                                          "  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP," +
                                          "  FOREIGN KEY (`run_id`) REFERENCES `runs`(`id`) ON DELETE CASCADE," +
                                          "  INDEX `idx_run_info_run` (`run_id`)" +
                                          ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                using (var cmd = new MySqlCommand(createRunInfoSql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] ERROR creating run_info table: {ex.Message}");
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
