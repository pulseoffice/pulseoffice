# Deep audit, round 2: Ledger interface, drawing and charts

Area: `src/PulseOffice.Sheets.UI` (LedgerViewParts, Ledger* commands), Drawing, Charts, UI.Embedding, Exchange.

## Group 1: conditional formatting re-read the range for every painted cell (serious, open since round 1)

`ConditionalFormattingEvaluator` (`src/PulseOffice.Sheets.Engine/Operations/ConditionalFormattingEvaluator.cs`):
`RangeExtremes.Stops` and `RangeTest.Values` scanned every cell of the sheet (`NonEmptyCellsIn` filters and sorts the
whole dictionary), de-duplicated and sorted the range again for every cell evaluated. With 100,000 cells under a colour
scale, a data bar and a top-10 rule, the evaluator managed 184 cells in 10 seconds.

Fix: `ConditionalRangeSnapshots` (same folder) keeps, per worksheet (weak table) and per rule instance (reference
equality, rules are immutable records, so an edited rule is a new key), one `ConditionalRangeSnapshot` with the values,
the sorted numbers, the average and the duplicate counts. The whole cache of a sheet is dropped when
`Worksheet.Revision` changes (every cell write, recalculated values included, goes through the indexer that bumps it)
or when stale rule entries pile up. Rank, average, duplicate, colour scale, data bar and icon set all read the
snapshot; the top/bottom threshold is now an index into the sorted list instead of a sort per cell.

Tests: `tests/PulseOffice.Sheets.Engine.Tests/ConditionalFormattingPerformanceTests.cs` (100,000 cells with three
rules within 10 s, failed before with 184 cells; an edited value is seen by the next evaluation; a replaced rule is
not served from the old cache).

## Group 2: copying whole columns or the whole sheet walked every cell (serious)

`SheetRangeCapture.Grid` and `Portion` (`src/PulseOffice.Sheets.UI/Exchange/SheetRangeCapture.cs`) and the HTML
fragment in `LedgerExchange.CaptureCells` iterated the selection as given: ⌘C on whole columns built a million rows per
column, ⌘A ⌘C tried seventeen billion cells (memory exhaustion). The engine's `SheetClipboard.Copy` already bounded its
snapshot to the used area. `ISheetRangeCapture.Occupied` now gives the same bound (selection start kept, end clipped to
the last used row and column) and is used by the grid, the ODS/XLSX packages and the HTML fragment.

Test: `tests/PulseOffice.Sheets.UI.Tests/SheetRangeCaptureBoundsTests.cs` (whole columns: 3 rows captured, failed before
with 1,048,576; whole sheet bounded to the used area).

## Point evaluated and deferred: whole rows crossing formatted columns

Formatting whole rows when some columns carry a default style (`SelectionWorkArea.Plan`, `src/PulseOffice.Sheets/Addressing/SelectionWorkArea.cs:51-57`)
writes an explicit cell at every intersection, because `SheetLayout.DefaultStyleAt` lets the row style win over the column
style and the column style must survive on the cells of the formatted rows. The number of cells is bounded (rows selected
× formatted columns), and both file formats round-trip it, so it is a cost and not a loss. A proper banded layer is too
large for this round; precise proposal:

- Model: replace `SheetLayout._rowStyles` / `_columnStyles` with a `StyleBands` service holding ordered layers
  `(Band: Rows|Columns, Range, StyleId, Order)`; `DefaultStyleAt` resolves the last layer covering the cell, and a change
  of a row band over formatted columns adds a *derived* layer for the intersection (row range × column range) instead of
  cells. Undo stores the layer list (Memento).
- ODS: rows already carry `table:default-cell-style-name` per row and per column; intersections are written as repeated
  cells with `table:number-columns-repeated` inside the row (one element per column run, not per cell).
- XLSX: `<row s customFormat>` and `<col style>`; intersections need cells (`<c s>`) per intersection because OOXML has
  no band layer, so the writer expands only there.
- Effort: model, two readers, two writers, structural shifts (`SheetStructure`), snapshots and copy: estimated two days.

## Remaining from round 1, checked again

- `ConditionalRulesManagerView.cs:74,81-88`: async event handlers still let an exception from `EditAsync` reach the
  dispatcher (minor, not fixed).
- `LedgerTableLooks.cs:29`, `SheetImageInsertion.cs:29`, `SpreadsheetSnapshotProvider.cs:20`, `RandomView.cs:134-158`,
  `ShapeTextCellStyle.cs:63,76-97`, `SheetPageSetupView.cs:329`: unchanged, minor.
- `PrintFacade.ExportPdf`: fixed in the meantime (`ScriptCapability.FileSystemWrite` demanded).
- Painting loop (`GridCellPainter.cs`, read 231/231): every per-cell lookup is now constant time; the conditional
  evaluation, the only range-wide cost, is served by the snapshot cache.

## To test live

- A column of 100,000 numbers with colour scale, data bar and top 10: scrolling stays smooth; editing a value recolours.
- Select columns A:C or the whole sheet and copy: immediate, and pasting into Folio or another app gives only the used area.

Tests run: `PulseOffice.Sheets.Engine.Tests` 1227/1227, `PulseOffice.Sheets.UI.Tests` 354/354.
