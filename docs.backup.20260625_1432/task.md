# Task List - Recreate from Webhook Log

- [x] Modify PowerShell Script `recreate_from_webhook_log.ps1`
  - [x] Add `-Reset` switch parameter.
  - [x] Implement database clearing for `run_info`, `runs`, and `batches` tables in correct FK dependency order when `-Reset` is active.
  - [x] Change query to select `webhook_logs` with status `'Completed'` ordered by `id ASC` (unless a specific `$LogId` is provided).
  - [x] Add in-memory Dictionary tracking for batch sequence numbers (STT) per device and date to ensure clean, correct incrementing starting from `01`.
  - [x] Insert batches, runs, and BOM records accurately.
- [x] Verification
  - [x] Verify script syntax.
  - [x] Run test locally to verify that data is cleared and correctly regenerated from completed webhook logs.
