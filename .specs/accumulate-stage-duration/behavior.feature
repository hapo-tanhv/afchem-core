Feature: Software Timer Accumulator
  As a Developer
  I want a software-based stage duration timer accumulator in the Core Logger
  So that process stage durations are computed continuously and logged accurately even if the PLC timer resets on resume.

  Background:
    Given a batch is currently Active
    And the current active stage is "T001" (Cấp liệu)
    And the Core Logger has initialized the accumulated timer for "T001" to 0

  Scenario: Happy Path - Normal stage counting without pause
    When the PLC timer for "T001" counts from 0 to 30 seconds
    Then the Core Logger shall accumulate the timer value as 30 seconds
    And when the stage ends, the Core Logger shall save 30 seconds into the "alarmreport" database

  Scenario: Edge Case - Pause and resume with PLC timer reset
    Given the PLC timer for "T001" runs up to 14 seconds
    And the machine is paused (STOP = 1)
    And the accumulated timer value is 14 seconds
    When the machine resumes (STOP = 0)
    And the PLC timer for "T001" resets to 0 seconds and counts up to 20 seconds
    Then the Core Logger shall accumulate the timer value continuously from 14 seconds up to 34 seconds
    And when the stage ends, the Core Logger shall log 34 seconds as the duration for "T001"
    And the Core Logger shall not trigger false stage duration alarms for 20 seconds

  Scenario: Error State & Recovery - Logger restarts mid-batch
    Given the accumulated timer value for "T001" in "alarmreport" database is 15 seconds
    When the Logger application crashes and restarts
    Then the Core Logger shall query the database and recover the initial accumulated timer value as 15 seconds
    And when the PLC timer continues from 0 to 10 seconds
    Then the Core Logger shall resume accumulation from 15 seconds and reach 25 seconds
