Add-Type -Path "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\HinoTools.Alarm\bin\Debug\MySql.Data.dll"
$connString = "Server=localhost;Uid=root;Pwd=101101;Database=scada"

$conn = New-Object MySql.Data.MySqlClient.MySqlConnection($connString)
$conn.Open()

Write-Host "--- ALL TABLES IN SCADA ---"
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand("SHOW TABLES", $conn)
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt)
$dt | Format-Table -AutoSize

Write-Host "--- ROW COUNT IN EACH TABLE ---"
foreach ($row in $dt.Rows) {
    $tableName = $row[0]
    $query = "SELECT COUNT(*) FROM " + $tableName
    $countCmd = New-Object MySql.Data.MySqlClient.MySqlCommand($query, $conn)
    $count = $countCmd.ExecuteScalar()
    $message = "{0}: {1} rows" -f $tableName, $count
    Write-Host $message
}

$conn.Close()
