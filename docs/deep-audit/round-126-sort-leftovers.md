# Round 126: sort leftovers, subtotals and consolidate

Follows round 122 (docs/deep-audit/round-122-ledger-sort.md). The branch was fast-forwarded to `work` first. Every
defect got a test first; tests that needed new API were compiled against empty stubs and seen red before the fix
(the subtotal ordering test was also run red against the fixed code with the fix taken out).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets/SortSettings.cs | all | orientation and `SheetRangeSort` added |
| src/PulseOffice.Sheets.Engine/Operations/SheetSort.cs | all | left-to-right sort (axis strategy), `SortLists` locale |
| src/PulseOffice.Sheets.Engine/Operations/SortSettingsMemory.cs | all | plain-range memory on the sheet |
| src/PulseOffice.Sheets.Engine/Operations/FilterStructurePart.cs | all | plain-range sort follows structure edits; fields follow the sort orientation |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxSortStateCodec.cs | all | `columnSort`, plain range |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTableFilters.cs | all | worksheet `sortState` read |
| src/PulseOffice.Formats.Odf/OdfSortCodec.cs, OdfDatabaseRangeMapper.cs | all | `table:orientation`, anonymous range for a plain sort |
| src/PulseOffice.UI.Controls/Sorting/SortLevelsView.cs, SortLevelsModel.cs | all | four minors fixed, direction option |
| src/PulseOffice.Sheets.UI/LedgerToolCommands.cs | 1-439 | sort and subtotals wiring |
| src/PulseOffice.Sheets.UI/DataDialogs/CustomSortOrders.cs | all | remembered orders offered |
| src/PulseOffice.Sheets.Engine/Operations/SheetSubtotals.cs | all | 3 fixed |
| src/PulseOffice.Sheets.Engine/Operations/SheetConsolidation.cs | all | 2 fixed |
| src/PulseOffice.Sheets.UI/DataDialogs/ConsolidateView.cs | all | clean |
| src/PulseOffice.Sheets.UI/LedgerDataDialogCommands.cs | all | clean (writes netted by the protection guard) |

## Decision: where a plain-range sort is kept

`Worksheet.RangeSort` (`SheetRangeSort`: range, header flag, `SortSettings`), one per sheet, as Excel's worksheet
`sortState` and the reference suite's unnamed sheet database range. A sort on an AutoFilter or database range stays on
its owner. XLSX writes the worksheet `sortState` (its `ref` excludes the header, so the header flag reads back as part
of the range); ODS writes the unnamed `__Anonymous_Sheet_DB__N` range with `table:sort`, shared with a standard filter
on the same range. When that unnamed range is taken by an AutoFilter or a filter on another range the settings are
lost in ODS: declared `ODF274`. `SortSettings.Orientation` (`LeftToRight`: field offsets count rows) crosses XLSX
`columnSort` and ODS `table:orientation="column"`; AutoFilters and tables only accept top-to-bottom.

## Defects fixed

1. Medium: worksheet-level XLSX `sortState`, `columnSort` and ODS orientation were not read; plain-range sorts were not
   remembered. Fixed as above; the sort engine and dialog gained left-to-right. Tests: `Round126SortTests`,
   `Round126SortStateTests`.
2. Minor (decision 17): `SortLists.ContainingAll` used the current culture; now the injected locale.
3. Minor: SortLevelsView: Alt+= / Alt+- add and remove a level (Mac has no Insert); duplicate columns disable the
   confirm button; a remembered custom order matching no offered one is offered by its items
   (`ILocaleSortOrders.Including`); widths derive from the font size metric. Tests: `Round126SortLevelsTests`,
   `Round126SortOrderTests`.
4. Medium: subtotals put the grand total row above the last group's subtotal (both inserted after the same row).
   The insertion moved into the engine (`ISheetSubtotals.Apply`) and the grand total goes in first. Labels now go in
   the group column. Test: `Round126SubtotalsTests`.
5. Minor (decision 17): subtotal group keys and consolidation labels compared with the thread culture; now the locale.
6. Medium: consolidation scanned whole source ranges (whole columns: hundreds of millions of reads, also on every
   preview refresh); sources are clipped to the used area.

## Open

- Consolidation settings are not part of the model: ODS `table:consolidation` and XLSX `dataConsolidate` are neither
  read nor written, so the dialog never reopens with the last settings.
- Subtotals: one value column only, no "replace current subtotals", no outline groups.
- Moving cells by drag does not carry `RangeSort`.

## To verify live

- Sort dialog, "Sort left to right": rows are offered, columns reorder; reopen the dialog: same settings.
- Sort a plain range, save ODS and XLSX, reopen in both and in Excel (Data, Sort shows the levels).
- Subtotals on a grouped list: the grand total is the last row; labels in the group column.
