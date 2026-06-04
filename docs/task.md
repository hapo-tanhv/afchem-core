# Task List - Stop/Run Logic Integration

- [x] Modify `AlarmReportLogger.cs` in `HinoTools.Data`
  - [x] Add `stopStartTime` field and `StopTimeout` property
  - [x] Implement `GetSystemTagValue(string subTagName)` helper method
  - [x] Implement `FailActiveBatch()` method to set status to 'Error' and insert compensating run
  - [x] Implement `InsertRealtimeErrorEvent(string stageName, string message)`
  - [x] Modify `PollAndLog()` to implement the Stop/Reset check, flag resets, and Double-Lock stage transitions
  - [x] Update `CompleteActiveBatch()` to check for status in ('Pending', 'Active')
- [x] Modify `AlarmLogger.cs` in `HinoTools.Alarm`
  - [x] Update `GetActiveBatchAndRunId` to set status of force-closed runs to 'Error' instead of 'Completed'
  - [x] Add compensating run creation logic in `GetActiveBatchAndRunId` when run is marked as 'Error'
  - [x] Update batch completion status check to look for ('Pending', 'Active') runs
- [x] Verification
  - [x] Compile using modern MSBuild (VS 2019)
