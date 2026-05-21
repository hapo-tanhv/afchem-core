Add-Type -Path "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\HinoTools.Alarm\bin\Debug\MySql.Data.dll"
$connString = "Server=localhost;Uid=root;Pwd=101101;Database=scada"
$conn = New-Object MySql.Data.MySqlClient.MySqlConnection($connString)
$conn.Open()

# Query all alarmsettings
Write-Host "--- Query: alarmsettings ---"
$query = "SELECT * FROM alarmsettings"
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand($query, $conn)
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt)
$dt | Format-Table -AutoSize

$conn.Close()
