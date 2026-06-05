# Kế hoạch triển khai xử lý Webhook tạo Batch và lưu BOM vào Run Info

Tài liệu này mô tả chi tiết phương án thiết kế và triển khai xử lý payload gửi về từ Base qua cổng Webhook, tạo Batch đồng bộ và xử lý bất đồng bộ các Run cùng dữ liệu BOM vào bảng `run_info`.

---

## 1. Nghiệp vụ thực tế & Thiết kế Giải pháp

### A. Phân tách và Xử lý Payload Webhook:
- **Định dạng dữ liệu**: Webhook nhận dữ liệu dạng `application/x-www-form-urlencoded` từ Base.
- **Giải mã tham số**:
  - `custom_ngay_san_xuat` (hoặc `ngay_san_xuat`): Ngày sản xuất định dạng `dd/MM/yyyy` (Ví dụ: `04/06/2026`).
  - `custom_thiet_bi_su_dung` (hoặc `thiet_bi_su_dung`): Tên thiết bị (Ví dụ: `TX01`). Mặc định là `TX01`.
  - `custom_so_me_san_xuat` (hoặc `so_me_san_xuat`): Số lượng mẻ (Ví dụ: `2`). Mặc định là `1`.
  - **Thông tin sản phẩm & mẻ cha**:
    - `custom_ten_hang_hoa` (hoặc `ten_hang_hoa`): Tên hàng hóa.
    - `custom_ma_dinh_danh` (hoặc `ma_dinh_danh`): Mã định danh.
    - `custom_nha_san_xuat` (hoặc `nha_san_xuat`): Nhà sản xuất.
    - `custom_khoi_luong_muc_tieu` (hoặc `khoi_luong_muc_tieu`): Khối lượng mục tiêu.
    - `custom_cong_thuc` (hoặc `cong_thuc`): Công thức sản xuất.
  - BOM keys định dạng `custom_thong_tin_bom_san_xuat_[suffix]` (với `suffix` là `'a'` cho Mẻ 1, `'b'` cho Mẻ 2, `'c'` cho Mẻ 3, v.v.).

### B. Quy tắc Đặt tên & Sequence (STT):
- Cần thêm các cột metadata (`date`, `product_name`, `product_code`, `manufacturer`, `target_weight`, `formula`) vào bảng `batches`.
- Tìm STT tiếp theo cho ngày chạy bằng cách đếm các Batch của thiết bị có cùng ngày chạy (`date = @production_date`).
- **Batch Name**: `[Device]-[yyyyMMdd]-[STT:D2]` (Ví dụ: `TX01-20260604-01`).
- **Run Name**: `[BatchName]-Me[RunNumber:D2]` (Ví dụ: `TX01-20260604-01-Me01`).

### C. Cơ chế Xử lý Bất đồng bộ:
1. **Đồng bộ (Synchronous)**:
   - Nhận request, ghi log raw payload vào bảng `webhook_logs` với trạng thái `Pending`.
   - Parse ngày sản xuất, thiết bị, số mẻ và các thông tin mẻ cha.
   - Lock DB, truy vấn STT tiếp theo cho ngày sản xuất đó.
   - Thêm bản ghi Batch mới vào bảng `batches` (lưu cả ngày sản xuất, tên hàng hóa, mã định danh, nhà sản xuất, khối lượng mục tiêu, công thức).
   - Trả về phản hồi `200 OK` chứa `batch_name` vừa tạo.
2. **Bất đồng bộ (Asynchronous - chạy ngầm)**:
   - Kích hoạt một `Task.Run` chạy ngầm.
   - Tạo các bản ghi Run tương ứng trong bảng `runs`.
   - Với mỗi mẻ, tìm key BOM tương ứng (ví dụ Mẻ 1 ứng với suffix `a` -> key `custom_thong_tin_bom_san_xuat_a`).
   - Nếu có dữ liệu BOM:
     - Giải mã Base64 sang chuỗi JSON.
     - Deserialize chuỗi JSON sang mảng các dòng BOM: `[["code", "material_code", "quantity", "value", "unit", "batch_no"]]`.
     - Lưu từng dòng BOM vào bảng `run_info`.
   - Nếu thành công toàn bộ: cập nhật trạng thái dòng webhook trong `webhook_logs` thành `Completed`.
   - Nếu xảy ra lỗi: cập nhật trạng thái dòng webhook thành `Failed` và ghi chi tiết biệt lệ vào cột `error_message`.

---

## 2. Thiết kế Cơ sở Dữ liệu

### A. Cập nhật bảng `batches`:
Tự động kiểm tra và thêm các cột sau nếu chưa tồn tại:
- `date` (DATE NULL): Ngày chạy thực tế
- `product_name` (VARCHAR(255) NULL): Tên hàng hóa
- `product_code` (VARCHAR(100) NULL): Mã định danh
- `manufacturer` (VARCHAR(255) NULL): Nhà sản xuất
- `target_weight` (DOUBLE NOT NULL DEFAULT 0): Khối lượng mục tiêu
- `formula` (VARCHAR(100) NULL): Công thức sản xuất

```sql
ALTER TABLE `batches` 
  ADD COLUMN `date` DATE NULL AFTER `device_name`,
  ADD COLUMN `product_name` VARCHAR(255) NULL AFTER `date`,
  ADD COLUMN `product_code` VARCHAR(100) NULL AFTER `product_name`,
  ADD COLUMN `manufacturer` VARCHAR(255) NULL AFTER `product_code`,
  ADD COLUMN `target_weight` DOUBLE NOT NULL DEFAULT 0 AFTER `manufacturer`,
  ADD COLUMN `formula` VARCHAR(100) NULL AFTER `target_weight`;
```

### B. Tạo mới bảng `run_info`:
Bảng này lưu thông tin định mức nguyên vật liệu (BOM) chi tiết cho từng mẻ.
```sql
CREATE TABLE IF NOT EXISTS `run_info` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `run_id` INT NOT NULL,
  `code` VARCHAR(100) NULL,
  `material_code` VARCHAR(100) NULL,
  `quantity` DOUBLE NOT NULL DEFAULT 0,
  `value` VARCHAR(100) NULL,
  `unit` VARCHAR(50) NULL,
  `batch_no` VARCHAR(100) NULL,
  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (`run_id`) REFERENCES `runs`(`id`) ON DELETE CASCADE,
  INDEX `idx_run_info_run` (`run_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

---

## 3. Kế hoạch thay đổi Code trong `WebhookHttpServer.cs`

- **Thêm helper `ParseFormUrlEncoded`**: Phân tích cú pháp form-urlencoded.
- **Nâng cấp `EnsureWebhookTableExists`**:
  - Tự động chạy lệnh SQL tạo bảng `run_info`.
  - Tự động chạy lệnh kiểm tra và thêm các cột `date`, `product_name`, `product_code`, `manufacturer`, `target_weight`, `formula` vào bảng `batches`.
- **Cập nhật `ProcessRequest`**:
  - Đọc và parse payload form-urlencoded.
  - Trích xuất thông tin mẻ cha và ngày sản xuất.
  - Thực hiện tạo Batch đồng bộ trong block `lock (dbLock)` và trả về mã Batch.
  - Đóng gói logic tạo Runs & BOM vào hàm chạy ngầm `ProcessWebhookAsync`.
- **Thêm hàm chạy ngầm `ProcessWebhookAsync`**:
  - Chạy bất đồng bộ, thực hiện loop tạo Run và parse Base64 BOM.
  - Deserialize JSON bằng `JavaScriptSerializer` sang `List<List<object>>`.
  - Insert dữ liệu BOM vào bảng `run_info`.
  - Xử lý và bắt ngoại lệ để cập nhật trạng thái `Failed`/`Completed` vào `webhook_logs`.

---

## 4. Kế hoạch Xác minh (Verification Plan)

### A. Xác minh Biên dịch
- Chạy MSBuild để biên dịch solution:
  `C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe`

### B. Xác minh Thủ công (Manual Test)
Sử dụng cURL gửi request kiểm thử với dữ liệu thực tế từ Base:
```bash
curl -X POST "http://localhost:5600/api/webhook?token=wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "custom_ngay_san_xuat=04%2F06%2F2026&custom_thiet_bi_su_dung=TX01&custom_so_me_san_xuat=2&custom_ten_hang_hoa=TEST+AF&custom_ma_dinh_danh=ABCYA123&custom_nha_san_xuat=AFCHEM&custom_khoi_luong_muc_tieu=0.82&custom_cong_thuc=AFCx12223&custom_thong_tin_bom_san_xuat_a=W1siQUJDIiwiQUYwMSIsIjAuOCIsIjExMTAiLCJLRyIsIjEyMzIxNTEyMyJdXQ%3D%3D&custom_thong_tin_bom_san_xuat_b=W1siQUNDVyIsIkFGMDIiLCIwLjgiLCIxMjMiLCJLRyIsIjEyMzEyNTEyMyJdXQ%3D%3D"
```

**Kết quả mong đợi**:
1. Phản hồi HTTP Status `200 OK` nhận được ngay lập tức với JSON chứa tên Batch dạng `TX01-20260604-01`.
2. Kiểm tra database bảng `batches`:
   - Có bản ghi mới với `name = 'TX01-20260604-01'`, `date = '2026-06-04'`, `total_runs = 2`, `product_name = 'TEST AF'`, `product_code = 'ABCYA123'`, `manufacturer = 'AFCHEM'`, `target_weight = 0.82`, `formula = 'AFCx12223'`.
3. Kiểm tra database bảng `runs`:
   - Có 2 bản ghi mới: `TX01-20260604-01-Me01` và `TX01-20260604-01-Me02`.
4. Kiểm tra database bảng `run_info`:
   - BOM của Run 1 (`Me01`) chứa các trường ứng với `[["ABC","AF01",0.8,"1110","KG","123215123"]]`.
   - BOM của Run 2 (`Me02`) chứa các trường ứng với `[["ACCW","AF02",0.8,"123","KG","123125123"]]`.
5. Kiểm tra bảng `webhook_logs`:
   - Bản ghi webhook tương ứng chuyển trạng thái thành `Completed` và `error_message` là `NULL`.
