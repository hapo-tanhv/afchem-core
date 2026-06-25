# Walkthrough: Register Scaling (Divide by 10)

We have successfully implemented the scaling detection and division by 10 for specific PLC/HMI registers.

## Changes Made

### 1. `HinoTools.Data/Log/AlarmReportLogger.cs`

- Added `GetScaledValueString(string alias, string rawValue)`:
  - This helper detects if an alias is one of the 9 target registers (`CaiDatApSuat`, `DatNguongNhietDoMoiTruong`, `DatNguongDoAmMoiTruong`, `ApSuat`, `NhietDoMoiTruong`, `DoAmMoiTruong`, `NhietDoBonTronTren`, `NhietDoBonTronGiua`, `NhietDoBonTronDuoi`).
  - If a match is found, it parses the double value, divides it by `10.0`, and returns the formatted decimal using `CultureInfo.InvariantCulture`.
- Updated `InsertAlarmReport()`:
  - Scaled values are now logged to the `alarmreport` database table.
- Updated `GetTagValueByAlias()` and `GetSystemTagValue()`:
  - Ensured any query/comparison against these tags internally returns the correct scaled values (divided by 10).

### 2. `HinoTools.Data/Log/RealtimeThresholdLogger.cs`

- Updated `ScanAndLog()`:
  - Intercepted the tag's raw value, scaled it by `1/10` if the tag name/alias corresponds to any of the 9 target registers, and then evaluated it against the configured threshold.
  - This ensures that alarms are triggered using correct, real-world scaled values, and the database stores correct scaled values in the `Value` column of the `realtime_alarms` table.

---

## How to Verify

1. Run the Hino HMI application.
2. In the HMI/PLC driver, simulate values:
   - E.g., set `ApSuat` to `400`.
   - E.g., set `NhietDoMoiTruong` to `380`.
3. Verify that:
   - The logged value for `ApSuat` in the `alarmreport` database is `40.0` or `40`.
   - The `realtime_alarms` table triggers warning/alarm with actual value `38` if the threshold is configured to `38`.
