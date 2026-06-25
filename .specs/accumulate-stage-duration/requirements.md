# Requirements Document

## Introduction
The system currently experiences a time-duration discrepancy when the machine is paused and resumed. When the PLC receives a resume command (`STOP = 0`), it resets its internal process stage timers (e.g., `ThoiGianCapLieu`, `ThoiGianTron1`) to 0, and then starts counting up again. Consequently, the C# Core Logger (which directly reads these PLC registers) loses the duration elapsed prior to the pause. This results in incorrect timeseries data in the `alarmreport` table and triggers false alarms since the logger checks the process duration against recipe thresholds using only the post-resume duration.

To resolve this safely without modifying the PLC code, this specification introduces a **Software Accumulator** in the C# Core Logger. The logger will track and accumulate timer changes in memory, detecting when a register is reset and continuing the accumulation seamlessly. It will also support self-healing on restart by reading the last accumulated state from the database.

## Requirements

### Requirement 1: Software Accumulator for Stage Timers
**Objective:** As a Developer, I want a software-based timer accumulator in the Core Logger, so that stage running durations are computed continuously and correctly even when PLC registers are reset on machine resume.

#### Acceptance Criteria
1. While máy đang trong trạng thái mẻ chạy hoạt động (active run) và công đoạn đang hoạt động (currentCongDoan > 0), the Core Logger shall liên tục so sánh giá trị thanh ghi PLC hiện tại với chu kỳ trước đó để tích lũy thời gian.
2. When giá trị PLC hiện tại lớn hơn hoặc bằng giá trị chu kỳ trước (PLC đang đếm bình thường), the Core Logger shall cộng thêm lượng chênh lệch (currentValue - previousValue) vào biến tích lũy của công đoạn đó.
3. When giá trị PLC hiện tại nhỏ hơn giá trị chu kỳ trước (PLC đã reset thanh ghi về 0 hoặc giá trị nhỏ khi chạy lại), the Core Logger shall cộng thêm chính giá trị PLC hiện tại vào biến tích lũy của công đoạn đó (nếu giá trị hiện tại > 0).
4. When chuyển sang một công đoạn tiếp theo hoặc khi bắt đầu một mẻ chạy mới, the Core Logger shall đặt lại biến tích lũy của công đoạn mới về 0.

### Requirement 2: Database Storage in `alarmreport`
**Objective:** As a Data Analyst, I want the accumulated stage durations to be logged into the `alarmreport` table, so that we can query and export correct process durations.

#### Acceptance Criteria
1. While mẻ chạy đang hoạt động, when ghi nhận dữ liệu định kỳ (Polling Interval) hoặc khi kết thúc/lỗi mẻ, the Core Logger shall ghi giá trị tích lũy vào cột tương ứng của bảng `alarmreport` thay vì ghi trực tiếp giá trị thô từ PLC.
2. The Core Logger shall lưu trữ giá trị tích lũy làm tròn tối đa 2 chữ số thập phân.

### Requirement 3: Self-Healing on Logger Restart
**Objective:** As an Operator, I want the accumulator to recover its state if the Logger app restarts mid-batch, so that we don't lose the elapsed duration from before the restart.

#### Acceptance Criteria
1. When Logger khởi động lại và phát hiện mẻ chạy vẫn đang ở trạng thái hoạt động (Active) trong cơ sở dữ liệu, the Core Logger shall truy vấn giá trị lớn nhất đã lưu của công đoạn hiện tại từ bảng `alarmreport` để khởi tạo lại giá trị tích lũy ban đầu.
2. If không tìm thấy dữ liệu cũ trong `alarmreport` cho mẻ hiện tại, the Core Logger shall khởi tạo giá trị tích lũy ban đầu bằng 0.

### Requirement 4: Accurate Stage Duration Alarms
**Objective:** As a Quality Control Engineer, I want duration alarms to compare the accumulated duration against setpoints, so that we prevent false alarm alerts.

#### Acceptance Criteria
1. When kết thúc một công đoạn (phát hiện sườn xuống), the Core Logger shall sử dụng giá trị tích lũy thời gian của công đoạn đó (thay vì giá trị thô `prevValue`) để truyền vào hàm `CheckAndLogStageDurationAlarm()` phục vụ việc đánh giá và phát cảnh báo.
