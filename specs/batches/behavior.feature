# language: vi
Tính năng: Hệ thống Quản lý Batch (Lô) - Run (Mẻ) và Tích hợp API nâng cấp
  Là một bên thứ ba (hệ thống MES/ERP) hoặc người quản lý nhà máy
  Tôi muốn hệ thống quản lý Batch (Lô) gồm nhiều Run (Mẻ) sản xuất và mỗi Mẻ gồm 8 công đoạn
  Để tôi có thể theo dõi, vận hành độc lập và hiển thị báo cáo chất lượng chính xác theo từng mẻ của lô

  Bối cảnh:
    Given Hệ thống HinoTools.Alarm đang chạy và kết nối SCADA
    And HTTP API tự lưu trữ đang hoạt động ngầm ở cổng 5500
    And Bảng "batches" đã được bổ sung cột "total_runs"
    And Bảng "runs" đã được tạo mới trong cơ sở dữ liệu MySQL
    And Bảng "alarmreport", "alarmlog" và "realtime_alarms" đã được chèn thêm cột "runId"

  Kịch bản: Tạo lô sản xuất thành công qua API kèm theo nhiều mẻ con
    When Bên thứ ba gửi request POST đến "http://localhost:5500/api/batches/create" với JSON body chứa {"device_name": "TX01", "runs_count": 2}
    Then Hệ thống tạo mới một bản ghi trong bảng "batches" với status là "Pending", total_runs là 2
    And Tự động tạo 2 mẻ con trong bảng "runs" liên kết tới lô này, có run_number lần lượt là 1 và 2
    And Tên mẻ con được sinh tự động dạng "TX01-yyyyMMdd-stt-Run01" và "TX01-yyyyMMdd-stt-Run02" ở trạng thái "Pending"
    And Trả về HTTP Status Code 200 kèm JSON chi tiết về Lô và các Mẻ con vừa tạo

  Kịch bản: Tự động bắt đầu mẻ sản xuất đầu tiên của lô theo cơ chế FIFO
    Given Có một Batch "Pending" gồm 2 mẻ con ở trạng thái "Pending" ứng với thiết bị "TX01" trong DB
    And Hệ thống đang ở trạng thái Idle (chờ mẻ) với "currentCongDoan" = 0
    When Thanh ghi "ThoiGianCapLieu" của thiết bị "AFChemTX01" có giá trị > 0
    Then Hệ thống thực hiện truy vấn DB lấy mẻ "Pending" có ID nhỏ nhất (cũ nhất) của thiết bị "TX01"
    And Cập nhật trạng thái mẻ con đó thành "Active", gán "start_time" là thời điểm hiện tại
    And Cập nhật trạng thái Batch cha tương ứng thành "Active", gán "start_time" là thời điểm hiện tại
    And Lưu "activeRunId" là ID của mẻ con và "activeBatchId" là ID của Batch cha vào bộ nhớ
    And Hệ thống chuyển sang trạng thái ghi dữ liệu mẻ sản xuất

  Kịch bản: Tự động bắt đầu mẻ sản xuất thứ hai của lô (Lô đã hoạt động trước đó)
    Given Có một Batch đang ở trạng thái "Active" có "total_runs" là 2, mẻ 1 đã "Completed", mẻ 2 đang "Pending"
    And Hệ thống đang ở trạng thái Idle với "currentCongDoan" = 0
    When Thanh ghi "ThoiGianCapLieu" của thiết bị "AFChemTX01" có giá trị > 0
    Then Hệ thống truy vấn lấy mẻ con tiếp theo đang "Pending" (Mẻ 2) thuộc Batch "Active" này
    And Cập nhật trạng thái mẻ con 2 thành "Active" và gán "start_time"
    And Giữ nguyên trạng thái Batch cha là "Active"
    And Lưu "activeRunId" của mẻ 2 và "activeBatchId" của Batch vào bộ nhớ

  Kịch bản: Tự động chèn dữ liệu báo cáo và cảnh báo kèm cả batchId và runId
    Given Hệ thống đang có mẻ con "Active" với "activeRunId" = 20 và Batch cha có "activeBatchId" = 10
    When Phát sinh sự kiện ghi log alarmreport hoặc cảnh báo alarmlog xảy ra
    Then Hệ thống thực hiện chèn bản ghi vào bảng tương ứng
    And Giá trị cột "batchId" được gán là 10
    And Giá trị cột "runId" được gán là 20

  Kịch bản: Hoàn thành mẻ 1 nhưng Lô cha chưa hoàn thành (Còn mẻ 2 chưa chạy)
    Given Hệ thống đang ghi log cho mẻ 1 có "activeRunId" = 20 thuộc Batch có "activeBatchId" = 10 (lô có 2 mẻ)
    And "currentCongDoan" = 5
    When Cả hai thanh ghi "ThoiGianXaHang" và "ThoiGianRungXaHang" đều trở về 0 (kết thúc 8 công đoạn)
    Then Hệ thống cập nhật mẻ 1 trong bảng "runs" thành "status" = "Completed" và "end_time" = thời điểm hiện tại
    And Kiểm tra thấy vẫn còn mẻ 2 chưa "Completed" trong Lô này
    And Hệ thống giữ nguyên trạng thái Lô 10 là "Active" và end_time là NULL
    And Giải phóng "activeRunId" và "activeBatchId" về NULL trong bộ nhớ
    And Quay về trạng thái Idle chờ mẻ 2 (currentCongDoan = 0)

  Kịch bản: Hoàn thành mẻ cuối cùng và hoàn thành toàn bộ Lô sản xuất
    Given Hệ thống đang ghi log cho mẻ 2 có "activeRunId" = 21 thuộc Batch có "activeBatchId" = 10
    And Mẻ 1 của Batch này đã hoàn thành trước đó
    And "currentCongDoan" = 5
    When Cả hai thanh ghi "ThoiGianXaHang" và "ThoiGianRungXaHang" đều trở về 0
    Then Hệ thống cập nhật mẻ 2 trong bảng "runs" thành "Completed" và ghi nhận "end_time"
    And Kiểm tra thấy tất cả các mẻ con (Mẻ 1 & Mẻ 2) của Lô 10 đã ở trạng thái "Completed"
    And Cập nhật trạng thái Batch 10 thành "Completed" và gán "end_time" của Batch
    And Giải phóng bộ nhớ "activeRunId" và "activeBatchId" về NULL
    And Quay về trạng thái Idle chờ Lô mới tiếp theo

  Kịch bản: Giao diện lọc tự động chọn mẻ mới nhất khi người dùng chọn Batch
    Given Người dùng mở giao diện giám sát chất lượng sản xuất
    When Người dùng chọn một Batch có ID = 10 từ danh sách
    Then Hệ thống tải danh sách các mẻ con của Batch 10 (ví dụ: Mẻ 1 và Mẻ 2)
    And Dropdown chọn Mẻ tự động thiết lập lựa chọn là Mẻ 2 (Mẻ mới nhất dựa trên run_number)
    And Hiển thị đồ thị và dữ liệu báo cáo chất lượng tương ứng với Mẻ 2
