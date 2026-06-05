# Task List - Setpoints Logging and Dynamic Updates

- [x] Database Schema Migration
  - [x] Update `EnsureBatchesTableExists()` in `AlarmReportLogger.cs` to add 8 columns (`sp_thoi_gian_...`) to the `runs` table
- [x] Implement Setpoints Tracking
  - [x] Add `lastSetpoints` cache array at the class level in `AlarmReportLogger.cs`
  - [x] Update `ResetFlags()` in `AlarmReportLogger.cs` to reset the cache array to `-1`
  - [x] Implement `CheckAndUpdateSetpoints()` helper in `AlarmReportLogger.cs` to poll and update setpoint values when changed
  - [x] Update `PollAndLog()` in `AlarmReportLogger.cs` to call `CheckAndUpdateSetpoints()` on each poll cycle
- [x] Compilation & Verification
  - [x] Rebuild the solution using MSBuild 2019
  - [x] Verify database updates on start and when setpoint changes occur

- [x] Register Scaling (Divide by 10)
  - [x] Create scaling helper method in `AlarmReportLogger.cs`
  - [x] Implement scaling in `AlarmReportLogger.cs` value resolvers & `InsertAlarmReport()`
  - [x] Implement scaling in `RealtimeThresholdLogger.cs` inside `ScanAndLog()` before evaluation and insert
  - [x] Compile and verify solution rebuild

