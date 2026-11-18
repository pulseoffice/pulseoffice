# Round 163: closed-document lifetime (continued) and relative timing tests

Two parts: what still keeps a closed document reachable after round 156, and the timing tests whose absolute
thresholds failed when the test runner runs five chunks at once. Nothing was built or run in this round; the
coordinator builds and runs the tests after joining the cycle.

## Fixed

| Severity | Area | Defect | Fix |
|---|---|---|---|
| Medium | Recovery | A recovery snapshot queued by autosave (which walks the modified documents one by one, awaiting each) or by the fault guard, and run after its document had been closed, registered the closed document again in `DocumentRecovery._identifiers` and wrote a recovery file for it. The closed document stayed reachable until the app quit, and the next start offered to "recover" a document the user had closed on purpose. | `DocumentRecovery` counts discards per document in a `ConditionalWeakTable`; a queued snapshot remembers the count when it was asked and is skipped if `DiscardFor` ran in between. Test: `Round163RecoveryLifetimeTests`. |
| Minor | Macros | `CommandRecorder` kept the owner document of a running recording after that document closed: the document stayed alive, the recorder stayed in recording mode, and stopping handed the macro to a closed document. | The recorder listens to `DocumentClosed` and ends the recording of a closed owner (owner and commands dropped, `Changed` raised). Test: `Round163CommandRecorderLifetimeTests`. |
| Medium | Tests | Timing tests asserted absolute durations (from 2 ms per caret move to a few seconds) and failed under parallel load. | New shared `tests/Shared/GrowthCheck.cs` (linked into every test project by `tests/Directory.Build.props`): `Linear` times a run at size n/10 (after a warm-up run) and at size n and bounds the ratio by the size ratio times a slack of 4; `Flat` does the same for operations that must not depend on an extent (whole column versus the used rows); `Bounded` compares two measured times. A floor (2 ms by default) on the small time keeps noise on tiny runs from failing. Rewritten: `RoundSixteenCoreTests` (three tests), `RoundFourteenCoreTests` (two), `RoundTwoLimitsTests`, `StyleCollectionRound98Tests`, `AnchoredObjectIndexTests` (three), `FolioScaleTests` (export, typing, page status, recovery: 600 against 6000 paragraphs), `ChromeCostTests` (ribbon projection against building the ribbon tree), `DeckRenderCostTests` (20 against 200 slides), `DeckRecoveryCopyTests`, `Round133TableEditorTimingTests`, `WholeRangeRound21Tests` (every test: the whole range against the 16 used rows), `CsvWholeColumnRound21Tests`, `RowAutoHeightTests`, `SparklineDataRangeTests`, `MergedRegionsIndexTests` (two), `DeepAuditSheetsEngineTests` (huge arguments against small ones), `SheetRegionsTests`, `StatisticsProceduresScaleTests` (two), `RoundFourteenLineTests`, `RichTextInsertionTests`. Each keeps its functional assertions and its original large size. |

## Extended

- `Round163ClosedDocumentPanelsTests` (Desktop.Tests): the round 156 release check with a panel open on the document
  before it closes: Ledger navigator, Folio navigator and comments, Deck navigator and comments.

## Checked clean

- `ExternalLinkAutoRefresh` and `ExternalLinkSchedule`: the timer reads the active context on each tick, and the
  schedule keys workbooks in a `ConditionalWeakTable`.
- `ModuleNavigatorSource` and `ModuleReviewSource`: they refollow the active document on activation and on close,
  unsubscribing from the previous history.
- `DeckArrangeCommands`, `FolioAccessibilityCommands`, `TextReviewEditWatch`, `CommandExecutor`: history and document
  subscriptions follow the active target and are dropped when it changes or closes.
- `ExchangeDragDrop`, `FormDesignModeOnOpen`, `ShellFontCommands`: handlers added to each view or run once, no
  reference kept in a long-lived service.
- `RecoveryCoordinator`: unsubscribes from a document's `Changed` on close.

## Open

- Timing tests still asserting absolute durations, not rewritten in this round: `SheetLayoutScaleTests`,
  `SheetFormattingTests`, `GridAndDataAuditTests`, `GridGeometryTests`, `LedgerGridRound19Tests`,
  `LedgerRenderCostTests`, `ReviewPanelCostTests`, `FolioReviewTests` (typing), `SubtotalVisibilityRecalculationTests`,
  `Round126SubtotalsTests`, `DeepAuditRound27ChartTests`, `WholeColumnFillPerformanceTests`,
  `WholeColumnMillionRowPerformanceTests`, `DeepAuditLedgerFunctionsRound16/51/52Tests`, `PivotLimitsTests`,
  `RoundSixtyProbeTests`, `RoundFourteenLayoutTests`, `DeepAuditRoundTwentyTwoPasteTests`, `DocxManyRunsTests`,
  `DocxParagraphMarkChainPerformanceTests`, `TextEditSpliceTests`, `AuditSpellingTests`, `CompactTableTests`,
  Typography `RobustnessTests`. Their bounds range from 150 ms to a few seconds; they should move to `GrowthCheck`.
- Bounds of 5 s or more that guard against hangs on hostile input or check a sandbox time cap (`HostileFileTests`,
  `HostileRichClipboardTests`, `HostileInputTests`, `SvgRasterizingTests`, `JavaScriptSandboxTests`,
  `ScriptExecutionPermissionsTests`, `PythonVersionRangeTests`, `TabularSourcesTests`, `MetricsTablesTests`, the
  large ODF and OOXML read and export tests) prove termination rather than speed; they were left as they are.
- The new tests and the rewritten ones have not been compiled or run in this round.
- Charts, images, macros IDE windows, find and replace and MCP agent sessions were inspected by reading code only;
  the release test does not exercise them yet.

## Status

Two lifetime defects fixed (one medium, one minor); the timing tests named in the brief and the tightest absolute
bounds are relative; the remaining ones are listed above.
