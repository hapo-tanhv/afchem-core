# revert_task.ps1 - Gỡ bỏ Scheduled Task HinoTools_Alarm_Service
# Chạy script này bằng quyền Administrator để dọn dẹp Task Scheduler

try {
    $TaskName = "HinoTools_Alarm_Service"
    $TaskExists = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue

    if ($TaskExists) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Host "Đã gỡ bỏ Scheduled Task '$TaskName' thành công!" -ForegroundColor Green
    } else {
        Write-Host "Scheduled Task '$TaskName' không tồn tại trên hệ thống." -ForegroundColor Yellow
    }
}
catch {
    Write-Error "Lỗi khi gỡ bỏ Scheduled Task: $_"
}
