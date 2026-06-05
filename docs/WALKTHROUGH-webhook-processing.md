# Walkthrough - Xử lý Webhook, Tạo Batch và Nhập BOM vào Run Info

Tài liệu này tổng hợp chi tiết kết quả triển khai và hướng dẫn kiểm thử cho tính năng **Webhook Processing (Xử lý Webhook Lệnh Sản Xuất)** trong hệ thống `HinoTools.Alarm`.

---

## 1. Các thành phần mã nguồn đã thay đổi

### A. [WebhookHttpServer.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Http/WebhookHttpServer.cs)
- **Tự động di chuyển DB (Auto-Migration)**:
  - Nâng cấp hàm `EnsureWebhookTableExists` để tự động kiểm tra và thêm 6 cột metadata vào bảng `batches`: `date`, `product_name`, `product_code`, `manufacturer`, `target_weight`, `formula`.
  - Tự động tạo bảng `run_info` lưu trữ định mức nguyên vật liệu (BOM) với khóa ngoại `run_id` liên kết tới bảng `runs.id` (`ON DELETE CASCADE`).
- **Phân tách tham số (Form Payload Parser)**:
  - Triển khai helper `ParseFormUrlEncoded` để tự động giải mã dữ liệu định dạng `application/x-www-form-urlencoded` được gửi từ hệ thống Base.
- **Tạo Batch đồng bộ**:
  - Trích xuất thông tin ngày sản xuất, thiết bị, số lượng mẻ con, và thông tin hàng hóa.
  - Sử dụng block `lock (dbLock)` để thực hiện truy vấn số thứ tự (STT) tiếp theo cho ngày chạy đó bằng câu lệnh:
    ```sql
    SELECT `name` FROM `batches` WHERE `name` LIKE @name_pattern ORDER BY `id` DESC LIMIT 1
    ```
    Cơ chế `LIKE` này giúp tìm chính xác STT tăng dần cho ngày chạy mà không bị trùng lặp với các bản ghi lịch sử có cột `date = NULL`.
  - Chèn mẻ cha (`batches`) vào cơ sở dữ liệu đồng bộ và trả về kết quả `200 OK` chứa mã Batch vừa tạo (ví dụ `TX01-20260604-02`) ngay lập tức cho client.
- **Xử lý bất đồng bộ (Asynchronous Background Task)**:
  - Khởi chạy một tiến trình ngầm `ProcessWebhookAsync` thông qua `Task.Run` để thực hiện:
    - Tạo các mẻ con (`runs`) tương ứng (ví dụ `TX01-20260604-02-Me01`, `TX01-20260604-02-Me02`).
    - Tìm kiếm key BOM tương ứng theo quy tắc hậu tố chữ cái tăng dần: Mẻ 1 ứng với `'a'` (`custom_thong_tin_bom_san_xuat_a`), Mẻ 2 ứng với `'b'`, v.v.
    - Giải mã Base64 dữ liệu BOM, chuyển sang UTF-8 JSON.
    - Sử dụng `JavaScriptSerializer` để deserialize mảng BOM sang cấu trúc dữ liệu `List<List<object>>`.
    - Duyệt qua từng dòng nguyên vật liệu trong mảng BOM và lưu vào bảng `run_info`.
    - Cập nhật trạng thái của dòng log webhook tương ứng thành `Completed` (hoặc `Failed` kèm theo biệt lệ cụ thể trong cột `error_message` nếu có lỗi xảy ra).

---

## 2. Kết quả Xác minh và Kiểm thử thành công

Chúng ta đã tạo tập lệnh kiểm thử tự động tại [test_webhook.ps1](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/scratch/test_webhook.ps1). Kết quả kiểm thử cho thấy:

1. **Phản hồi từ API Webhook (Đồng bộ)**:
   ```json
   {
       "success": true,
       "message": "Batch created successfully",
       "batch_name": "TX01-20260604-02"
   }
   ```
2. **Dữ liệu mẻ cha (`batches`)**:
   - Tên Batch: `TX01-20260604-02` (STT được tính toán tăng tiến chính xác từ `01` lên `02` dựa trên tên mẻ).
   - Ngày chạy (`date`): `2026-06-04`.
   - Các trường metadata được điền đầy đủ: `product_name = 'TEST AF'`, `product_code = 'ABCYA123'`, `manufacturer = 'AFCHEM'`, `target_weight = 0.82`, `formula = 'AFCx12223'`.
3. **Dữ liệu mẻ con (`runs`)**:
   - Hai mẻ con được tạo: `TX01-20260604-02-Me01` và `TX01-20260604-02-Me02`.
4. **Dữ liệu BOM (`run_info`)**:
   - Mẻ 1 (`Me01`) import thành công dòng nguyên vật liệu `ABC` / `AF01` / `0.8` / `1110` / `KG`.
   - Mẻ 2 (`Me02`) import thành công dòng nguyên vật liệu `ACCW` / `AF02` / `0.8` / `123` / `KG`.
5. **Dòng log Webhook (`webhook_logs`)**:
   - Chuyển trạng thái thành `Completed` thành công.
