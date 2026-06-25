# Research & Design Decisions - Software Timer Accumulator

## Summary
- **Feature**: `accumulate-stage-duration`
- **Discovery Scope**: Extension of process logging capability in Core Logger
- **Key Findings**:
  - The PLC registers reset to 0 (or a small value) upon receiving a resume command (STOP = 0).
  - Memory-based software tracking can detect these transitions by comparing current and previous register values within the 1-second polling loop.
  - State recovery can be achieved upon logger startup by querying the maximum logged value from the `alarmreport` table for the current active run.

## Research Log

### PLC Timer Reset Handling
- **Context**: The PLC reset behavior is a hardware constraint. The timer for the active stage resets to 0 (or starts from 1, 2, 3...) when the process resumes.
- **Findings**:
  - The logger polls the PLC registers every 1 second in `PollAndLog()`.
  - By maintaining the previous PLC read value, we can detect a reset whenever `currentValue < previousValue`.
  - Normal count-up yields `currentValue >= previousValue`, where the increment is `currentValue - previousValue`.
  - On reset, the timer starts from 0, so the new elapsed time is simply `currentValue`.
- **Implications**:
  - The system will introduce a helper dictionary or fields for accumulated stage durations: `accumulatedCapLieu`, `accumulatedTron1`, `accumulatedXaDay`, `accumulatedRungXaDay`, `accumulatedHutXaDay`, `accumulatedTron2`, `accumulatedXaHang`, `accumulatedRungXaHang`.
  - When writing to `alarmreport` or checking thresholds, the accumulated values are used instead of the raw values.

### State Recovery on Logger Startup
- **Context**: If the logger app crashes or restarts mid-batch, the memory accumulator is lost. It must recover its state.
- **Findings**:
  - The `alarmreport` table stores the stage durations periodically.
  - Since values are accumulated, the maximum value logged in the current run represents the last accumulated state.
  - We can run a query:
    `SELECT MAX(CAST(\`[AliasName]\` AS DECIMAL(10,2))) FROM \`alarmreport\` WHERE \`runId\` = {runId}`
    to find the last recorded accumulator value for the active stage.
- **Implications**:
  - At startup (`TryInitialize` / `PollAndLog` sync active run), if an active run is detected, we query the DB to recover the accumulated values for all stages of that run.

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| Software Accumulator in C# memory | Track delta changes of PLC registers in memory and store the sum | Simple, does not require PLC program changes, robust to reset values | Reset of memory on app crash | Aligns with existing Logger state machine pattern; mitigated by self-healing DB query |
| Database-Triggered Summation | Keep raw PLC values in DB, sum them via SQL grouping | Decoupled from logger memory | High database write load, complex query mapping for Excel/UI | Rejected due to high query complexity and performance overhead |

## Design Decisions

### Decision: In-Memory Software Accumulator with Database Recovery
- **Context**: PLC timer resets on resume, causing time loss.
- **Alternatives Considered**:
  1. Option A — Sửa đổi PLC (Rejected: không thể can thiệp chương trình PLC của máy đang hoạt động).
  2. Option B — Database-triggered summation (Rejected: viết nhiều dòng thô rồi SUM phức tạp, gây phình to database).
- **Selected Approach**: Maintain accumulated stage duration variables in C# memory (`AlarmReportLogger`), using delta-tracking. Recover accumulator state from `alarmreport` database if logger restarts.
- **Rationale**: Keeps the DB schema simple, ensures continuous duration values in `alarmreport`, and requires zero changes to the PLC.
- **Trade-offs**: Memory state must be protected against application crashes, resolved via database self-healing.
- **Follow-up**: Verify that delta detection is robust when there are communication delays/latencies.

## Risks & Mitigations
- **Logger App Restart** — Mitigated by querying the maximum previously recorded value from the `alarmreport` table for the active `runId` on initialization.
- **Noise/Jitter in PLC reads** — Timer registers are strictly monotonic between resets, so delta calculation is highly stable.
- **Stage transition timings** — Resetting flags (`ResetFlags`) during pauses ensures that transition checks are not falsely triggered.

## References
- [ATSCADA Driver Documentation] — Details about tag polling lifecycle.
- [HinoTools Database Schema] — Column structure of `alarmreport` and `realtime_alarms`.
