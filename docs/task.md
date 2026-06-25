# Task List - WebApp Realtime Stage Duration Accumulator

- [x] 1. Backend Development
  - [x] 1.1 Update API `GetCurrentBatchStats` in `OverviewController.cs` to return `accumulatedValues` representing the numerical maximum of each stage duration from database.
- [x] 2. Frontend Layout Development
  - [x] 2.1 Track global `window.headerIsPaused` state in `LayoutMain.js`.
  - [x] 2.2 Freeze layout header ticking interval clock when `window.headerIsPaused === true`.
- [x] 3. Frontend Realtime Development
  - [x] 3.1 Initialize client-side accumulator states (`jsAccumulatedTimers` & `jsPreviousTimerValues`) in `OverviewRealtime.js`.
  - [x] 3.2 Implement delta-ticking calculator `getJsAccumulatedValue` handling paused register resets.
  - [x] 3.3 Bind mixing tank diagram HTML elements with accumulator logic using `updateTimerTag`.
  - [x] 3.4 Integrate database synchronization and self-healing logic using `Math.max` in API callback.
  - [x] 3.5 Implement automatic accumulator resets when switching runs.
- [x] 4. Testing & Verification
  - [x] 4.1 Unit Testing: C# accumulator unit tests (4/4 tests success) in `ConsoleApp.exe`.
  - [x] 4.2 Unit Testing: JS accumulator unit tests (3/3 tests success) in `verify_js_accumulator.js` run on Node.js.
  - [x] 4.3 Integration Testing: Manual verification of live data ticking, paused clock freezing, page reload recovery, and run transition resetting.
  - [x] 4.4 Code Review: Execute static code analysis and validation of correctness, security, regressions, and performance.
