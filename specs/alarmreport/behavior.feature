Feature: Định kỳ ghi nhận báo cáo mẻ trộn (Alarm Report)
  Là một người quản lý nhà máy
  Tôi muốn hệ thống tự động ghi lại trạng thái của mẻ trộn mỗi 30 giây khi máy đang hoạt động (dựa trên các thanh ghi timer thực tế)
  Để tôi có thể theo dõi và thống kê chi tiết trên Web Dashboard với dữ liệu time-series

  Background:
    Given Hệ thống HinoTools.Alarm đang chạy và kết nối SCADA
    And Component "AlarmReportLogger" đang hoạt động với vòng lặp 30s
    And Bảng "alarmreport" đã có đủ 17 cột thanh ghi + TagNo + QuyTrinh + CongDoanMay

  Scenario: Bắt đầu mẻ mới (Quy trình mới) khi nạp liệu
    Given Hệ thống đang ở trạng thái chờ (không ghi log)
    When Thanh ghi "ThoiGianCapLieu" bắt đầu có giá trị > 0
    Then Hệ thống chuyển sang trạng thái "Đang ghi log mẻ trộn"
    And Hệ thống truy vấn Database lấy QuyTrinh lớn nhất hiện tại và cộng thêm 1
    And Bắt đầu thực hiện chuỗi ghi log mỗi 30 giây với QuyTrinh mới

  Scenario: Ghi log liên tục trong suốt 5 công đoạn
    Given Hệ thống đang ở trạng thái "Đang ghi log mẻ trộn"
    When Timer đếm đủ 30 giây
    Then Hệ thống đọc toàn bộ 17 thanh ghi từ SCADA
    And Thực hiện lệnh INSERT xuống bảng "alarmreport" với cùng một ID QuyTrinh hiện tại

  Scenario: Nhận diện hoàn thành công đoạn 3 (Xả đáy)
    Given Hệ thống đang ở trạng thái "Đang ghi log mẻ trộn" thuộc "CongDoanMay" = 3
    And Các thanh ghi xả đáy (ThoiGianXaDay, ThoiGianRungXaDay, ThoiGianHutXa) đang có giá trị (vd: 16)
    When Thanh ghi "ThoiGianHutXa" tăng lên 586 rồi nhảy về 0 ở lần đọc kế tiếp
    Then Hệ thống nhận diện Công đoạn 3 đã hoàn thành
    And Chuyển sang theo dõi Công đoạn 4 khi "ThoiGianTron2" bắt đầu có dữ liệu

  Scenario: Kết thúc mẻ trộn (Quy trình hoàn tất) tại Công đoạn 5
    Given Hệ thống đang ghi log cho Công đoạn 5
    And Thanh ghi "ThoiGianXaHang" đã chạy trước đó, sau đó "ThoiGianRungXaHang" mới bắt đầu có giá trị > 0
    When Thanh ghi "ThoiGianRungXaHang" nhảy về 0 ở lần đọc kế tiếp
    Then Hệ thống nhận diện Công đoạn 5 đã hoàn thành
    And Hệ thống nhận diện toàn bộ mẻ trộn (Quy trình) đã hoàn tất
    And Hệ thống chuyển về trạng thái chờ (ngừng ghi log) cho đến mẻ tiếp theo

  Scenario: Cập nhật cơ chế ghi log sự kiện (alarmlog) cho thanh ghi liên tục
    Given Một thanh ghi đang được theo dõi cho `alarmlog` có giá trị ban đầu là 0
    When Giá trị thanh ghi thay đổi thành 1 (> 0)
    Then Hệ thống ghi nhận "OccurrenceTime" và thực hiện lệnh INSERT trạng thái "Alarm" vào bảng "alarmlog"
    When Giá trị thanh ghi tiếp tục thay đổi liên tục (2, 3, 4...) nhưng vẫn > 0
    Then Hệ thống giữ nguyên trạng thái "Alarm" và KHÔNG thực hiện INSERT mới hay UPDATE "RestoreTime"
    When Giá trị thanh ghi nhảy về 0
    Then Hệ thống ghi nhận "RestoreTime" và UPDATE trạng thái thành "Resolved" cho bản ghi đã INSERT trước đó

  Scenario: Giám sát và cảnh báo vượt ngưỡng thời gian thực (Realtime)
    Given Hệ thống đã khởi tạo một Timer giám sát độc lập chạy lặp lại mỗi 3 giây
    And Bảng "realtime_alarms" đã được chuẩn bị trong cơ sở dữ liệu
    When Timer 3 giây kích hoạt sự kiện đọc dữ liệu SCADA
    And Giá trị thanh ghi (Nhiệt độ, Áp suất, v.v...) đọc được LỚN HƠN (>) ngưỡng cài đặt cho phép
    Then Hệ thống thực hiện lệnh INSERT cảnh báo vượt ngưỡng vào bảng "realtime_alarms"
    And Nếu giá trị thanh ghi <= ngưỡng an toàn, hệ thống không thực hiện hành động ghi nào
