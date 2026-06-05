# Walkthrough - Lưu trữ và Cập nhật thanh ghi Cài đặt (Setpoints)

Tài liệu này mô tả chi tiết kết quả triển khai và hướng dẫn kiểm thử cho tính năng **Setpoints Tracking (Theo dõi & Ghi nhận giá trị cài đặt thời gian)** từ PLC/HMI vào bảng `runs` của cơ sở dữ liệu.

---

## 1. Các thành phần mã nguồn đã thay đổi

### A. [AlarmReportLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs)
- **Tự động di chuyển DB (Auto-Migration)**:
  - Cập nhật hàm `EnsureBatchesTableExists()` để tự động kiểm tra và thêm 8 cột giá trị cài đặt (`sp_thoi_gian_cap_lieu`, `sp_thoi_gian_tron1`, `sp_thoi_gian_xa_day`, `sp_thoi_gian_rung_xa_day`, `sp_thoi_gian_hut_xa_day_them`, `sp_thoi_gian_tron2`, `sp_thoi_gian_xa_hang`, `sp_thoi_gian_rung_xa_hang`) vào bảng `runs` nếu chúng chưa tồn tại.
- **Bộ nhớ đệm Setpoints (`lastSetpoints` cache)**:
  - Khai báo mảng đệm gồm 8 phần tử: `private double[] lastSetpoints = new double[8] { -1, -1, -1, -1, -1, -1, -1, -1 }` để lưu giữ giá trị setpoint của lần quét trước.
  - Đồng bộ việc reset bộ đệm về `-1` trong hàm `ResetFlags()` mỗi khi có sự kiện chuyển đổi mẻ chạy mới. Điều này kích hoạt việc ghi nhận các giá trị cài đặt ban đầu của mẻ mới vào DB ngay trong chu kỳ quét đầu tiên.
- **Giám sát & Cập nhật động**:
  - Triển khai hàm `CheckAndUpdateSetpoints()` để đọc trực tiếp các tag cài đặt từ PLC (ví dụ: `AFChemTX01.ThoiGianCaiDatCapLieu`).
  - So sánh với các giá trị trong bộ đệm. Nếu phát hiện có sự thay đổi (hoặc bắt đầu mẻ mới), thực hiện câu lệnh `UPDATE` cập nhật cơ sở dữ liệu của dòng `runs` hoạt động hiện tại và nạp lại cache.
  - Tích hợp gọi hàm `CheckAndUpdateSetpoints()` định kỳ trong chu kỳ `PollAndLog()`.

---

## 2. Kết quả Xác minh và Kiểm thử

Chúng ta đã tạo và chạy thử nghiệm bằng tập lệnh [test_setpoints.ps1](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/scratch/test_setpoints.ps1) sử dụng cơ chế Reflection tải trực tiếp DLL và thực thi mã nguồn di chuyển DB của C#. 

Kết quả kiểm thử cho thấy:
1. **Migration thành công**: 8 cột cài đặt đã được thêm thành công vào bảng `runs` trong DB với kiểu dữ liệu `int(11)`, mặc định `0`, và không cho phép `Null`.
2. **Cấu trúc cột trong DB**:
   ```
   sp_thoi_gian_cap_lieu        int(11) NO       0            
   sp_thoi_gian_tron1           int(11) NO       0            
   sp_thoi_gian_xa_day          int(11) NO       0            
   sp_thoi_gian_rung_xa_day     int(11) NO       0            
   sp_thoi_gian_hut_xa_day_them int(11) NO       0            
   sp_thoi_gian_tron2           int(11) NO       0            
   sp_thoi_gian_xa_hang         int(11) NO       0            
   sp_thoi_gian_rung_xa_hang    int(11) NO       0
   ```
