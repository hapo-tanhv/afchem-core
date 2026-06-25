# Plan: Implement Register Scaling (Divide by 10)

This plan outlines the changes needed to support scaling (dividing by 10) for specific PLC/HMI registers that represent decimal numbers but are returned as integers multiplied by 10.

## Targeted Registers

The following 9 registers will be scaled (divided by 10):
- `CaiDatApSuat`
- `DatNguongNhietDoMoiTruong`
- `DatNguongDoAmMoiTruong`
- `ApSuat`
- `NhietDoMoiTruong`
- `DoAmMoiTruong`
- `NhietDoBonTronTren`
- `NhietDoBonTronGiua`
- `NhietDoBonTronDuoi`

---

## Proposed Changes

### 1. `HinoTools.Data/Log/AlarmReportLogger.cs`

- **Helper Method**: Add a helper method `GetScaledValueString(string alias, string rawValue)` to detect the target registers and scale the value by 10.0 using `CultureInfo.InvariantCulture`.
- **Insert Logging**: Update `InsertAlarmReport()` to apply `GetScaledValueString()` on each register's raw value before adding it to the SQL query.
- **Value Resolvers**: Update `GetTagValueByAlias()` and `GetSystemTagValue()` to divide by 10.0 if the target alias matches any of the 9 registers.

### 2. `HinoTools.Data/Log/RealtimeThresholdLogger.cs`

- **Scan and Evaluate**: Update `ScanAndLog()` to detect if the item's alias is one of the 9 target registers. If so, divide `currentValue` by 10.0 *before* executing `EvaluateThreshold()` and inserting the record.
- This ensures the database logs the actual scaled value (e.g. `38.0` instead of `380.0`) and compares it correctly with the user-configured thresholds.

---

## Verification Plan

### Manual Verification
1. **Rebuild the Solution**: Terminate any running instance of the Hino application and rebuild the project using MSBuild.
2. **Simulate/Mock Values**: Set a target register (e.g., `ApSuat`) to `400` in HMI/driver.
3. **Check Database logs**:
   - Verify that `alarmreport` has `40.0` or `40` for the `ApSuat` column.
   - Verify that `realtime_alarms` logs `38.0` or similar for alarm entries when `NhietDoMoiTruong` is breached.
