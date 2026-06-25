# Requirements Document

## Introduction
Tài liệu này định nghĩa các yêu cầu nghiệp vụ (WHAT) cho tính năng hiển thị thời gian công đoạn tích lũy thực tế realtime trên WebApp (cả Overview Dashboard và Header), đảm bảo khắc phục triệt để lỗi nhảy giật lùi/sụt thời gian về 0 khi PLC reset thanh ghi lúc máy dừng (Pause) hoặc khởi chạy lại (Resume).

Dự án hiện đã có sẵn các thành phần xử lý thời gian thực trên WebApp bao gồm:
- **Backend:** `OverviewController.cs` (hàm `GetCurrentBatchStats` lấy thông tin trạng thái mẻ chạy).
- **Frontend Header:** `LayoutMain.js` (cập nhật trạng thái chung và ticking header).
- **Frontend Overview:** `OverviewRealtime.js` (cập nhật tag WCF realtime lên Sơ đồ bồn trộn và các panel thống kê).

Tài liệu yêu cầu này tập trung vào việc nâng cấp cơ chế xử lý dữ liệu của các thành phần hiện tại thay vì xây dựng mới hệ thống.

## Requirements

### Requirement 1: Backend API Data Extraction (Lấy dữ liệu tích lũy từ Database)
**Objective:** As a WebApp Backend, I want to query the maximum accumulated values of the 8 stage timers from the `alarmreport` table for the current `runId`, so that the frontend can seed its accumulator and maintain real-time accuracy even across refreshes.

#### Acceptance Criteria
1. When a client requests `GetCurrentBatchStats` with a valid active `runId` greater than 0, the WebApp Backend shall query the max value of each stage timer (`ThoiGianCapLieu`, `ThoiGianTron1`, `ThoiGianXaDay`, `ThoiGianRungXaDay`, `ThoiGianHutXaDay`, `ThoiGianTron2`, `ThoiGianXaHang`, `ThoiGianRungXaHang`) from the `alarmreport` table.
2. If the active `runId` is invalid, not active, or not resolved (e.g. -1 or null), the WebApp Backend shall return 0 for all accumulated stage values.
3. The WebApp Backend shall return these maximum values in the JSON response under the `batchInfo.accumulatedValues` object.

### Requirement 2: Client-side JS Accumulator (Bộ tích lũy thời gian thực trên giao diện client-side)
**Objective:** As a WebApp Client, I want to calculate the accumulated duration of the current stage based on tag changes and database seed, so that the time stays accurate even when the PLC resets the tag value to 0 during pause.

#### Acceptance Criteria
1. While a batch is active, when a stage timer tag value changes, the WebApp Client shall calculate the delta `currentVal - prevVal`.
2. When the tag value changes and `currentVal >= prevVal`, the WebApp Client shall add the delta to the accumulated value of that stage.
3. When the tag value changes and `currentVal < prevVal` (indicating a reset), the WebApp Client shall add the `currentVal` to the accumulated value if `currentVal > 0`, and update the previous value tracker to `currentVal`.
4. The WebApp Client shall display this accumulated value on the Mixing Tank Diagram elements (`#feedingTime`, `#mix1Time`, `#bottomDischargeTime`, `#bottomDischargeVibrationTime`, `#bottomSuctionDischargeTime`, `#mix2Time`, `#clearanceSaleTime`, `#vibrationDischargeTime`) instead of the raw tag values.

### Requirement 3: Initialization and Synchronization (Khởi tạo và Đồng bộ hóa với Database)
**Objective:** As a WebApp Client, I want to sync the in-memory accumulators with DB data and reset them when a new run starts, so that the display is always consistent and correct.

#### Acceptance Criteria
1. When the page loads or the periodic 30-second API polling succeeds, the WebApp Client shall sync `jsAccumulatedTimers` with the database `batchInfo.accumulatedValues` by taking the maximum of `ClientValue` and `DbValue`.
2. When the `runId` returned from the API is different from the cached `activeRunId` (indicating a new run has started), the WebApp Client shall call `resetJsAccumulators()` to clear all in-memory accumulators and previous value cache to 0.
3. If `runStatus` is not `"Active"`, the WebApp Client shall display the completed step durations from the API steps table instead of updating via real-time tags.

### Requirement 4: Header Ticker Control during Pause (Khóa bộ đếm Header khi máy tạm dừng)
**Objective:** As a WebApp Client, I want to pause the header running time ticker when the machine status is paused, so that the total elapsed time does not run forward falsely.

#### Acceptance Criteria
1. While the machine is paused (`batchInfo.isPaused === 1`), the WebApp Client shall freeze the header running time display and prevent the 1-second interval ticker from incrementing the seconds.
2. When `batchInfo.isPaused` changes from 1 to 0 (resuming), the WebApp Client shall resume the 1-second header ticking from the updated running time value returned by the server.
