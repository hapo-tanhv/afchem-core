# Kế hoạch lưu trữ các thanh ghi cài đặt (Setpoints) vào cơ sở dữ liệu

Tài liệu này mô tả chi tiết phương án thiết kế và triển khai cơ chế lưu trữ 8 thanh ghi cài đặt thời gian từ PLC vào bảng `runs` của cơ sở dữ liệu khi bắt đầu mẻ và cập nhật bất kỳ sự thay đổi tham số nào từ người vận hành trong quá trình chạy mẻ (Mid-run changes).

---

## 1. Nghiệp vụ thực tế & Thiết kế Giải pháp

### A. Danh sách các thanh ghi cài đặt (Setpoints):
Hệ thống sẽ giám sát và ghi nhận 8 thanh ghi thời gian cài đặt sau:
1. `ThoiGianCaiDatCapLieu` -> Cài đặt thời gian cấp liệu
2. `ThoiGianCaiDatTron1` -> Cài đặt thời gian trộn 1
3. `ThoiGianCaiDatXaDay` -> Cài đặt thời gian xả đáy
4. `ThoiGianCaiDatRungXaDay` -> Cài đặt thời gian rung xả đáy
5. `ThoiGianCaiDatHutXaDayThem` -> Cài đặt thời gian hút xả đáy thêm
6. `ThoiGianCaiDatTron2` -> Cài đặt thời gian trộn 2
7. `ThoiGianCaiDatXaHang` -> Cài đặt thời gian xả hàng
8. `ThoiGianCaiDatRungXaHang` -> Cài đặt thời gian rung xả hàng

### B. Thiết kế Lưu trữ & Tối ưu hóa:
- **Nơi lưu trữ**: Lưu trữ trực tiếp tại bảng `runs` (Mẻ con) vì mỗi mẻ chạy có thể có cấu hình cài đặt riêng từ người vận hành.
- **Thời điểm ghi nhận**:
  - Ghi nhận giá trị cài đặt ban đầu khi mẻ chạy chuyển sang trạng thái hoạt động (`ThoiGianCapLieu > 0` và máy bắt đầu chạy).
  - Sử dụng cơ chế bộ đệm trong bộ nhớ (`lastSetpoints` cache) để lưu lại các giá trị cài đặt cuối cùng được đọc từ driver.
  - Trong mỗi chu kỳ quét dữ liệu (polling), nếu phát hiện bất kỳ thanh ghi cài đặt nào thay đổi giá trị so với bộ đệm (do người vận hành thay đổi thông số trên HMI giữa chừng) $\rightarrow$ tự động thực hiện câu lệnh `UPDATE` cập nhật vào dòng `runs` tương ứng trong DB và cập nhật lại cache.

---

## 2. Thiết kế Cơ sở Dữ liệu

### Cập nhật bảng `runs`:
Bổ sung 8 cột mới để lưu trữ giá trị cài đặt của từng mẻ con:
```sql
ALTER TABLE `runs` 
  ADD COLUMN `sp_thoi_gian_cap_lieu` INT NOT NULL DEFAULT 0 AFTER `status`,
  ADD COLUMN `sp_thoi_gian_tron1` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_cap_lieu`,
  ADD COLUMN `sp_thoi_gian_xa_day` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_tron1`,
  ADD COLUMN `sp_thoi_gian_rung_xa_day` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_xa_day`,
  ADD COLUMN `sp_thoi_gian_hut_xa_day_them` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_rung_xa_day`,
  ADD COLUMN `sp_thoi_gian_tron2` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_hut_xa_day_them`,
  ADD COLUMN `sp_thoi_gian_xa_hang` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_tron2`,
  ADD COLUMN `sp_thoi_gian_rung_xa_hang` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_xa_hang`;
```

---

## 3. Kế hoạch thay đổi Code trong `AlarmReportLogger.cs`

- **Khai báo bộ đệm Setpoints**:
  ```csharp
  private double[] lastSetpoints = new double[8] { -1, -1, -1, -1, -1, -1, -1, -1 };
  ```
- **Nâng cấp hàm `EnsureBatchesTableExists`**:
  - Thêm cơ chế tự động kiểm tra sự tồn tại và thêm các cột cài đặt `sp_...` tương ứng vào bảng `runs` bằng các lệnh `SHOW COLUMNS` và `ALTER TABLE`.
- **Cập nhật `ResetFlags`**:
  - Reset bộ đệm `lastSetpoints` về `-1` mỗi khi bắt đầu hoặc chuyển đổi sang mẻ mới để kích hoạt việc cập nhật dữ liệu cài đặt ban đầu vào cơ sở dữ liệu.
- **Thêm hàm `CheckAndUpdateSetpoints`**:
  - Đọc 8 giá trị cài đặt từ PLC qua hàm `GetSystemTagValue`.
  - So sánh với cache `lastSetpoints`. Nếu có sự thay đổi (hoặc mẻ vừa khởi chạy) $\rightarrow$ update vào DB và lưu lại cache.
- **Cập nhật hàm `PollAndLog`**:
  - Gọi hàm `CheckAndUpdateSetpoints()` sau khi hoàn tất đồng bộ mẻ hoạt động hiện tại để đảm bảo dữ liệu cài đặt luôn khớp với trạng thái thực tế trên PLC.

---

## 4. Kế hoạch Xác minh (Verification Plan)

### A. Xác minh Biên dịch
- Biên dịch solution bằng MSBuild 2019:
  `C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe`

### B. Xác minh Thủ công (Manual Verification)
1. **Khởi chạy lại dịch vụ**: Chạy ứng dụng `WindowsFormsApp1.exe` để kích hoạt migration tạo thêm 8 cột mới trong bảng `runs`.
2. **Kiểm tra mẻ bắt đầu**:
   - Thiết lập các giá trị cài đặt trên PLC giả lập (ví dụ: `ThoiGianCaiDatCapLieu = 50`, `ThoiGianCaiDatTron1 = 120`...).
   - Đưa máy về trạng thái `Run = 1`, `Stop = 0`, và tăng `ThoiGianCapLieu = 10` để khởi động mẻ mới.
   - Kiểm tra DB bảng `runs` xem dòng mẻ mới tạo có chứa đúng giá trị cài đặt tương ứng không.
3. **Kiểm tra thay đổi thông số giữa chừng (Mid-run change)**:
   - Trong lúc mẻ đang chạy, thay đổi giá trị `ThoiGianCaiDatTron1` từ `120` thành `150`.
   - Chờ chu kỳ Polling tiếp theo, kiểm tra DB xem giá trị cột `sp_thoi_gian_tron1` đã được cập nhật thành `150` chưa.
