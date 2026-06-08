Add-Type -Path "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\HinoTools.Alarm\bin\Debug\MySql.Data.dll"
$connString = "Server=localhost;Uid=root;Pwd=101101;Database=scada"

try {
    $conn = New-Object MySql.Data.MySqlClient.MySqlConnection($connString)
    $conn.Open()
} catch {
    Write-Host "MySQL Connection FAILED: $_" -ForegroundColor Red
    exit
}

Write-Host "--- LATEST WEBHOOK LOGS ---" -ForegroundColor Cyan
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand("SELECT id, received_at, status, error_message FROM webhook_logs ORDER BY id DESC LIMIT 5", $conn)
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt)
$dt | Format-Table -AutoSize

Write-Host "--- LATEST BATCHES (TX02) ---" -ForegroundColor Cyan
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand("SELECT id, name, device_name, date, product_name, status, total_runs FROM batches WHERE device_name = 'TX02' ORDER BY id DESC LIMIT 5", $conn)
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt)
$dt | Format-Table -AutoSize

Write-Host "--- LATEST RUNS (TX02) ---" -ForegroundColor Cyan
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand("SELECT r.id, r.batch_id, b.name as batch_name, r.run_number, r.name, r.status FROM runs r JOIN batches b ON r.batch_id = b.id WHERE b.device_name = 'TX02' ORDER BY r.id DESC LIMIT 5", $conn)
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt)
$dt | Format-Table -AutoSize

Write-Host "--- LATEST RUN INFO / BOM ---" -ForegroundColor Cyan
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand("SELECT ri.id, ri.run_id, r.name as run_name, ri.code, ri.material_code, ri.quantity, ri.unit, ri.batch_no FROM run_info ri JOIN runs r ON ri.run_id = r.id WHERE r.name LIKE '%TX02%' ORDER BY ri.id DESC LIMIT 15", $conn)
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt)
$dt | Format-Table -AutoSize

$conn.Close()
