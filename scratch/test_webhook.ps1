Add-Type -Path "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\HinoTools.Alarm\bin\Debug\MySql.Data.dll"
Add-Type -Path "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\HinoTools.Data\bin\Debug\HinoTools.Data.dll"

$connString = "Server=localhost;Uid=root;Pwd=101101;Database=scada"

# Check if MySQL is running
try {
    $conn = New-Object MySql.Data.MySqlClient.MySqlConnection($connString)
    $conn.Open()
    $conn.Close()
    Write-Host "MySQL Connection OK!" -ForegroundColor Green
} catch {
    Write-Host "MySQL Connection FAILED: $_" -ForegroundColor Red
    exit
}

# Start the WebhookHttpServer
Write-Host "Starting WebhookHttpServer on port 5600..." -ForegroundColor Cyan
$server = New-Object HinoTools.Data.Http.WebhookHttpServer($connString, 5600, "wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b")
$server.Start()

# Get today's date formatted as dd/MM/yyyy and URL encode it (replace '/' with '%2F')
$todayStr = (Get-Date).ToString("dd/MM/yyyy")
$todayEncoded = $todayStr.Replace("/", "%2F")

# Prepare payload
$body = "custom_ngay_san_xuat=$todayEncoded&custom_thiet_bi_su_dung=TX01&custom_so_me_san_xuat=2&custom_ten_hang_hoa=TEST+AF&custom_ma_dinh_danh=ABCYA123&custom_nha_san_xuat=AFCHEM&custom_khoi_luong_muc_tieu=0.82&custom_cong_thuc=AFCx12223&custom_thong_tin_bom_san_xuat_a=W1siQUJDIiwiQUYwMSIsIjAuOCIsIjExMTAiLCJLRyIsIjEyMzIxNTEyMyJdXQ%3D%3D&custom_thong_tin_bom_san_xuat_b=W1siQUNDVyIsIkFGMDIiLCIwLjgiLCIxMjMiLCJLRyIsIjEyMzEyNTEyMyJdXQ%3D%3D"


# Send webhook request
Write-Host "Sending POST request to webhook endpoint..." -ForegroundColor Cyan
$response = Invoke-RestMethod -Uri "http://127.0.0.1:5600/api/webhook?token=wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded"

Write-Host "Response received:" -ForegroundColor Green
$response | ConvertTo-Json

Write-Host "Waiting 3 seconds for background task to complete..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

# Stop server
Write-Host "Stopping WebhookHttpServer..." -ForegroundColor Cyan
$server.Stop()

# Query and display database state
$conn.Open()

Write-Host "`n--- batches ---" -ForegroundColor Green
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand("SELECT id, name, device_name, date, product_name, product_code, manufacturer, target_weight, formula, status, total_runs FROM batches ORDER BY id DESC LIMIT 1", $conn)
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt)
$dt | Format-List

Write-Host "`n--- runs ---" -ForegroundColor Green
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand("SELECT id, batch_id, run_number, name, status FROM runs ORDER BY id DESC LIMIT 2", $conn)
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt)
$dt | Format-Table -AutoSize

Write-Host "`n--- run_info ---" -ForegroundColor Green
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand("SELECT * FROM run_info ORDER BY id DESC LIMIT 4", $conn)
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt)
$dt | Format-Table -AutoSize

Write-Host "`n--- webhook_logs ---" -ForegroundColor Green
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand("SELECT id, received_at, status, error_message FROM webhook_logs ORDER BY id DESC LIMIT 1", $conn)
$adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt)
$dt | Format-List

$conn.Close()
