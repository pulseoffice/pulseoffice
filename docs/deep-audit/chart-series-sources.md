# Chart series sources (decision 25)



## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Charts/ChartDefinition.cs | 95/95 | ChartSeriesSource added |
| Sheets.Engine/Operations/ChartDataExtractor.cs | 196/196 | per-series path |
| Formats.OpenXml/XlsxChartMapper.cs | 169/169 | to change |
| Formats.OpenXml/Charts/ChartSpaces.cs | 468/468 | to change |
| Formats.Odf/Charts/ChartDocuments.cs + ChartReading.cs | read | to change |
| Sheets.Engine/Operations/ChartSeriesSources.cs | new | references, compact/per-series layout, per-series reader |
| Sheets.Engine/Operations/ChartReferenceUpdates.cs | new | structure, rename, removal rewrites; object memento |
| Sheets.Engine/Operations/SheetStructure.cs, SheetCellShift.cs, SheetRenaming.cs | edited | charts relinked |
| Sheets.UI/LedgerSheetCommands.cs, LedgerStructureCommands.cs, Scripting/DocumentFacade.cs | edited | #REF! on removal, one-step undo |
| Formats.OpenXml/XlsxChartMapper.cs, Charts/ReferencedSeriesSource.cs | edited/new | per-series c:ser |
| Formats.Odf/Charts/ChartDocuments.cs, ChartReading.cs, SeriesSourceAddresses.cs, OdfChartMapper.cs | edited/new | per-series chart:series |
| UI.Controls/Charts/ChartSeriesRanges.cs (new), ChartPickerView.cs, ChartPicker.cs, ChartEditor.cs; Charts/ChartSettings.cs; Localization 5 files | edited | per-series editor |

## Design

- Model: `ChartSeriesSource(Name, Values, Categories)` and `ChartDefinition.SeriesSources`. References are canonical
  sheet-qualified A1 text (`'Sud Est'!B2:B4`); `#REF!` marks a broken one. Whole columns/rows (`B:B`, `2:2`) are read too
  and bounded by the used area when extracted, as in round 27. The compact form stays when `SeriesSources` is null.
- `ChartSourceLayout.Expand` gives the per-series references a compact chart implies (the same ones the writers emit);
  `Settle` keeps the compact form only when the read references equal that expansion (names and categories compared only
  when read), so our own files stay compact and foreign multi-sheet or non-contiguous charts become per-series.
- Extraction (`SeriesSourceReader`): each series reads its own range, at most 100 000 points, hidden rows/columns skipped
  for the visible-only view; categories come from the first series that has them, otherwise ordinals.
- Structure (`ChartLinksPart`, also in `SheetCellShift`): compact range and per-series references on the changed sheet
  move; whole-axis references stay; removed ranges become `#REF!`. `LedgerStructureCommands` now saves and restores the
  object placements of every sheet (`IObjectPlacementMementos`), so a chart on another sheet undoes in the same step.
- Sheets: `SheetRenaming` plans chart changes (compact `SourceSheet` too, which earlier stayed stale) and reverts them;
  `IChartSheetReferences.Removed` turns references to a deleted sheet into `#REF!` in the delete command and in the macro
  `RemoveSheet`, restored by undo.
- Editor: `ChartSeriesRanges` (UI.Controls, shared) lists one range per series under "Series ranges" with a reference
  picker; editing a series switches the chart to per-series, editing the whole range goes back to compact. Ledger passes
  the expansion and a normaliser that qualifies a bare range with the source sheet. Texts in five languages.
- Copy and paste: references are sheet-qualified, so a chart pasted on another sheet keeps its data; across workbooks the
  exchange still freezes the data inline (unchanged). Protection: unchanged path (`ledger.chart.edit` refuses on a
  protected sheet before and after the dialog).

## Defects found on the way
- Medium: renaming a sheet left compact charts pointing at the old name (empty chart). Fixed, test
  `RinominareIlFoglioDiUnGraficoCompattoNeSegueLaSorgente`.
- Medium: inserting or deleting rows/columns never moved chart ranges. Fixed, tests in `ChartSeriesSourcesTests`.
- Minor: the ODS reader enclosed ranges of every sheet in one rectangle on the first sheet (`CellRangeAddresses.Split`).

## Limits (declared, no format loss)
- Scatter series share the chart's X (categories) reference: a foreign chart whose scatter series have different X
  ranges keeps only the first one; this is a model limit, identical in ODS and XLSX.
- Multi-area references (`(A1:A3,C1:C3)`) in one series are not read as references: when the chart is otherwise
  per-series, that series comes back empty (as before this change, when it was ignored by the union).

## Tests (failing before where applicable)
- Formats.OpenXml.Tests `ChartSeriesSourcesFormatTests` (5): external XLSX with series on two sheets, XLSX, ODS and
  XLSX→ODS→XLSX round trips, compact form kept. With the settling disabled three of them fail (verified).
- Sheets.Engine.Tests `ChartSeriesSourcesTests` (9): extraction, whole column, row insertion, deletion to `#REF!`,
  renaming with revert, compact renaming, sheet removal with undo, expansion and settling.
- Sheets.UI.Tests `ChartSeriesSourcesCommandTests` (3): row insertion undone in one step for a chart on another sheet,
  sheet deletion to `#REF!` and back, editor receives per-series editing and normalises references.

## Runs
- Sheets.Engine.Tests full: 1422 passed. Formats.OpenXml.Tests (Chart|Xlsx|Parity|DeepAudit): 1406 passed.
  Formats.Odf.Tests (Chart|Ods|Parity|Foreign): 278 passed. Sheets.UI.Tests (chart, facade, protection, structure): 143
  passed. Architecture.Tests: 40 passed. Localization.Tests: 32 passed. Desktop.Tests CompositionTests: 24 passed.
- Corpus audit not run (optional).

## Live checks
- Ledger: open an XLSX whose chart takes series from two sheets: both series drawn with the right values.
- Ledger: Edit chart…, change one series' range to another sheet, apply, save as ODS and XLSX, reopen both.
- Ledger: insert rows above the data on the source sheet: the chart keeps its data; ⌘Z once restores everything.
- Ledger: rename and delete the data sheet: the chart follows the rename; after deletion that series is empty, ⌘Z
  brings it back.

Status: done (uncommitted).
