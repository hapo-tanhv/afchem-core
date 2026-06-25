# Implementation Plan - Software Timer Accumulator

- [ ] 1. Core Logger Software Accumulator Logic
- [ ] 1.1 Implement in-memory accumulator tracking
  - Initialize the stage duration tracking variables for the 8 process stages
  - Compare current tag values from PLC with previous values on each 1-second polling tick
  - Add the delta difference (current - previous) to the corresponding stage accumulator when values increase monotonically
  - Detect register reset (current < previous) and accumulate the new starting value directly
  - Reset the corresponding stage accumulator to 0 upon transitioning to a new stage or starting a new run
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [ ] 1.2 Implement self-healing state recovery from database
  - On startup and run synchronization, query the database to find the maximum previously recorded value for each stage under the active run ID
  - Initialize the in-memory stage accumulator variables with the maximum values retrieved from the database
  - Fall back to initializing the accumulator to 0 if no records are found in the database for the active run
  - _Requirements: 3.1, 3.2_

- [ ] 1.3 Save accumulated values to telemetry database
  - Periodically write the accumulated values instead of raw PLC registers to the telemetry database at the configured polling interval
  - Round the stored accumulator values to at most 2 decimal places before persistence
  - Use the accumulated duration instead of raw values when triggering process stage duration verification alarms at the end of each stage
  - _Requirements: 2.1, 2.2, 4.1_

- [ ] 2. WebApp Excel Report Enhancement
- [ ] 2.1 (P) Integrate stage name into the Excel pause event description
  - Modify the batch record Excel export process to retrieve the process stage code of the pause event from the telemetry table
  - Map the retrieved process stage codes (T001-T008) to their respective Vietnamese process stage names (e.g. Cấp liệu, Trộn 1)
  - Append the mapped stage name into the pause incident description message in the exported Excel sheet
  - _Requirements: 2.1_

- [ ] 3. Verification and Integration Testing
- [ ] 3.1 Verify accumulation and self-healing under simulated pause events
  - Simulate a process run, pause the machine mid-stage, verify the timer register reset, and confirm the accumulator continues counting correctly
  - Stop the logger, verify that restarting it restores the accumulated values from the database and resumes counting seamlessly
  - Verify that stage duration alarms are assessed against the total accumulated time rather than post-resume duration
  - _Requirements: 1.2, 1.3, 3.1, 4.1_

- [ ] 3.2* Run automated unit tests for accumulator delta-tracking logic
  - Execute automated unit tests verifying delta calculations under normal, reset, and communication jitter cases
  - _Requirements: 1.2, 1.3_
