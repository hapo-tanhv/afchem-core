# Plan - Recreate batches, runs and run_info from webhook_logs

This plan outlines how to enhance the PowerShell recovery script `recreate_from_webhook_log.ps1` to correctly regenerate batches, runs, and run_info data from the webhook logs in the database.

## User Review Required

We need the user's decision on the following crucial design behaviors before execution:

> [!IMPORTANT]
> **1. Data Clearing (Reset/Truncate):**
> When running this script, do you want it to automatically clear (DELETE/TRUNCATE) all existing records in the `batches`, `runs`, and `run_info` tables first?
> - **If YES:** We ensure that the generated sequences (e.g., `TX01-20260616-01`, `-02`, etc.) correspond exactly to the order of entries in `webhook_logs`, and we prevent duplicate batch entries.
> - **If NO:** We keep existing data, but running the script against already-processed webhook logs will cause duplicate batches or incremented STT numbers (e.g., creating `TX01-20260616-03` even if the log was previously processed).
>
> **2. Processing Scope:**
> Should the script process ALL records in `webhook_logs` (including those marked as 'Completed' or 'Error'), or only those currently marked as 'Pending'?
> - If we clear the target tables, we must process all webhook logs to reconstruct the data.

## Proposed Changes

### PowerShell Script

#### [MODIFY] [recreate_from_webhook_log.ps1](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/scratch/recreate_from_webhook_log.ps1)
- Add a new parameter `[switch]$Reset` to support clearing existing tables and reset webhook log status.
- Add logic to delete/truncate tables in correct foreign key order: `run_info` first, then `runs`, then `batches` if `$Reset` is passed.
- Update all `webhook_logs` status to 'Pending' before processing if `$Reset` is passed.
- Read all records ordered by `id ASC` from `webhook_logs`.
- Build a tracking dictionary in memory to track the last used batch sequence (STT) for each `(deviceName, dateStr)` pair to ensure sequence numbers match exactly the chronologically ordered logs, regardless of whether database queries return old data.
- Update log outputs to provide clear debug messages on the batch sequence generated.

## Verification Plan

### Manual Verification
- We will execute the script with `-Reset` switch and check:
  1. `batches`, `runs`, and `run_info` are cleared successfully.
  2. All webhook logs are read and processed chronologically.
  3. Batch names are generated correctly with matching sequences (e.g. `-01`, `-02`).
  4. Runs and run_info items (BOMs) are correctly associated with the correct runs.
  5. The webhook logs status is updated back to `Completed`.
