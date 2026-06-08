Add-Type -Path "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\HinoTools.Alarm\bin\Debug\MySql.Data.dll"
Add-Type -Path "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\HinoTools.Data\bin\Debug\HinoTools.Data.dll"
Add-Type -Path "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\HinoTools.Alarm\bin\Debug\HinoTools.Alarm.dll"

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

# Seed test run
Write-Host "Seeding test run..." -ForegroundColor Cyan
$cmd = $conn.CreateCommand()

# Clear old test alarms
$cmd.CommandText = "DELETE FROM alarmlog WHERE TagName = 'AFChemTX01.ThoiGianCapLieu' AND Location = 'MixerTest'"
$cmd.ExecuteNonQuery() | Out-Null

$cmd.CommandText = "DELETE FROM realtime_alarms WHERE DeviceName = 'TX01' AND TagName = 'System' AND CongDoan = 'T001' AND Message = 'Duplicate Test Error'"
$cmd.ExecuteNonQuery() | Out-Null

# Create dummy run
$cmd.CommandText = "DELETE FROM runs WHERE name = 'TestBatch-Dup-Me01'"
$cmd.ExecuteNonQuery() | Out-Null
$cmd.CommandText = "DELETE FROM batches WHERE name = 'TestBatch-Dup'"
$cmd.ExecuteNonQuery() | Out-Null

$cmd.CommandText = "INSERT INTO batches (name, device_name, status, total_runs, start_time) VALUES ('TestBatch-Dup', 'TX01', 'Active', 1, NOW())"
$cmd.ExecuteNonQuery() | Out-Null
$batchId = $cmd.LastInsertedId

$cmd.CommandText = "INSERT INTO runs (batch_id, run_number, name, status, start_time) VALUES ($batchId, 1, 'TestBatch-Dup-Me01', 'Active', NOW())"
$cmd.ExecuteNonQuery() | Out-Null
$runId = $cmd.LastInsertedId

Write-Host "Created Batch ID: $batchId, Run ID: $runId" -ForegroundColor Yellow

# --- TEST 1: alarmlog Duplication Check ---
Write-Host "`n--- TEST 1: alarmlog Duplication Check ---" -ForegroundColor Cyan

# Instantiate AlarmLogger
$alarmLogger = New-Object HinoTools.Alarm.Control.AlarmLogger
$alarmLogger.ServerName = "localhost"
$alarmLogger.DatabaseName = "scada"
$alarmLogger.UserID = "root"
$alarmLogger.Password = "101101"
$alarmLogger.TableName = "alarmlog"

# Set private fields in AlarmLogger using Reflection
$flags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance
$dataAccess = New-Object HinoTools.Alarm.Database.DataAccess
$dataAccess.ConnectionString = $connString
$alarmLogger.GetType().GetField("dataAccess", $flags).SetValue($alarmLogger, $dataAccess)

# Construct AlarmParam
$param = New-Object HinoTools.Alarm.Model.AlarmParam
$param.TagName = "AFChemTX01.ThoiGianCapLieu"
$param.TagNo = "T001"
$param.Location = "MixerTest"
$param.Description = "Timer Cấp Liệu"
$param.Value = "0"
$param.Type = [HinoTools.Alarm.Model.AlarmType]::Continuous

# Helper function to invoke GetActiveBatchAndRunId to make sure it runs correctly
# In InsertAlarm, we override the runId returned, so we'll mock the returned runId/batchId in a custom mock or test harness, or since it reads the active run from DB:
# GetActiveBatchAndRunId queries DB for Active run on TX01. Our seeded run 'TestBatch-Dup-Me01' is active for device 'TX01'!
# So it will naturally resolve to our batchId and runId!

$insertAlarmMethod = $alarmLogger.GetType().GetMethod("InsertAlarm", $flags)

# Trigger Alarm Occurrence 1 (GUID 1)
$item1 = New-Object HinoTools.Alarm.Model.AlarmItem
$item1.ID = [Guid]::NewGuid().ToString()
$item1.Param = $param
$item1.Status = [HinoTools.Alarm.Model.AlarmStatus]::ALARM
$item1.OccurrenceTime = [DateTime]::Now

Write-Host "Logging Alarm Occurrence 1 (ID: $($item1.ID))..." -ForegroundColor Gray
$insertAlarmMethod.Invoke($alarmLogger, @($item1.psobject.BaseObject))

# Trigger Alarm Occurrence 2 (GUID 2) - Simulation of app restart where tag is still active
$item2 = New-Object HinoTools.Alarm.Model.AlarmItem
$item2.ID = [Guid]::NewGuid().ToString()
$item2.Param = $param
$item2.Status = [HinoTools.Alarm.Model.AlarmStatus]::ALARM
$item2.OccurrenceTime = [DateTime]::Now

Write-Host "Logging Alarm Occurrence 2 (ID: $($item2.ID)) - simulating app restart..." -ForegroundColor Gray
$insertAlarmMethod.Invoke($alarmLogger, @($item2.psobject.BaseObject))

# Verify alarmlog row count (should be 1)
$cmd.CommandText = "SELECT COUNT(*) FROM alarmlog WHERE TagName = 'AFChemTX01.ThoiGianCapLieu' AND Location = 'MixerTest'"
$cnt = $cmd.ExecuteScalar()
$color1 = if ($cnt -eq 1) { "Green" } else { "Red" }
Write-Host "Alarmlog count for this run: $cnt (Expected: 1)" -ForegroundColor $color1

# Retrieve the ID from alarmlog
$cmd.CommandText = "SELECT ID, Status, OccurrenceTime, RestoreTime, runId FROM alarmlog WHERE TagName = 'AFChemTX01.ThoiGianCapLieu' AND Location = 'MixerTest'"
$ad = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$ad.Fill($dt) | Out-Null
$dt | Format-Table -AutoSize
$loggedId = $dt.Rows[0]["ID"].ToString()

# Trigger Alarm Resolution (GUID 2) - Simulating resolve after restart
$item3 = New-Object HinoTools.Alarm.Model.AlarmItem
$item3.ID = $item2.ID
$item3.Param = $param
$item3.Status = [HinoTools.Alarm.Model.AlarmStatus]::NORMAL # Resolves
$item3.OccurrenceTime = $item2.OccurrenceTime
$item3.RestoreTime = [DateTime]::Now

Write-Host "Logging Alarm Resolution (ID: $($item3.ID) - Resolves)..." -ForegroundColor Gray
$insertAlarmMethod.Invoke($alarmLogger, @($item3.psobject.BaseObject))

# Verify alarmlog status (should be Resolved and RestoreTime should be set on the original ID!)
$cmd.CommandText = "SELECT ID, Status, OccurrenceTime, RestoreTime, runId FROM alarmlog WHERE TagName = 'AFChemTX01.ThoiGianCapLieu' AND Location = 'MixerTest'"
$dt2 = New-Object System.Data.DataTable
$ad.Fill($dt2) | Out-Null
Write-Host "After resolution verification:" -ForegroundColor Yellow
$dt2 | Format-Table -AutoSize


# --- TEST 2: realtime_alarms Duplication Check ---
Write-Host "`n--- TEST 2: realtime_alarms Duplication Check ---" -ForegroundColor Cyan

# Instantiate AlarmReportLogger
$reportLogger = New-Object HinoTools.Data.Log.AlarmReportLogger
$reportLogger.ServerName = "localhost"
$reportLogger.DatabaseName = "scada"
$reportLogger.UserID = "root"
$reportLogger.Password = "101101"

$dataAccess2 = New-Object HinoTools.Data.Database.DataAccess
$dataAccess2.ConnectionString = $connString
$reportLogger.GetType().GetField("dataAccess", $flags).SetValue($reportLogger, $dataAccess2)
$reportLogger.GetType().GetField("activeRunId", $flags).SetValue($reportLogger, [int]$runId)
$reportLogger.GetType().GetField("activeBatchId", $flags).SetValue($reportLogger, [int]$batchId)
$reportLogger.GetType().GetField("deviceName", $flags).SetValue($reportLogger, "TX01")
$reportLogger.GetType().GetField("currentQuyTrinh", $flags).SetValue($reportLogger, 1)

$insertErrorMethod = $reportLogger.GetType().GetMethod("InsertRealtimeErrorEvent", $flags)

# Log error alarm 1
Write-Host "Logging Error Alarm 1..." -ForegroundColor Gray
$insertErrorMethod.Invoke($reportLogger, @("T001", "Duplicate Test Error")) | Out-Null

# Log error alarm 2 (Duplicate)
Write-Host "Logging Error Alarm 2 (Duplicate)..." -ForegroundColor Gray
$insertErrorMethod.Invoke($reportLogger, @("T001", "Duplicate Test Error")) | Out-Null

# Verify realtime_alarms row count (should be 1)
$cmd.CommandText = "SELECT COUNT(*) FROM realtime_alarms WHERE DeviceName = 'TX01' AND TagName = 'System' AND CongDoan = 'T001' AND Message = 'Duplicate Test Error' AND runId = $runId"
$cnt2 = $cmd.ExecuteScalar()
$color2 = if ($cnt2 -eq 1) { "Green" } else { "Red" }
Write-Host "Realtime error alarms count for this run: $cnt2 (Expected: 1)" -ForegroundColor $color2

# Display recorded alarms
$ad2 = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt3 = New-Object System.Data.DataTable
$ad2.Fill($dt3) | Out-Null
$dt3 | Format-Table -AutoSize

$conn.Close()
Write-Host "Duplicate checks integration tests completed!" -ForegroundColor Green
