# Round 129: subtotals parity, consolidation settings, moved sorts

Follows round 126 (docs/deep-audit/round-126-sort-leftovers.md), whose three open points this round closes. The branch
was fast-forwarded to `work` (round 126 included) first.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Engine/Operations/SheetSubtotals.cs | all | restructured: several value columns, per-column function, replace current, outline, page breaks, summary above or below |
| src/PulseOffice.Sheets.Engine/Operations/SheetOutline.cs | all | reused for subtotal outlines and for ungrouping on replace |
| src/PulseOffice.Sheets.Engine/Operations/SheetConsolidation.cs | all | clean |
| src/PulseOffice.Sheets.Engine/Operations/RangeMoveFilters.cs | all | 1 defect fixed (remembered sort not carried); consolidation settings follow moves |
| src/PulseOffice.Sheets.Engine/Operations/FilterStructurePart.cs | all | consolidation settings follow row and column insertion and deletion |
| src/PulseOffice.Sheets.Engine/Operations/SheetRangeMove.cs | 1-200 | parts list, edit record |
| src/PulseOffice.Sheets.Engine/Operations/ReferenceStructureParts.cs | 15-45 | page breaks follow structure edits (used as is) |
| src/PulseOffice.Sheets.Engine/Operations/SheetSnapshot.cs | fields | groups and breaks are in the snapshot, so subtotal undo restores them |
| src/PulseOffice.Sheets/Workbook.cs | all | consolidation settings reindexed on sheet insert, move, removal, restored with the sheet |
| src/PulseOffice.Sheets/SortSettings.cs | all | clean |
| src/PulseOffice.Sheets/WorkbookCopier.cs | 1-65 | copies the consolidation settings |
| src/PulseOffice.Sheets.UI/LedgerToolCommands.cs | subtotals and sort | new subtotal dialog fields |
| src/PulseOffice.Sheets.UI/LedgerDataDialogCommands.cs | all | settings remembered, dialog reopens with them |
| src/PulseOffice.Sheets.UI/DataDialogs/ConsolidateView.cs | all | accepts initial function and labels |
| src/PulseOffice.Formats.Odf/OdsWriter.cs, OdsReader.cs | spreadsheet body | `table:consolidation` written after the data pilot tables, read |
| src/PulseOffice.Formats.Odf/Tables/QualifiedRanges.cs | all | reused |
| src/PulseOffice.Formats.OpenXml/XlsxWriter.cs, XlsxReader.cs, XlsxFormat.cs | sections, sheet reader, losses | `dataConsolidate` after `sortState`; XLSX237 |

## Decisions

- **Consolidation settings live on the workbook** (`Workbook.Consolidation`, `ConsolidationSettings`: source areas by
  sheet index, destination sheet and cell, function, top-row and left-column labels, link to source data), one per
  workbook as in ODF. ODS writes `table:consolidation` (`table:function`, `table:source-cell-range-addresses`,
  `table:target-cell-address`, `table:use-labels` none/row/column/both, where "row" means labels in the left column,
  `table:link-to-source-data`). XLSX writes `dataConsolidate` on the destination sheet (`function`, `leftLabels`,
  `topLabels`, `link`, `dataRefs/dataRef ref sheet`); reading accepts `startLabels` as left labels and keeps the first
  sheet that carries one. XLSX has no destination cell: declared **XLSX237** (info) when it is not A1. The settings
  follow sheet insert, move and removal (sources on a removed sheet are dropped; a removed destination sheet drops the
  settings; reinserting the sheet brings back the previous settings), row and column edits and cell moves. Link to
  source data is kept through both formats but the dialog does not offer it yet. The consolidate command stores the
  settings in the same undo step as the written cells; the dialog reopens with them when the active sheet is the
  remembered destination.
- **Subtotals** (`ISheetSubtotals.Apply(engine, sheet, request)` now does the whole job): several value columns, each
  with its own function (`SubtotalRequest.ColumnFunctions`; the dialog offers all eleven SUBTOTAL functions per
  column), replace current subtotals (rows of the list holding a `SUBTOTAL(` formula are deleted, row groups in the
  list removed, page breaks inside it cleared), page break before every group after the first, summary below or above
  the data (above: grand total first, then each group's subtotal before its rows, as Excel does), and outline groups
  (level 1 around the list without the grand total, level 2 around each group's data rows). The label uses the
  function name when all columns share one, "Result" otherwise. Undo goes through the sheet snapshot, which holds
  groups and breaks. A single selected cell now expands to the current region, as for sorting. Nothing new crosses the
  formats: the result is ordinary cells, formulas, row groups and page breaks, which both formats already keep.

## Defects fixed

1. Medium: moving cells by drag left `Worksheet.RangeSort` behind (and kept a remembered sort that the move
   overwrote). `MovedFiltersPart.MoveRangeSort` carries it with the moved block, to another sheet too, and drops one
   the landing area overlaps. Tests `Round129SubtotalsTests.Moving_a_sorted_range_carries_its_remembered_sort` and
   `Overwriting_a_sorted_range_by_a_move_drops_its_remembered_sort`, both seen red before the fix.
2. Medium (rule 7): consolidation settings were neither in the model nor in ODS or XLSX. Fixed as above. Tests
   `Round129ConsolidationTests` (ODS and XLSX round trip, ODF vocabulary, XLSX237) and the sheet-index and structure
   tests in `Round129SubtotalsTests`.
3. Medium: subtotals had one value column and one function, no replace, no outline, no page breaks, no summary above.
   Fixed as above. Tests in `Round129SubtotalsTests`.

The tests for 2 and 3 cover new API and were written together with it, so they were not run red against the old code.

## Open

- The subtotal outline does not record "summary above" for the outline buttons (no such setting in the model; XLSX
  `outlinePr summaryBelow`): with totals above, the expand buttons still sit below each group.
- Subtotals offer one group column; nested levels come from applying the command again without "replace".
- Consolidation "link to source data" is kept in files but not offered or computed.
- Undo of a row deletion does not restore a consolidation source area the deletion dropped (the settings are only the
  dialog's memory; the cells are restored).

## To verify live

- Data, Subtotals: tick two columns with different functions; with outline on, the level buttons collapse to the
  totals; apply again with "Replace current subtotals": no duplicated total rows; untick "Summary below the data":
  totals above each group; page breaks show in the page break preview; undo restores the list in one step.
- Data, Consolidate: run it, reopen it on the same sheet: sources, function and labels come back; save ODS and XLSX,
  reopen both, open the dialog again; in Excel, Data, Consolidate shows the references.
- Sort a plain range, drag it elsewhere, open Sort: the remembered levels are there.
