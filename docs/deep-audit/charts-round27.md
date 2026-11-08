# Deep audit round 27: charts

Area: chart creation and editing in the three modules (chart picker, chart settings, pivot charts, Ledger sparklines),
the model in `src/PulseOffice.Charts` and the plots in `src/PulseOffice.UI.Controls/Charts` not reread in round 9
([shared-round9.md](shared-round9.md)), and the chart round trip through ODS/XLSX/ODT/DOCX/ODP/PPTX.

Note on scope: the chart model has no user-set axis bounds, logarithmic scales, unit intervals or trend lines
(`ChartDefinition`, `ChartSettings`), so those classes of defect cannot arise from the model; scales are always
computed from the data by `PlotScale.Nice`.

## Coverage

| File | Lines read/total | Outcome |
|---|---|---|
| UI.Controls/Charts/Plots/ChartPlot.cs | 112/112 | ok |
| UI.Controls/Charts/Plots/LinePlot.cs | 195/195 | ok |
| Charts/PlotScale.cs | 103/103 | ok (reread) |
| UI.Controls/Charts/ChartAxisPainter.cs | 80/80 | ok |
| Charts/ChartDefinition.cs, ChartSettings.cs | 170/170 | ok |
| UI.Controls/Charts/ChartRenderer.cs | 192/192 | ok (reread) |
| Sheets.Engine/Operations/ChartDataExtractor.cs | 173/173 | fixed: whole-column and whole-row ranges read every row/column of the sheet |
| Sheets.UI/LedgerChartCommands.cs | 235/235 | ok |
| UI.Controls/Charts/Plots/ScatterPlot.cs | 104/104 | ok |
| UI.Controls/Charts/Plots/RadarPlot.cs | 135/135 | ok |
| UI.Controls/Charts/Plots/MeterPlot.cs | 184/184 | ok |
| UI.Controls/Charts/Plots/FinancialPlot.cs | 86/86 | ok (gaps are zero-filled by the renderer before it is called) |
| UI.Controls/Charts/ChartLegendPainter.cs | 98/98 | ok (minor: flow legends lay out every label three times per paint) |
| UI.Controls/Charts/ChartEditor.cs, ChartPicker.cs, ChartPreview.cs, EmbeddedChartPrompt.cs, ChartText.cs | 227/227 | ok |
| UI.Controls/Charts/ChartPickerView.cs | 538/538 | ok |
| Sheets.UI/SheetChartImages.cs, GridParts/GridObjectRenderers.cs | 76/76 | ok (charts are extracted at every paint: now bounded by the fix above) |
| Sheets/Sparklines.cs | 97/97 | ok (line width bounds moved here, shared by both mappers) |
| Sheets.Engine/Operations/SparklineData.cs | 66/66 | fixed: «same scale» re-read the whole group for every sparkline (open since the sheets-engine round) |
| Sheets.UI/SparklinePainter.cs, SparklineBars.cs | 172/172 | ok |
| Formats.Odf/OdfSparklineMapper.cs | 132/132 | ok |
| Formats.OpenXml/XlsxSparklineMapper.cs | 130/130 | fixed: line weight read without upper bound or finiteness check (ODS clamps) |
| Sheets.UI/LedgerSparklineCommands.cs | 150/150 | fixed: sparklines inserted and deleted on a protected sheet |
| Sheets.UI/LedgerProtectionGuard.cs | 77/77 | fixed: sparkline groups were not part of the protected state |
| Sheets.UI/CellFormat/SparklineDialogView.cs | 184/184 (texts and fields) | ok |
| Presentations.UI/ObjectDialogs/ChartEditDialog.cs, SlideChartData.cs | 61/61 | restructured: inline chart data moved to the shared `UI.Controls/Charts/InlineChartData.cs` |
| Documents.UI/Commands/FolioObjectInsertCommands.cs, FolioObjectDialogs.cs, Editing/FolioObjectEditing.cs (interface and `Replace`) | 400/535 | fixed: Folio could insert a chart but never edit it |
| Formats.Odf/Charts/ChartReading.cs | 120/247 | ok (every `ChartDefinition` property is read; pie colours are per point by design) |

Not read in this round (left for the next one): `Formats.OpenXml/Charts/ChartSpaces.cs`, `ChartFamilies.cs`,
`XlsxChartMapper.cs`, `Formats.Odf/Charts/ChartDocuments.cs`, `OdfChartMapper.cs`, `OdfDeckChartMapper.cs`,
`PptxDeckChartMapper.cs`, `Sheets.Pivot/PivotRenderer.cs` (pivot charts). A property census shows every
`ChartDefinition` property written and read by both mappers; the existing chart and parity tests (1,148) pass.

## Defects

### Serious
- Sheets.Engine/Operations/ChartDataExtractor.cs:64 — a chart over whole columns (or whole rows) read every row of the
  sheet: 1,048,576 categories and values per series, with the cell presenter run for each category, at every grid
  paint, in the chart dialog preview at every keystroke and when copying the chart. The range is now bounded to the
  used area through the shared `ISelectionWorkArea` (injected; the used area is computed only for whole ranges).
  Tests `Sheets.Engine.Tests/DeepAuditRound27ChartTests` (three, all failed before).

### Medium
- Sheets.Engine/Operations/SparklineData.cs:32 — with «same scale» every sparkline re-read the whole group: n² cell
  reads per paint (3,000 sparklines of 20 cells: 11 s). The group scale is now cached per group and sheet revision.
  Tests `LaScalaComuneDiUnGruppoDiSparklineNonRileggeIlGruppoPerOgniSparkline` (failed before: 11 s),
  `LaScalaComuneSegueIValoriCambiati`.
- Sheets.UI/LedgerSparklineCommands.cs:122,127 and LedgerProtectionGuard.cs:30 — sparklines could be inserted into and
  deleted from a protected sheet (by command, macro or agent): the guard did not compare sparkline groups. Both the
  command and the guard now refuse with `status.protection.objects`. Test
  `Sheets.UI.Tests/DeepAuditRound27SparklineTests` (failed before).
- Documents.UI — a Folio chart could not be edited after insertion (Ledger and Deck have it; rule 10). New
  `FolioChartCommands` (`folio.chart.edit`, menu Format › Object and object context menu, five languages) runs the
  shared `IChartEditor` with the shared `IInlineChartData` (moved from Deck, now used by both) and replaces the object
  through `IFolioObjectEditing.Replace`, so read-only, locked objects and protection are respected and it is one undo
  step. Test `Documents.UI.Tests/DeepAuditRound27ChartTests` (did not compile before: the command did not exist).

### Minor
- Formats.OpenXml/XlsxSparklineMapper.cs:122 — the XLSX line weight was read without upper bound or finiteness check
  (ODS clamps to 0.01–100 pt): an infinite pen. The bounds now live on `SparklineStyle` and both mappers use them.
  Test `Formats.OpenXml.Tests/DeepAuditRound27ChartTests` (three cases, all failed before).

## Open
- Charts in Folio and Deck have no data editor: their inline data can only be the sample data (a functional gap, not
  a regression).
- Legends at top or bottom lay out every label three times per paint (minor, performance).
- `ledger.chart.edit` checks protection only after the dialog closes (minor, UX).

## Live checks
- Ledger: select columns A:B with ten rows of data, insert a column chart: it shows ten categories, scrolling stays
  smooth; open the chart dialog and type in the range field.
- Ledger: a column of 1,000 sparklines with «same scale»: smooth scrolling; change a value and the scale follows.
- Ledger: protect the sheet, then insert or delete sparklines: refused with the protection message.
- Folio: insert a chart, right-click it › Edit Chart…, change type and title, Apply, then ⌘Z.
- Deck: edit a chart as before (shared data source).
