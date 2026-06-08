Add-Type -Path "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\HinoTools.Alarm\bin\Debug\MySql.Data.dll"
$connString = "Server=localhost;Uid=root;Pwd=101101;Database=scada"

$conn = New-Object MySql.Data.MySqlClient.MySqlConnection($connString)
$conn.Open()

Write-Host "--- alarmsettings ---" -ForegroundColor Cyan
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand("SELECT * FROM alarmsettings", $conn)
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt)
$dt | Format-Table -AutoSize

Write-Host "--- eventsettings ---" -ForegroundColor Cyan
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand("SELECT * FROM eventsettings", $conn)
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt)
$dt | Format-Table -AutoSize

$conn.Close()
