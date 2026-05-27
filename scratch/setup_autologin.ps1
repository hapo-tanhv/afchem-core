# setup_autologin.ps1 - Tự động cấu hình Windows Đăng nhập tự động qua Registry
# Chạy script này bằng quyền Administrator. Bạn cần nhập Username và Password của tài khoản Windows trạm SCADA.

$Username = Read-Host "Nhập Username đăng nhập Windows (Ví dụ: Administrator hoặc tên User hiện tại)"
$Password = Read-Host "Nhập Password đăng nhập Windows (Nếu không có mật khẩu, hãy để trống và nhấn Enter)"

$RegistryPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"

try {
    # 1. Bật tính năng đăng nhập tự động
    Set-ItemProperty -Path $RegistryPath -Name "AutoAdminLogon" -Value "1" -Type String
    
    # 2. Thiết lập Username
    Set-ItemProperty -Path $RegistryPath -Name "DefaultUserName" -Value $Username -Type String
    
    # 3. Thiết lập Password
    if ([string]::IsNullOrEmpty($Password)) {
        # Nếu không có mật khẩu, xóa giá trị DefaultPassword nếu có
        Remove-ItemProperty -Path $RegistryPath -Name "DefaultPassword" -ErrorAction SilentlyContinue
    } else {
        Set-ItemProperty -Path $RegistryPath -Name "DefaultPassword" -Value $Password -Type String
    }

    # 4. Tắt tùy chọn yêu cầu Windows Hello nếu là tài khoản Local (dành cho Windows 10/11)
    $DevicePath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device"
    if (Test-Path $DevicePath) {
        Set-ItemProperty -Path $DevicePath -Name "DevicePasswordLessBuildVersion" -Value 0 -Type DWord -ErrorAction SilentlyContinue
    }

    Write-Host "`nĐã cấu hình tự động đăng nhập Windows thành công!" -ForegroundColor Green
    Write-Host "Kể từ lần khởi động sau, Windows sẽ tự động đăng nhập vào tài khoản '$Username'." -ForegroundColor Green
    Write-Host "Lưu ý: Hãy chắc chắn kéo Shortcut ứng dụng WindowsFormsApp1 vào thư mục Startup (shell:startup)." -ForegroundColor Yellow
}
catch {
    Write-Error "Lỗi cấu hình Registry: $_"
}
