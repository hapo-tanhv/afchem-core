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

$cmd = $conn.CreateCommand()

# Clear old test stage alarms
$cmd.CommandText = "DELETE FROM realtime_alarms WHERE TagName = 'AFChemTX01.ThoiGianTestStageAlarm'"
$cmd.ExecuteNonQuery() | Out-Null

$runId = 17 # Use run from duplicate check or a new one
$batchId = 10

# Instantiate AlarmReportLogger
$reportLogger = New-Object HinoTools.Data.Log.AlarmReportLogger
$reportLogger.ServerName = "localhost"
$reportLogger.DatabaseName = "scada"
$reportLogger.UserID = "root"
$reportLogger.Password = "101101"

$flags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance
$dataAccess = New-Object HinoTools.Data.Database.DataAccess
$dataAccess.ConnectionString = $connString
$reportLogger.GetType().GetField("dataAccess", $flags).SetValue($reportLogger, $dataAccess)
$reportLogger.GetType().GetField("activeRunId", $flags).SetValue($reportLogger, [int]$runId)
$reportLogger.GetType().GetField("activeBatchId", $flags).SetValue($reportLogger, [int]$batchId)
$reportLogger.GetType().GetField("deviceName", $flags).SetValue($reportLogger, "TX01")
$reportLogger.GetType().GetField("currentQuyTrinh", $flags).SetValue($reportLogger, 1)

$insertStageAlarmMethod = $reportLogger.GetType().GetMethod("InsertRealtimeStageAlarm", $flags)

# Call InsertRealtimeStageAlarm
Write-Host "Invoking InsertRealtimeStageAlarm..." -ForegroundColor Gray
$res = $insertStageAlarmMethod.Invoke($reportLogger, @("AFChemTX01.ThoiGianTestStageAlarm", [double]120, [double]60, "Test Stage Duration Discrepancy", "T001"))
Write-Host "Insert return value: $res" -ForegroundColor Yellow

# Query DB state
$cmd.CommandText = "SELECT TagName, Value, Threshold, Severity, restore_time FROM realtime_alarms WHERE TagName = 'AFChemTX01.ThoiGianTestStageAlarm'"
$ad = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$ad.Fill($dt) | Out-Null

Write-Host "`nDatabase Verification:" -ForegroundColor Cyan
$dt | Format-Table -AutoSize

$severity = $dt.Rows[0]["Severity"].ToString()
$restoreTime = $dt.Rows[0]["restore_time"]

if ($severity -eq "INFO" -and $restoreTime -ne [DBNull]::Value) {
    Write-Host "SUCCESS: Stage alarm severity is INFO and restore_time is populated!" -ForegroundColor Green
} else {
    Write-Host "FAILURE: Severity is '$severity' (Expected: INFO) or restore_time is null!" -ForegroundColor Red
}

$conn.Close()
