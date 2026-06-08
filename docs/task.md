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

- [x] Stage Duration Alarms
  - [x] Declare `prev...` fields in `AlarmReportLogger.cs`
  - [x] Reset `prev...` fields in `ResetFlags()`
  - [x] Detect falling edge in `PollAndLog()` when `activeRunId` is not null
  - [x] Update `prev...` values at the end of `PollAndLog()`
  - [x] Implement comparison logic: `|Actual - Setpoint| > Threshold` in helper methods
  - [x] Query configuration from `alarmsettings` (Value column) and setpoints from `runs`
  - [x] Write alarm events to `realtime_alarms` with `Severity = 'ALARM'`
  - [x] Validate compilation with MSBuild 2019
  - [x] Perform integration test with simulated data and verify output

- [x] Duplicate Prevention on Restart
  - [x] Implement active alarm check in `InsertAlarm` within `AlarmLogger.cs`
  - [x] Map C# normal/resolve events to original GUID/ID and execute update instead of insert
  - [x] Prevent active run from being marked as Error and starting duplicate run on restart
  - [x] Implement duplicate check for `realtime_alarms` in `AlarmReportLogger.cs`
  - [x] Rebuild solution and verify with test script `test_duplicate_check.ps1`



