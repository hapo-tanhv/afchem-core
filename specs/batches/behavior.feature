Feature: Hệ thống Quản lý Mẻ (Batches) và Tích hợp API
  Là một bên thứ ba (hệ thống MES/ERP) hoặc người quản lý nhà máy
  Tôi muốn hệ thống cung cấp API tạo mẻ trộn trước khi PLC hoạt động
  Và tự động liên kết các báo cáo ghi log, nhật ký cảnh báo với mẻ tương ứng
  Để tôi có thể theo dõi và thống kê báo cáo chất lượng theo từng mẻ chính xác

  Background:
    Given Hệ thống HinoTools.Alarm đang chạy và kết nối SCADA
    And HTTP API tự lưu trữ đang hoạt động ngầm ở cổng 5500
    And Bảng "batches" đã được khởi tạo trong cơ sở dữ liệu MySQL
    And Bảng "alarmreport" và "alarmlog" đã được nâng cấp thêm cột "batchId"

  Scenario: Bên thứ ba tạo mẻ thành công qua API sử dụng giá trị mặc định
    When Bên thứ ba gửi request POST đến "http://localhost:5500/api/batches/create" với JSON body rỗng
    Then Hệ thống tự động lấy tên thiết bị mặc định là "TX01"
    And Sinh số thứ tự (stt) mẻ tiếp theo trong ngày hiện tại cho "TX01" (ví dụ: "01")
    And Sinh tên mẻ dạng "TX01-yyyyMMdd-stt" (ví dụ: "TX01-20260520-01")
    And INSERT một bản ghi mới vào bảng "batches" với status là "Pending", start_time và end_time là NULL
    And Trả về HTTP Status Code 200 kèm JSON chứa thông tin mẻ vừa tạo cho bên thứ ba

  Scenario: Bên thứ ba tạo mẻ thành công qua API với thiết bị tùy chỉnh
    When Bên thứ ba gửi request POST đến "http://localhost:5500/api/batches/create" với JSON body chứa {"device_name": "TX02"}
    Then Hệ thống lấy tên thiết bị là "TX02"
    And Sinh số thứ tự mẻ kế tiếp và tên mẻ dạng "TX02-20260520-01"
    And INSERT bản ghi mới với status là "Pending" vào bảng "batches"
    And Trả về HTTP Status Code 200 kèm JSON chứa thông tin mẻ vừa tạo

  Scenario: Tự động bắt đầu mẻ trộn theo cơ chế FIFO khi PLC bắt đầu chạy
    Given Có ít nhất hai mẻ trộn ở trạng thái "Pending" của thiết bị "TX01" trong DB
    And Hệ thống đang ở trạng thái Idle (chờ mẻ) với "currentCongDoan" = 0
    When Thanh ghi "ThoiGianCapLieu" của thiết bị "AFChemTX01" (sau khi tách prefix và ánh xạ thành "TX01") có giá trị > 0
    Then Hệ thống thực hiện truy vấn DB lấy mẻ "Pending" có thời gian tạo cũ nhất (FIFO) của thiết bị "TX01"
    And Cập nhật trạng thái mẻ đó thành "Active" và gán "start_time" là thời điểm hiện tại (bắt đầu Công đoạn 1 trên tổng số 5 công đoạn)
    And Gán ID mẻ đó vào biến bộ nhớ "ActiveBatchId"
    And Chuyển sang trạng thái "Đang ghi log mẻ trộn" của mẻ hiện hành

  Scenario: Tự động khởi tạo mẻ khẩn cấp khi PLC chạy nhưng không có mẻ Pending trong DB
    Given Không có mẻ trộn nào ở trạng thái "Pending" của thiết bị "TX01" trong DB
    And Hệ thống đang ở trạng thái Idle với "currentCongDoan" = 0
    When Thanh ghi "ThoiGianCapLieu" của thiết bị "AFChemTX01" có giá trị > 0
    Then Hệ thống tự động tạo mới một mẻ khẩn cấp trong bảng "batches" cho thiết bị "TX01"
    And Gán trạng thái mẻ mới tạo là "Active" và "start_time" là thời điểm hiện tại (bắt đầu 5 công đoạn của mẻ)
    And Gán ID mẻ mới tạo vào biến bộ nhớ "ActiveBatchId"
    And Tiến hành ghi log báo cáo mẻ trộn bình thường

  Scenario: Ghi log alarmreport liên kết chính xác với batchId
    Given Hệ thống đang có một mẻ hoạt động với "ActiveBatchId" = 5
    When Timer 30 giây kích hoạt sự kiện ghi log
    Then Hệ thống thực hiện INSERT dòng dữ liệu báo cáo vào bảng "alarmreport" với đầy đủ thông tin của 5 công đoạn tương ứng
    And Giá trị cột "batchId" trong dòng vừa INSERT phải bằng 5

  Scenario: Tự động kết thúc mẻ sau khi hoàn thành trọn vẹn cả 5 công đoạn
    Given Hệ thống đang ghi log cho mẻ hiện tại có "ActiveBatchId" = 5 ứng với "CongDoanMay" = 5 (Công đoạn 5 - Xả hàng)
    When Thanh ghi "ThoiGianXaHang" và "ThoiGianRungXaHang" đều trở về giá trị 0 (từ mức > 0 trước đó, đánh dấu kết thúc Công đoạn 5 và hoàn thành 1 mẻ trộn)
    Then Hệ thống cập nhật bản ghi mẻ ID = 5 trong bảng "batches" thành "status" = "Completed" và "end_time" = thời điểm hiện tại
    And Giải phóng biến bộ nhớ "ActiveBatchId" về NULL
    And Quay về trạng thái Idle chờ mẻ tiếp theo (currentCongDoan = 0)

  Scenario: Tự động liên kết cảnh báo sự cố alarmlog với batchId hoạt động
    Given Thiết bị "TX01" đang chạy mẻ trộn có "ActiveBatchId" = 5 trong database (status = "Active")
    When Một cảnh báo sự cố xảy ra cho tag "AFChemTX01.ApSuat"
    Then Hệ thống tự động xác định thiết bị tương ứng là "TX01"
    And Thực hiện truy vấn DB tìm mẻ đang "Active" của thiết bị "TX01" (kết quả là ID = 5)
    And Thực hiện INSERT dòng cảnh báo vào bảng "alarmlog" với cột "batchId" được gán giá trị 5
