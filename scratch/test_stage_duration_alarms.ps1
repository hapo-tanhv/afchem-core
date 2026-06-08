Add-Type -Path "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\HinoTools.Alarm\bin\Debug\MySql.Data.dll"
Add-Type -Path "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\HinoTools.Data\bin\Debug\HinoTools.Data.dll"

$connString = "Server=localhost;Uid=root;Pwd=101101;Database=scada"

# Check MySQL connection
try {
    $conn = New-Object MySql.Data.MySqlClient.MySqlConnection($connString)
    $conn.Open()
    Write-Host "MySQL Connection OK!" -ForegroundColor Green
} catch {
    Write-Host "MySQL Connection FAILED: $_" -ForegroundColor Red
    exit
}

# Seed configuration in alarmsettings and test batch/run in runs
Write-Host "Setting up test configuration and runs in DB..." -ForegroundColor Cyan

# 1. Clean up old test data
$cmd = $conn.CreateCommand()
$cmd.CommandText = "DELETE FROM realtime_alarms WHERE DeviceName = 'TX01' AND TagName = 'AFChemTX01.ThoiGianCapLieu'"
$cmd.ExecuteNonQuery() | Out-Null

$cmd.CommandText = "DELETE FROM runs WHERE name LIKE 'TestBatch-StageAlarm%'"
$cmd.ExecuteNonQuery() | Out-Null

$cmd.CommandText = "DELETE FROM batches WHERE name = 'TestBatch-StageAlarm'"
$cmd.ExecuteNonQuery() | Out-Null

$cmd.CommandText = "DELETE FROM alarmsettings WHERE TagNo = 'T001'"
$cmd.ExecuteNonQuery() | Out-Null

# 2. Insert or update alarmsettings configuration
$cmd.CommandText = @"
INSERT INTO alarmsettings (TagName, TagNo, Value, Location, Description, Type, Level, FaultCode)
VALUES ('AFChemTX01.ThoiGianCapLieu', 'T001', '5', 'Mixer', 'Timer Cấp Liệu', 2, 0, 0)
ON DUPLICATE KEY UPDATE Value = '5', Type = 2
"@
$cmd.ExecuteNonQuery() | Out-Null

# 3. Create dummy batch
$cmd.CommandText = "INSERT INTO batches (name, device_name, status, total_runs, start_time) VALUES ('TestBatch-StageAlarm', 'TX01', 'Active', 1, NOW())"
$cmd.ExecuteNonQuery() | Out-Null
$batchId = $cmd.LastInsertedId

# 4. Create dummy run with setpoint = 10s
$cmd.CommandText = "INSERT INTO runs (batch_id, run_number, name, status, sp_thoi_gian_cap_lieu, start_time) VALUES ($batchId, 1, 'TestBatch-StageAlarm-Me01', 'Active', 10, NOW())"
$cmd.ExecuteNonQuery() | Out-Null
$runId = $cmd.LastInsertedId

Write-Host "Created Batch ID: $batchId, Run ID: $runId with sp_thoi_gian_cap_lieu = 10s, threshold = 5s" -ForegroundColor Yellow

# Instantiate AlarmReportLogger
$logger = New-Object HinoTools.Data.Log.AlarmReportLogger
$logger.ServerName = "localhost"
$logger.DatabaseName = "scada"
$logger.UserID = "root"
$logger.Password = "101101"

# Inject private fields using Reflection
$bindingFlags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance

$dataAccess = New-Object HinoTools.Data.Database.DataAccess
$dataAccess.ConnectionString = $connString
$logger.GetType().GetField("dataAccess", $bindingFlags).SetValue($logger, $dataAccess)
$logger.GetType().GetField("activeRunId", $bindingFlags).SetValue($logger, [int]$runId)
$logger.GetType().GetField("activeBatchId", $bindingFlags).SetValue($logger, [int]$batchId)
$logger.GetType().GetField("deviceName", $bindingFlags).SetValue($logger, "TX01")
$logger.GetType().GetField("currentQuyTrinh", $bindingFlags).SetValue($logger, 1)

$checkMethod = $logger.GetType().GetMethod("CheckAndLogStageDurationAlarm", $bindingFlags)

# Start tests
Write-Host "`nRunning Test Cases..." -ForegroundColor Cyan

# Test Case 1: Actual = 20s (Deviation = 10s > Threshold 5s) -> Expected: ALARM (Over-processed)
Write-Host "Test Case 1: Simulating ThoiGianCapLieu actual duration = 20s (Setpoint = 10s, Threshold = 5s)..." -ForegroundColor Gray
$checkMethod.Invoke($logger, @("T001", [double]20.0))

# Test Case 2: Actual = 12s (Deviation = 2s <= Threshold 5s) -> Expected: NO ALARM (Within limit)
Write-Host "Test Case 2: Simulating ThoiGianCapLieu actual duration = 12s (Setpoint = 10s, Threshold = 5s)..." -ForegroundColor Gray
$checkMethod.Invoke($logger, @("T001", [double]12.0))

# Test Case 3: Actual = 3s (Deviation = 7s > Threshold 5s) -> Expected: ALARM (Under-processed)
Write-Host "Test Case 3: Simulating ThoiGianCapLieu actual duration = 3s (Setpoint = 10s, Threshold = 5s)..." -ForegroundColor Gray
$checkMethod.Invoke($logger, @("T001", [double]3.0))

# Query and display realtime_alarms matching our test tag
Write-Host "`nVerifying written alarms in realtime_alarms table:" -ForegroundColor Yellow
$cmd.CommandText = "SELECT DateTime, DeviceName, TagName, Value, Threshold, Message, Severity FROM realtime_alarms WHERE DeviceName = 'TX01' AND TagName = 'AFChemTX01.ThoiGianCapLieu' ORDER BY ID ASC"
$ad = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$ad.Fill($dt) | Out-Null
$dt | Format-Table -AutoSize

$conn.Close()

Write-Host "Test completed!" -ForegroundColor Green
