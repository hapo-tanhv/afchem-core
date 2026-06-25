# Hướng dẫn Tích hợp API & Cơ sở Dữ liệu (Mới nhất)

Tài liệu này tổng hợp đặc tả cấu trúc cơ sở dữ liệu và đặc tả Webhook API mới nhất liên quan đến **Batch**, **Run (Mẻ con)**, và **Run Info (BOM)** để hỗ trợ lập trình viên tích hợp hệ thống Web/Dashboard.

---

## 1. Cấu trúc Cơ sở Dữ liệu MySQL (Database Schema)

### A. Bảng `batches` (Lô sản xuất / Mẻ cha)
Lưu thông tin tổng quan của đợt chạy sản xuất trong ngày.

| Tên cột | Kiểu dữ liệu | Đặc điểm | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | `INT` | PRIMARY KEY, AUTO_INCREMENT | Mã định danh tự tăng |
| `name` | `VARCHAR(100)` | UNIQUE, NOT NULL | Tên Batch dạng `[ThiếtBị]-[yyyyMMdd]-[STT:D2]` |
| `device_name` | `VARCHAR(100)` | NOT NULL | Tên thiết bị (Ví dụ: `TX01`) |
| `date` | `DATE` | NULL | Ngày sản xuất thực tế (từ Base) |
| `product_name` | `VARCHAR(255)` | NULL | Tên hàng hóa (`ten_hang_hoa`) |
| `product_code` | `VARCHAR(100)` | NULL | Mã định danh (`ma_dinh_danh`) |
| `manufacturer` | `VARCHAR(255)` | NULL | Nhà sản xuất (`nha_san_xuat`) |
| `target_weight` | `DOUBLE` | DEFAULT 0 | Khối lượng mục tiêu (`khoi_luong_muc_tieu`) |
| `formula` | `VARCHAR(100)` | NULL | Công thức (`cong_thuc`) |
| `status` | `VARCHAR(50)` | DEFAULT 'Pending' | Trạng thái (`Pending`, `Active`, `Completed`, `Error`) |
| `total_runs` | `INT` | DEFAULT 1 | Tổng số mẻ con (`so_me_san_xuat`) |
| `start_time` | `DATETIME` | NULL | Thời gian bắt đầu chạy thực tế |
| `end_time` | `DATETIME` | NULL | Thời gian hoàn thành |
| `created_at` | `TIMESTAMP` | CURRENT_TIMESTAMP | Thời gian tạo trên hệ thống |

### B. Bảng `runs` (Mẻ con)
Lưu chi tiết từng mẻ chạy đơn lẻ thuộc lô sản xuất.

| Tên cột | Kiểu dữ liệu | Đặc điểm | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | `INT` | PRIMARY KEY, AUTO_INCREMENT | Mã định danh mẻ con |
| `batch_id` | `INT` | FOREIGN KEY, INDEX | Liên kết tới `batches.id` (ON DELETE CASCADE) |
| `run_number` | `INT` | NOT NULL | Thứ tự mẻ con (ví dụ: `1`, `2`) |
| `name` | `VARCHAR(150)` | UNIQUE, NOT NULL | Tên mẻ con dạng `[BatchName]-Me[STT:D2]` |
| `status` | `VARCHAR(50)` | DEFAULT 'Pending' | Trạng thái (`Pending`, `Active`, `Completed`, `Error`) |
| `start_time` | `DATETIME` | NULL | Thời gian bắt đầu mẻ |
| `end_time` | `DATETIME` | NULL | Thời gian kết thúc mẻ |
| `created_at` | `TIMESTAMP` | CURRENT_TIMESTAMP | Thời gian tạo mẻ con |

### C. Bảng `run_info` (Chi tiết nguyên vật liệu mẻ con - BOM)
Lưu định mức nguyên vật liệu của từng mẻ con để so sánh với lượng xả thực tế.

| Tên cột | Kiểu dữ liệu | Đặc điểm | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | `INT` | PRIMARY KEY, AUTO_INCREMENT | Mã định danh chi tiết BOM |
| `run_id` | `INT` | FOREIGN KEY, INDEX | Liên kết tới `runs.id` (ON DELETE CASCADE) |
| `code` | `VARCHAR(100)` | NULL | Mã code (Ví dụ: `ABC`, `ACCW`) |
| `material_code` | `VARCHAR(100)` | NULL | Mã nguyên vật liệu (Ví dụ: `AF01`, `AF02`) |
| `quantity` | `DOUBLE` | DEFAULT 0 | Khối lượng định mức (Ví dụ: `0.8`) |
| `value` | `VARCHAR(100)` | NULL | Giá trị thanh ghi (Ví dụ: `"1110"`, `"123"`) |
| `unit` | `VARCHAR(50)` | NULL | Đơn vị tính (Ví dụ: `KG`) |
| `batch_no` | `VARCHAR(100)` | NULL | Số lô NVL đầu vào (Ví dụ: `123215123`) |
| `created_at` | `TIMESTAMP` | CURRENT_TIMESTAMP | Thời gian import dữ liệu |

---

## 2. API Webhook Nhận Dữ Liệu (POST /api/webhook)

Dùng để Base gửi dữ liệu kế hoạch sản xuất cuối ngày.

- **Endpoint**: `POST http://[Server_IP]:5600/api/webhook?token=wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b`
- **Content-Type**: `application/x-www-form-urlencoded`
- **Các tham số Payload**:

| Tham số từ Base | Map vào CSDL | Ví dụ giá trị | Ghi chú |
| :--- | :--- | :--- | :--- |
| `custom_ngay_san_xuat` | `batches.date` | `04/06/2026` | Định dạng bắt buộc: `dd/MM/yyyy` |
| `custom_thiet_bi_su_dung` | `batches.device_name`| `TX01` | Mặc định `TX01` nếu trống |
| `custom_so_me_san_xuat` | `batches.total_runs` | `2` | Mặc định `1` |
| `custom_ten_hang_hoa` | `batches.product_name`| `TEST AF` | Tên sản phẩm |
| `custom_ma_dinh_danh` | `batches.product_code`| `ABCYA123` | Mã sản phẩm |
| `custom_nha_san_xuat` | `batches.manufacturer`| `AFCHEM` | Nhà sản xuất |
| `custom_khoi_luong_muc_tieu`| `batches.target_weight`| `0.82` | Định dạng số thực (double) |
| `custom_cong_thuc` | `batches.formula` | `AFCx12223` | Mã công thức cài đặt |
| `custom_thong_tin_bom_san_xuat_a`| `run_info` (Mẻ 1) | Chuỗi Base64 | BOM mẻ 1. Hậu tố `_a` |
| `custom_thong_tin_bom_san_xuat_b`| `run_info` (Mẻ 2) | Chuỗi Base64 | BOM mẻ 2. Hậu tố `_b` |

> [!NOTE]
> - Quy tắc hậu tố BOM mẻ con: Mẻ 1 ứng với `_a`, Mẻ 2 ứng với `_b`, Mẻ 3 ứng với `_c`, Mẻ 4 ứng với `_d`...
> - Chuỗi Base64 sau khi giải mã sẽ ra mảng JSON dạng: `[["code", "material_code", "quantity", "value", "unit", "batch_no"]]`
>   - *Ví dụ*: `[["ABC","AF01","0.8","1110","KG","123215123"]]`

- **Phản hồi mẫu (Response - Đồng bộ)**:
  ```json
  {
    "success": true,
    "message": "Batch created successfully",
    "batch_name": "TX01-20260605-01"
  }
  ```

---

## 3. Câu lệnh SQL tham khảo cho Dashboard Web

Để hiển thị báo cáo chi tiết của một Batch kèm thông tin BOM mẻ con trên giao diện Web:

```sql
SELECT 
    b.name AS batch_name,
    b.product_name,
    b.product_code,
    b.target_weight,
    r.name AS run_name,
    r.run_number,
    r.status AS run_status,
    ri.material_code,
    ri.quantity AS bom_quantity,
    ri.unit,
    ri.batch_no AS material_lot_no
FROM batches b
JOIN runs r ON r.batch_id = b.id
LEFT JOIN run_info ri ON ri.run_id = r.id
WHERE b.name = 'TX01-20260605-01'
ORDER BY r.run_number ASC, ri.id ASC;
```
