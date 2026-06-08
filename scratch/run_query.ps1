Add-Type -Path "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\HinoTools.Alarm\bin\Debug\MySql.Data.dll"
$conn = New-Object MySql.Data.MySqlClient.MySqlConnection('Server=localhost;Uid=root;Pwd=101101;Database=scada')
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SHOW CREATE TABLE realtime_alarms"
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt) | Out-Null
Write-Host $dt.Rows[0][1]
$conn.Close()
