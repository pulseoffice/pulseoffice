# Deep audit, round 2 — sheet model and engine

Area: `src/PulseOffice.Sheets`, `PulseOffice.Sheets.Engine`, `PulseOffice.Sheets.Pivot`, `PulseOffice.Sheets.Printing`,
`PulseOffice.Sheets.Queries`. Round 1 notes: [sheets-engine.md](sheets-engine.md), [sheets-model.md](sheets-model.md).

## Points left open by round 1

### 1. SheetLayout by intervals — closed

Rows and columns were one dictionary entry per index (sizes, effective sizes, hidden sizes, optimal rows, row and
column styles). Measured before the change (`SheetLayoutScaleTests`):

| Case | Before | After |
|---|---|---|
| hide, show and hide again 1,000,000 rows | 100 MB retained | one run, < 4 MB, well under 3 s |
| `RowOffset` on 100,000 alternating heights, 28,600 calls | 11.9 s | milliseconds (prefix sums cached by revision) |
| a style on every row | 192 MB | one run |

- New `RunLengthMap<T>` and `RunLengthSet` (`src/PulseOffice.Sheets/RunLengthMap.cs`): sorted, coalesced runs with
  binary search, `Set`/`Remove` on ranges, `Insert`/`Delete` for structural edits, `Segments` for walking gaps and runs.
  They still implement `IReadOnlyDictionary<int, T>` so readers keep working; writers and hot paths use `Runs`.
- `SheetLayout` rebuilt on runs: range operations `HideRows`, `ShowRows`, `HideColumns`, `ShowColumns`,
  `SetRowHeightRange`, `SetColumnWidthRange`, `SetAutomaticRowHeights`, `SetRowStyles`, `SetColumnStyles`;
  `InsertRows`/`DeleteRows`/`InsertColumns`/`DeleteColumns` replace `RemapDerivedRows/Columns` and the per-index
  snapshot-reset-reapply in `SheetStructure.LayoutPart` (which on 1M hidden rows reset and re-set a million entries).
  `ShapeSignature()` hashes runs; `RowAutoHeightTracker` used to hash every entry on every flush.
- `RowAutoHeight.Refresh` walks row-style runs and measures each style once (it measured per row).
- `SheetEditGuard` sums run lengths instead of filtering every styled row.
- `SheetSnapshot` keeps a copy of the whole layout: before it lost automatic heights, kept sizes of hidden rows and the
  default sizes, so undoing a structural or checkpointed edit after «set all row heights» left the new default (medium).
- Outside the area, minimum needed: `LedgerFillCommands` and `SheetFacade` hide and show by range; the ODS reader reads
  repeated collapsed/sized rows and columns as one range — **it stopped at 4,096 repetitions** (`MaxContentRepeat`), so a
  file with 10,000 repeated hidden rows came back with only the first 4,096 hidden (medium, fixed); the ODS writer
  writes consecutive layout-only rows with the same shape as one repeated `table:table-row` (it wrote one element per
  hidden row). Test: `OdsHiddenRowRunsTests`.
- `MergedRegions` already has a spatial index (round 1); not touched.
- XLSX has no row repetition: a million hidden rows are still a million `<row>` elements, as in Excel.

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets/SheetLayout.cs | 328/328 | rewritten on runs |
| src/PulseOffice.Sheets/Formatting/RowAutoHeight.cs | 281/281 | signature and row styles by runs |
| src/PulseOffice.Sheets.Engine/Operations/SheetStructure.cs | layout part | layout part delegates to SheetLayout |
| src/PulseOffice.Sheets.Engine/Operations/SheetSnapshot.cs | 140/140 | full layout copy |
| src/PulseOffice.Sheets/SheetEditGuard.cs | 47/47 | runs |

### 2. SUBTOTAL/AGGREGATE and hidden rows — closed

Hiding or showing rows did not recalculate `SUBTOTAL(10x)`/`AGGREGATE` (volatile since round 1, so only the next edit
did). Now `SheetLayout.RowVisibilityChanged` fires when the set of hidden rows actually changes (hidden runs compared
before and after each recomposition, default height going to or from zero, layout copies on undo), `Worksheet` and
`Workbook` relay it, and `CalculationEngine` recalculates volatile cells and their dependents. Bulk operations
(collapse/expand/show level of groups in `SheetOutline`, autofilter in `GridViewport.ApplyRowFilter`) wrap their
per-row changes in `DeferRowVisibilityNotifications()`, so a collapse is one recalculation, not one per row.
Test: `SubtotalVisibilityRecalculationTests` (2 of 3 fail without the subscription).

### 3. Change log and sheet indexes — closed

`TrackedChange.SheetIndex` did not follow sheets (round 1: TrackedChanges.cs:12), and did not follow rows and
columns either. Now `Workbook.ShiftIndexed` (insert, move) also calls `ChangeLog.ReindexSheets`; removing a sheet
detaches its changes together with its external data and database ranges, and re-inserting the same sheet (undo)
reattaches them at the new position. `SheetStructure` has a `TrackedChangesPart` (also for cell-band shifts) that moves
changes with their cells and drops those of deleted cells. `ChangeLog.At`, called for every painted cell, was a
linear scan: now a lazily rebuilt (sheet, address) → pending change lookup, invalidated by every mutation.
The file formats are unaffected: the index is still the position at write time. Tests: `ChangeLogSheetIndexTests`,
`TrackedChangesStructureTests`. Also: `RemoveSheet` now unhooks the row-visibility relay.

### 4. Multiple operations without the 512 MB stack — closed

`CalculationEngine.EvaluateSubstituted` started a new thread with a 512 MB stack for every substituted evaluation
(every cell of a data table, every `MULTIPLE.OPERATIONS`, every step of goal seek), because `SubstitutingContext`
evaluated precedent formulas recursively, up to 100,000 levels. Now the engine computes, from the dependency graph,
the formula cells that are both dependents of the substituted inputs and precedents of the target (forward closure
from the inputs, backward walk from the target restricted to it, ranges matched against the candidate cells), orders
them topologically and `SubstitutingContext.Prepare`s them dependencies first: each evaluation finds its precedents
already computed, so the depth stays one. References the graph cannot see (INDIRECT, OFFSET) still evaluate lazily, now
bounded by 256 levels and `RuntimeHelpers.TryEnsureSufficientExecutionStack`, with `#VALUE!` beyond. The thread is gone.
Tests: `SubstitutedEvaluationRunsOnTheCallingThread` (a WEBSERVICE stub records the thread: before it was the worker),
`ALongChainEvaluatesOnASmallStack` (50,000 chained formulas on a 256 KB thread).

### 5. Formatting whole rows on sheets with formatted columns — closed as far as the cell model allows

`SelectionWorkArea.Plan` returned one address per cell in a `HashSet` (then sorted), one index per selected row or
column, and took the row and column styles as one index per styled row: formatting rows 2…1,048,576 built a million-entry
row list, a million `SetRowStyle` calls and a million undo actions, plus a million cells per styled column; with a row
style on every row, formatting a whole column walked a million rows. Now the plan is made of `CellAreas` (disjoint
rectangles: the used area clipped on both axes, plus the intersections of the selection with styled row/column runs
minus that area) and `IndexSpan`s; `SheetFormattingService` applies row and column defaults per style run (split at the
selection edges, so outline borders still see first, interior and last), records all changed cells in one undo action,
and `ToggleOnSelection` reads styles by runs. Also `Bound` now clips both axes to the used area (whole rows used to keep
every selected row inside the used columns: rows 2…end with data in ten columns meant ten million cells).
Still inherent: the intersection of whole rows with a styled column is a set of explicit cells (row styles win over column
styles, as in Excel, which also writes those cells); avoiding it needs a per-column band style layer in the model and in
both file formats — left for a dedicated block. Tests: `SelectionWorkAreaTests.AlmostAllWholeRowsWithStyledColumnsPlanAreasNotCells`,
`SheetFormattingTests.FormattingAlmostAllWholeRowsStaysOneRunAndUndoesInOneStep`.

## Regressions of round 1

- **SUBTOTAL and AGGREGATE volatile** (serious, performance; CalculationEngine.cs `VolatileFunctions`): round 1 made
  them volatile so that hiding rows would eventually be reflected; the cost was that every edit anywhere recalculated
  every SUBTOTAL/AGGREGATE and its dependents. 2,000 `SUBTOTAL(109;A$1:A$2000)` and 100 unrelated edits took **144 s**.
  Now they are *visibility-sensitive* instead (`_visibilitySensitive`), recalculated only when the hidden rows change
  (point 2): the same test takes milliseconds. Test `UnrelatedEditsDoNotRecalculateEverySubtotal` (fails before).

## Still open

- Whole rows × styled columns still materialise explicit cells (see point 5): needs a band style layer in the model and in
  both formats.
- `ConditionalFormattingEvaluator` re-reads and re-sorts the rule range for every painted cell (round 1, serious,
  performance): `Worksheet.Revision` now exists and could key a cache; not done in this round.
- `SheetOutline` keeps one `HiddenSizes` entry per row of a collapsed group and expands with `SetRowHeight`, which turns
  automatic rows manual (minor).
- `RangeDependencyIndex.Drop` is linear in the bucket (removing many large-range formulas is quadratic; evaluation of
  such formulas is already of the same order) (minor).
- XLSX writes one `<row>` per hidden row (the format has no repetition).

## Tests run (one project at a time)

Sheets.Tests 337, Sheets.Engine.Tests 1231, Sheets.UI.Tests 353, Formats.Odf.Tests 736, Formats.OpenXml.Tests 2127
(one unrelated timing failure in `PptxDeepAuditTests` under load), Architecture.Tests 38.

## Live checks for the coordinator

- Hide rows 2…1,048,576 (select from row 2, Ctrl+Shift+Down, hide), scroll, unhide; save as ODS and XLSX, reopen.
- With `=SUBTOTAL(109;A1:A10)`: hide a row, collapse a group, apply an autofilter: the total updates at once.
- Enable change recording, edit a cell on sheet 2, move sheet 2 to the end, insert rows above the edit: the change
  marker follows the cell; delete the sheet and undo.
- Data table / multiple operations over a long chain of formulas.
- Bold on rows 2…end on a sheet with a formatted column; undo in one step.
