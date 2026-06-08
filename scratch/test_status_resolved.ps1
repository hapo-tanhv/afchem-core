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

$cmd = $conn.CreateCommand()

# Clear old test alarms
$cmd.CommandText = "DELETE FROM alarmlog WHERE TagName = 'AFChemTX01.TestStageTag' AND Location = 'MixerTest'"
$cmd.ExecuteNonQuery() | Out-Null

# Create dummy run if not exists
$cmd.CommandText = "DELETE FROM runs WHERE name = 'TestBatch-Stage-Me01'"
$cmd.ExecuteNonQuery() | Out-Null
$cmd.CommandText = "DELETE FROM batches WHERE name = 'TestBatch-Stage'"
$cmd.ExecuteNonQuery() | Out-Null

$cmd.CommandText = "INSERT INTO batches (name, device_name, status, total_runs, start_time) VALUES ('TestBatch-Stage', 'TX01', 'Active', 1, NOW())"
$cmd.ExecuteNonQuery() | Out-Null
$batchId = $cmd.LastInsertedId

$cmd.CommandText = "INSERT INTO runs (batch_id, run_number, name, status, start_time) VALUES ($batchId, 1, 'TestBatch-Stage-Me01', 'Active', NOW())"
$cmd.ExecuteNonQuery() | Out-Null
$runId = $cmd.LastInsertedId

Write-Host "Created Seed Batch ID: $batchId, Run ID: $runId" -ForegroundColor Yellow

# Instantiate AlarmServer
$alarmServer = New-Object HinoTools.Alarm.Server.AlarmServer
$alarmServer.ServerName = "localhost"
$alarmServer.DatabaseName = "scada"
$alarmServer.UserID = "root"
$alarmServer.Password = "101101"
$alarmServer.TableLog = "alarmlog"
$alarmServer.Limit = 10

# Initialize private dataAccess field
$flags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance
$dataAccess = New-Object HinoTools.Alarm.Database.DataAccess
$dataAccess.ConnectionString = $connString
$alarmServer.GetType().GetField("dataAccess", $flags).SetValue($alarmServer, $dataAccess)

# Construct AlarmParam
$param = New-Object HinoTools.Alarm.Model.AlarmParam
$param.TagName = "AFChemTX01.TestStageTag"
$param.TagNo = "T100"
$param.Location = "MixerTest"
$param.Description = "Test Stage Duration"
$param.Value = "0"
$param.Type = [HinoTools.Alarm.Model.AlarmType]::Continuous

# Instantiate active alarm
$alarmItem = New-Object HinoTools.Alarm.Model.AlarmItem
$alarmItem.ID = [Guid]::NewGuid().ToString()
$alarmItem.Param = $param
$alarmItem.Status = [HinoTools.Alarm.Model.AlarmStatus]::ALARM
$alarmItem.OccurrenceTime = [DateTime]::Now
$alarmItem.IsAcknowledge = $false

# Add active alarm to alarmItems list in AlarmServer
$alarmItemsList = $alarmServer.GetType().GetField("alarmItems", $flags).GetValue($alarmServer)
$alarmItemsList.Add($alarmItem)

# Create normal/restored event
$normalItem = New-Object HinoTools.Alarm.Model.AlarmItem
$normalItem.ID = [Guid]::NewGuid().ToString() # New ID for the normal event
$normalItem.Param = $param
$normalItem.Status = [HinoTools.Alarm.Model.AlarmStatus]::NORMAL
$normalItem.OccurrenceTime = $alarmItem.OccurrenceTime
$normalItem.RestoreTime = [DateTime]::Now
$normalItem.IsAcknowledge = $true

# Pre-insert the active alarm in DB so UPDATE has something to update
$cmd.CommandText = "INSERT INTO alarmlog (ID, OccurrenceTime, RestoreTime, TagName, TagNo, Location, Description, Status, FaultCode, batchId, runId) " +
                   "VALUES ('$($alarmItem.ID)', '$($alarmItem.OccurrenceTime.ToString('yyyy-MM-dd HH:mm:ss'))', null, 'AFChemTX01.TestStageTag', 'T100', 'MixerTest', 'Test Stage Duration', 'Alarm', 0, $batchId, $runId)"
$cmd.ExecuteNonQuery() | Out-Null

Write-Host "Active alarm inserted in DB with Status: Alarm" -ForegroundColor Yellow

# Trigger ActionStatusChanged on AlarmServer
$actionStatusChangedMethod = $alarmServer.GetType().GetMethod("ActionStatusChanged", $flags)
Write-Host "Invoking ActionStatusChanged to resolve the alarm..." -ForegroundColor Gray
$actionStatusChangedMethod.Invoke($alarmServer, @($normalItem.psobject.BaseObject))

# Verify DB state
$cmd.CommandText = "SELECT ID, Status, OccurrenceTime, RestoreTime, runId FROM alarmlog WHERE TagName = 'AFChemTX01.TestStageTag' AND Location = 'MixerTest'"
$ad = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$ad.Fill($dt) | Out-Null

Write-Host "`nDatabase Verification:" -ForegroundColor Cyan
$dt | Format-Table -AutoSize

$resolvedStatus = $dt.Rows[0]["Status"].ToString()
$restoreTimeValue = $dt.Rows[0]["RestoreTime"]

if ($resolvedStatus -eq "Resolved" -and $restoreTimeValue -ne [DBNull]::Value) {
    Write-Host "SUCCESS: Alarm status updated to 'Resolved' and RestoreTime is populated!" -ForegroundColor Green
} else {
    Write-Host "FAILURE: Status is '$resolvedStatus' (Expected: Resolved) or RestoreTime is null!" -ForegroundColor Red
}

$conn.Close()
