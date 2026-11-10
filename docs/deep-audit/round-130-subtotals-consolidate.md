# Round 130: outline summary position, nested subtotals, linked consolidation, undoable consolidation settings

Closes the four open points of round 129 (docs/deep-audit/round-129-subtotals.md). The worktree was fast-forwarded to
`work` first (already up to date).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Engine/Operations/SheetSubtotals.cs | all | restructured: nested group columns, line-based layout, sets the sheet's summary position |
| src/PulseOffice.Sheets.Engine/Operations/SheetOutline.cs | all | reused by the gutter (collapse, expand, show level) |
| src/PulseOffice.Sheets.Engine/Operations/SheetConsolidation.cs | all | restructured: buckets keep their source cells; link to source data gives formulas |
| src/PulseOffice.Sheets.Engine/Operations/SheetSnapshot.cs | all | summary position and consolidation settings captured and restored |
| src/PulseOffice.Sheets.Engine/Operations/WorkbookCheckpoint.cs | all | consolidation settings captured and restored |
| src/PulseOffice.Sheets.Engine/Operations/FilterStructurePart.cs | all | clean (drops areas a deletion removes; undo now restores them) |
| src/PulseOffice.Sheets/ConsolidationSettings.cs, Workbook.cs (consolidation) | all | clean |
| src/PulseOffice.Sheets/Worksheet.cs, CellComment.cs (outline types), WorkbookCopier.cs | outline parts | `OutlineSummary` added and copied |
| src/PulseOffice.Sheets.Engine/Operations/SheetDuplicator.cs | copy | copies the summary position |
| src/PulseOffice.Sheets.UI/GridGeometry.cs, GridParts/GridViewport.cs | all | gutter widths inside the header bands; gutter hit-testing |
| src/PulseOffice.Sheets.UI/GridParts/Painting/GridHeaderPainter.cs | all | numbers and letters drawn beside the gutter |
| src/PulseOffice.Sheets.UI/GridParts/Input/GridPointerInput.cs | press | gutter buttons answered before any other gesture |
| src/PulseOffice.Sheets.UI/GridParts/GridNotifications.cs, SheetGridView.cs, SheetGridParts.cs | events, factory | `OutlineRequested` |
| src/PulseOffice.Sheets.UI/LedgerViewParts/LedgerCellOperations.cs, LedgerView.cs | resize, outline | gutter clicks are one undo step each |
| src/PulseOffice.Sheets.UI/LedgerToolCommands.cs | subtotals | two "Then by" group columns |
| src/PulseOffice.Sheets.UI/LedgerDataDialogCommands.cs, DataDialogs/ConsolidateView.cs, SheetValueWrites.cs | consolidate | link offered, remembered, written as formulas |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/SheetSections.cs, XlsxReader.cs | sheet properties | `sheetPr/outlinePr` |
| src/PulseOffice.Formats.Odf/OdsWriter.cs, Spreadsheet/SheetPartReaders.cs | table element | summary position attributes |

## Decisions

- **Summary position is sheet state** (`Worksheet.OutlineSummary`: `Below`, `Right`, both true by default), part of the
  sheet snapshot (so subtotal undo restores it), copied with the sheet. XLSX writes `sheetPr/outlinePr` with
  `summaryBelow="0"`/`summaryRight="0"` only when not default, between `tabColor` and `pageSetUpPr`, and reads it back.
  ODF has no attribute for it and the reference suite keeps no such setting in its spreadsheet files, so ODS writes our
  own `summary-below="false"`/`summary-right="false"` in the `urn:pulseoffice:outline` namespace on `table:table`;
  other readers ignore it and nothing is lost between our own files, so no loss code is needed. The subtotals command
  sets `Below` from "Summary below the data" when it builds an outline.
- **Outline gutter**: the grid had no outline buttons at all. `IOutlineGutter` (`PulseOffice.Sheets.UI.Outline`, no
  Avalonia types) computes the gutter thickness (one 14 px slot per level plus one), the level buttons (1 to depth+1, in
  the header corner) and one toggle per group, placed after the group (`Last + 1`) with the summary below or right, and
  before it (`First - 1`) with the summary above or left (after the group when it starts at the first row or column).
  It answers hit tests and presses: a toggle collapses or expands its group, level button n shows levels below n. The
  gutter lives inside the header bands (`GridGeometry.RowGutterWidth`, `ColumnGutterHeight`), so every other geometry
  computation is unchanged. `OutlineGutterPainter` draws brackets, plus/minus boxes and level numbers. Each click is one
  undo step ("Show or hide details", `history.outline.details`) restoring layout and groups; refused on a protected sheet
  as the outline menu commands are.
- **Nested subtotals**: `SubtotalRequest.ThenGroupColumns` adds inner group columns (`GroupColumns` = outer first). The
  plan is now a list of output lines: each run at level n emits its summary (above or below) around the runs of level
  n+1, recursively; the grand total wraps everything. Rows are inserted in ascending final position, then labels (in each
  level's own group column, or the first column when it is a value column) and `SUBTOTAL` formulas spanning the group's
  content lines, inner totals included, as the reference spreadsheets do (SUBTOTAL ignores nested SUBTOTAL). Outline
  groups: grand total level 1, then one level per group column. Page breaks separate only the outermost groups. Replace
  removes every row holding a `SUBTOTAL(` formula, so all levels go. The dialog offers two "Then by" columns. Undo is the
  existing single snapshot step.
- **Consolidation link to source data**: offered in the dialog (remembered with the other settings). When on, each
  result cell becomes a formula applying the chosen function (SUM, COUNTA, AVERAGE, MAX, MIN, PRODUCT, COUNT, STDEV,
  STDEVP, VAR, VARP) to absolute references of the contributing source cells, sheet-qualified; labels stay values. The
  reference suite instead inserts hidden detail rows with one reference per source and an outline; we write the
  references directly in the result formula, which keeps the destination layout identical to the unlinked result. The
  formulas cross both formats as ordinary formulas, and the flag already crossed both (round 129).
- **Consolidation settings are undoable structure state**: they are in `WorkbookCheckpoint` (row and column insert and
  delete, sheet commands) and in `SheetSnapshot` (sort, subtotals, value writes), so undo and redo restore them exactly.

## Defects fixed

1. Medium: undoing a row or column deletion left a consolidation source area dropped by the deletion
   (`WorkbookCheckpoints.Restore` ignored `Workbook.Consolidation`). Tests
   `Round130ConsolidationUndoTests.Restoring_a_checkpoint_brings_back_a_source_area_a_row_deletion_dropped` and
   `Restoring_a_checkpoint_clears_settings_created_after_it`, both seen red before the fix.
2. Medium: the same through sheet snapshots (structure changes made by subtotals, sorting and other snapshot-undone
   commands). Test `Restoring_a_sheet_snapshot_brings_back_a_source_area_a_row_deletion_dropped`, seen red before the fix.
3. Medium (rule 7): summary position not in the model nor in XLSX or ODS. Tests `Round130OutlineSummaryTests`.
4. Medium: no outline gutter in the grid, so outline buttons could not honour anything. Tests
   `Round130OutlineGutterTests`.
5. Medium: one group column only. Tests `Round130NestedSubtotalsTests`.
6. Medium: link to source data not offered nor computed. Tests `Round130ConsolidationLinkTests` and the dialog test in
   `LedgerAnalysisDialogsTests`.

Points 3 to 6 are new API and were written with it, so they were not run red against the old code.

## Open

- Gutter toggles in the frozen band are drawn with the pane offset, but the frozen and scrolled parts of the gutter
  share one clip; a toggle of a group crossing the freeze line may overlap the band edge.
- The linked consolidation does not build the reference suite's hidden detail rows.

## To verify live

- Data, Subtotals on a list with two text columns: group by the first, then by the second; untick "Summary below the
  data": totals above, the gutter shows three levels plus details, the plus/minus buttons sit on the total rows above
  each group; collapse and expand, undo each click; level buttons 1 to 4 in the corner. Save XLSX and ODS, reopen:
  buttons still above. In Excel the buttons are above too.
- Apply again with "Replace current subtotals" and no "Then by": every nested total goes.
- Data, Consolidate with "Link to source data": result cells hold `=SUM('Sheet'!$B$2;…)`; change a source, the result
  follows; reopen the dialog: the box stays ticked.
- Set up a consolidation, delete rows covering one source area, undo: reopening the dialog shows the area again.
