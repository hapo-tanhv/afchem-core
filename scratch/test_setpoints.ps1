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

# Instantiate AlarmReportLogger and trigger migration via Reflection
Write-Host "Instantiating AlarmReportLogger..." -ForegroundColor Cyan
$logger = New-Object HinoTools.Data.Log.AlarmReportLogger
$logger.ServerName = "localhost"
$logger.DatabaseName = "scada"
$logger.UserID = "root"
$logger.Password = "101101"

Write-Host "Invoking EnsureBatchesTableExists via Reflection..." -ForegroundColor Cyan
# Initialize the private dataAccess field in the logger
$dataAccess = New-Object HinoTools.Data.Database.DataAccess
$logger.GetType().GetField("dataAccess", [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance).SetValue($logger, $dataAccess)

$method = $logger.GetType().GetMethod("EnsureBatchesTableExists", [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance)
$method.Invoke($logger, $null)

Write-Host "Migration execution completed!" -ForegroundColor Green

# Query and display columns of runs table starting with sp_
Write-Host "`nChecking runs table columns starting with sp_:" -ForegroundColor Yellow
$conn.Open()
$cmd = New-Object MySql.Data.MySqlClient.MySqlCommand("SHOW COLUMNS FROM runs LIKE 'sp_%'", $conn)
$ad = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$ad.Fill($dt)
$dt | Format-Table -AutoSize
$conn.Close()
