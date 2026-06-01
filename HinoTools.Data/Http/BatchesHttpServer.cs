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
        private const string DefaultDevice = "TX01";
        private readonly object dbLock = new object();
        private int serverPort;

        public BatchesHttpServer(string connectionString, int port = 5500)
        {
            this.connectionString = connectionString;
            this.serverPort = port;
            listener = new HttpListener();
            // Listen on port from any source IP
            listener.Prefixes.Add($"http://*:{port}/");
        }

        public void Start()
        {
            if (isRunning) return;
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
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept");

            // Handle pre-flight OPTIONS request
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
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
                    string deviceName = DefaultDevice;
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
                        deviceName = DefaultDevice;
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

                    // Parse runs_count (default is 1)
                    int runsCount = 1;
                    var runsCountMatch = Regex.Match(body, "\"runs_count\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase);
                    if (runsCountMatch.Success)
                    {
                        if (int.TryParse(runsCountMatch.Groups[1].Value, out int rc))
                        {
                            runsCount = rc;
                        }
                    }
                    else
                    {
                        var queryRunsCount = request.QueryString["runs_count"];
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
                                        string runName = $"{batchName}-Run{r:D2}";
                                        string insertRunQuery = "INSERT INTO `runs` (`batch_id`, `run_number`, `name`, `status`, `created_at`) " +
                                                                "VALUES (@batch_id, @run_number, @name, 'Pending', NOW())";
                                        using (var runCmd = new MySqlCommand(insertRunQuery, conn))
                                        {
                                            runCmd.Parameters.AddWithValue("@batch_id", insertedId);
                                            runCmd.Parameters.AddWithValue("@run_number", r);
                                            runCmd.Parameters.AddWithValue("@name", runName);
                                            runCmd.ExecuteNonQuery();
                                            int runInsertedId = (int)runCmd.LastInsertedId;

                                            string runJson = $"{{\n" +
                                                             $"        \"id\": {runInsertedId},\n" +
                                                             $"        \"run_number\": {r},\n" +
                                                             $"        \"name\": \"{runName}\",\n" +
                                                             $"        \"status\": \"Pending\"\n" +
                                                             $"      }}";
                                            createdRuns.Add(runJson);
                                        }
                                    }

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
                else if (request.HttpMethod == "GET" && request.Url.AbsolutePath.Equals("/api/batches", StringComparison.OrdinalIgnoreCase))
                {
                    string deviceName = request.QueryString["device_name"];
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

                            string selectQuery = "SELECT `id`, `name`, `device_name`, `status`, `total_runs`, `start_time`, `end_time`, `created_at` FROM `batches` ";
                            if (!string.IsNullOrEmpty(deviceName))
                            {
                                selectQuery += "WHERE `device_name` = @device_name ";
                            }
                            selectQuery += "ORDER BY `id` DESC LIMIT @limit";

                            using (var cmd = new MySqlCommand(selectQuery, conn))
                            {
                                if (!string.IsNullOrEmpty(deviceName))
                                {
                                    cmd.Parameters.AddWithValue("@device_name", deviceName);
                                }
                                cmd.Parameters.AddWithValue("@limit", limit);

                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        int id = reader.GetInt32(0);
                                        string name = reader.GetString(1);
                                        string devName = reader.GetString(2);
                                        string status = reader.GetString(3);
                                        int totalRuns = reader.GetInt32(4);
                                        string startTime = reader.IsDBNull(5) ? "null" : $"\"{reader.GetDateTime(5):yyyy-MM-dd HH:mm:ss}\"";
                                        string endTime = reader.IsDBNull(6) ? "null" : $"\"{reader.GetDateTime(6):yyyy-MM-dd HH:mm:ss}\"";
                                        string createdAt = reader.GetDateTime(7).ToString("yyyy-MM-dd HH:mm:ss");

                                        string item = $"{{\n" +
                                                      $"      \"id\": {id},\n" +
                                                      $"      \"name\": \"{name}\",\n" +
                                                      $"      \"device_name\": \"{devName}\",\n" +
                                                      $"      \"status\": \"{status}\",\n" +
                                                      $"      \"total_runs\": {totalRuns},\n" +
                                                      $"      \"start_time\": {startTime},\n" +
                                                      $"      \"end_time\": {endTime},\n" +
                                                      $"      \"created_at\": \"{createdAt}\"\n" +
                                                      $"    }}";
                                        batchList.Add(item);
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

                            string selectQuery = "SELECT `id`, `batch_id`, `run_number`, `name`, `status`, `start_time`, `end_time`, `created_at` FROM `runs` WHERE `batch_id` = @batch_id ORDER BY `run_number` ASC";
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
                                        string startTime = reader.IsDBNull(5) ? "null" : $"\"{reader.GetDateTime(5):yyyy-MM-dd HH:mm:ss}\"";
                                        string endTime = reader.IsDBNull(6) ? "null" : $"\"{reader.GetDateTime(6):yyyy-MM-dd HH:mm:ss}\"";
                                        string createdAt = reader.GetDateTime(7).ToString("yyyy-MM-dd HH:mm:ss");

                                        string item = $"{{\n" +
                                                      $"      \"id\": {id},\n" +
                                                      $"      \"batch_id\": {bId},\n" +
                                                      $"      \"run_number\": {runNum},\n" +
                                                      $"      \"name\": \"{name}\",\n" +
                                                      $"      \"status\": \"{status}\",\n" +
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

            // Task 1.4: Historical data migration (One-time check and execution)
            try
            {
                // Create a run for each batch that doesn't have runs yet
                string migrateRunsSql = "INSERT INTO `runs` (`batch_id`, `run_number`, `name`, `status`, `start_time`, `end_time`, `created_at`) " +
                                        "SELECT b.id, 1, CONCAT(b.name, '-Run01'), b.status, b.start_time, b.end_time, b.created_at " +
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
    }
}
