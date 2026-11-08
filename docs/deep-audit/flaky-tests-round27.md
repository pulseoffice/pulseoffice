# Deep audit round 27: intermittent tests

## Coverage (files read)
| File | Outcome |
|---|---|
| tests/PulseOffice.Sheets.UI.Tests/LedgerRound6bAuditTests.cs (helpers, goal seek test) | flaky: concurrent control construction |
| tests/PulseOffice.Sheets.UI.Tests/LedgerRound7AuditTests.cs (helpers, data form test) | flaky: same cause |
| tests/PulseOffice.Sheets.UI.Tests/LedgerCommandsAuditTests.cs (`Inert` proxy and its readers) | shared static call log, fixed |
| tests/PulseOffice.Desktop.Tests/LedgerEditorTests.cs (harness, fault guard test) | race with fire-and-forget snapshots, fixed |
| tests/PulseOffice.Desktop.Tests/HeadlessDesktopSession.cs, tests/Shared/UiThreadSession.cs | read, no defect |
| src/PulseOffice.Shell/InterfaceFaultGuard.cs | product defect via DocumentRecovery (overlapping snapshots) |
| src/PulseOffice.Shell/DocumentRecovery.cs | product defect, fixed |
| src/PulseOffice.UI.Controls/Services/StatusBarService.cs | read, no defect |
| src/PulseOffice.UI.Controls/Preview/DispatcherDebounceTimerFactory.cs | read, no defect |
| src/PulseOffice.Sheets.UI/LedgerSheetExtraCommands.cs (data form) | read, deterministic logic |
| src/PulseOffice.Sheets.UI/LedgerAnalysisCommands.cs (goal seek) | read, deterministic logic |

## Findings

### 1. Sheets.UI goal seek and data form tests (test defect)
Reproduced: 1 failure in 6 runs of the project, then again in the next loop. Stack:
`InvalidOperationException: Stack empty` in `Avalonia.PropertyStore.AvaloniaPropertyDictionaryPool.Get()`, from
`DialogRow` built by `GoalSeekView`. Avalonia keeps process-wide pools that are not thread safe: they assume every
`AvaloniaObject` lives on one UI thread. `PulseOffice.Sheets.UI.Tests` has no UI thread and xUnit ran its test classes in
parallel, so two dialog views (goal seek, data form, any other) were built at the same time on two threads and corrupted
the pool. In the app every control is built on the UI thread, so the product is not affected.
Fix: `tests/PulseOffice.Sheets.UI.Tests/TestParallelization.cs` disables test parallelization for the assembly (the
project runs in about one second). Same exposure likely in the other `*.UI.Tests` projects that build controls without a
headless session: not changed in this round, flagged for follow-up.

### 2. Shared static call log of the `Inert` proxy (test defect)
`LedgerCommandsAuditTests.Inert.Log` was a static list written by every fake of every class: one test cleared it while
others appended, another read `Last(SaveFileAsync)` that could come from another test. The log is now per proxy
instance and the two readers cast their own fake.

### 3. Fault guard test in Desktop.Tests (product defect + test defect)
`InterfaceFaultGuard.Absorb` starts an emergency snapshot of every modified document without awaiting it (it runs inside
the dispatcher's exception handler). Six faults in a row started six overlapping `DocumentRecovery.SnapshotAsync` calls
for the same document: all wrote the same `<id>.dati.tmp` with `FileShare.None` and each `finally` deleted the
temporary file another call was about to move, so snapshots failed (logged as warnings) and the last good copy could be
lost. The test then left the scope while snapshots were still writing under its private data home, and `Dispose`
deleted that directory recursively under them: the reported sub-millisecond failure is the teardown.
Fix: `DocumentRecovery` queues snapshots (each waits for the previous one; faults of the previous one do not propagate
to the next), and exposes `IDocumentRecovery.WhenIdleAsync()`. The desktop test now waits for recovery to settle,
pumping the dispatcher, and asserts the snapshot exists.
Deterministic test: `DocumentRecoveryTests.CopieDiRecuperoSovrappostePerLoStessoDocumentoSiScrivonoUnaAllaVolta`,
verified failing before the fix (two writers active together) and passing after.

## Static mutable state
No mutable static fields in `PulseOffice.Sheets.UI`, `PulseOffice.Shell`, `PulseOffice.UI.Controls` and the Sheets.UI
tests besides the `Inert` log (fixed). Shared `static readonly` text catalogs in the round tests are never switched.

## Verification
See the final runs below.
- `PulseOffice.Sheets.UI.Tests`: 20 consecutive green runs (421 tests) after the fix; before it, 2 failures in 7 runs.
- `PulseOffice.Desktop.Tests` filtered on `LedgerEditorTests`: 3 consecutive green runs (29 tests) plus the single test alone.
- `PulseOffice.Shell.Tests`: green, 213 tests, repeated five times.
- An intermediate version of the desktop test that awaited `WhenIdleAsync()` directly on the headless UI thread hung
  once (the continuation needs the dispatcher to be pumped); the final version pumps with `Dispatcher.UIThread.RunJobs`.
