# Deep audit round 28: chart format mappers

Scope: the chart mappers that round 27 (`charts-round27.md`) did not read, plus its two open minor points.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Formats.OpenXml/Charts/ChartSpaces.cs | 468/468 | fixed: bubble names and colours misaligned; inline explicit names not written; linked titles lost |
| Formats.OpenXml/Charts/ChartFamilies.cs | 484/484 | ok |
| Formats.OpenXml/Charts/InlineChartParts.cs | 40/40 | ok (uses the fixed builder and reader) |
| Formats.OpenXml/XlsxChartMapper.cs | 169/169 | open: series on several sheets are united as if on one (see Open) |
| Formats.OpenXml/PptxDeckChartMapper.cs | 49/49 | ok |
| Formats.Odf/OdfChartMapper.cs | 102/102 | ok |
| Formats.Odf/OdfDeckChartMapper.cs | 54/54 | ok |
| Formats.Odf/Charts/ChartDocuments.cs | 298/298 | fixed: inline values beyond the categories lost, missing values written as 0 |
| Formats.Odf/Charts/ChartReading.cs | 247/247 | ok (minors in Open) |
| Charts/ChartFamilies.cs | 72/72 | ok |
| Sheets.Pivot/PivotRenderer.cs | 141/141 | no chart code; minor in Open |
| UI.Controls/Charts/ChartLegendPainter.cs | 98/98 | fixed: flow legends laid out every label three times per paint |
| Sheets.UI/LedgerChartCommands.cs | edit path | fixed: protection checked only after the dialog |

## Defects

### Grave
- Formats.Odf/Charts/ChartDocuments.cs:276 (`InlineChartDocumentBuilder.Supplement`) — the local table had one row per
  category: a Deck or Folio chart whose data has no categories (a PPTX/DOCX without `c:cat`, which is optional) lost
  all its values when saved to ODP/ODT; values beyond the last category were dropped too. The table now has as many
  rows as the longest of categories and series. Test `IValoriSenzaCategorieSopravvivonoAllOdp` (failed before).

### Medium
- Formats.OpenXml/Charts/ChartSpaces.cs:157 — inline charts (PPTX, DOCX) wrote the data name instead of the explicit
  series name: names set in the chart editor were lost in OOXML. Test `IlNomeEsplicitoDiUnaSerieInLineaSiScriveInPptxEDocx`.
- Formats.OpenXml/Charts/ChartSpaces.cs:384 — a bubble series is one DrawingML element but three model series (X, Y,
  size): explicit names and colours were read one per element, so the Y name and colour landed on X. `ModelSeriesSlots`
  maps elements to model slots as `Literals` already did. Test `INomiEIColoriDiUnaBollaRestanoSulleSerieGiuste`.
- Formats.Odf/Charts/ChartDocuments.cs:279 — a missing value was written as 0 instead of an empty cell (a gap became a
  zero bar). Test `UnValoreMancanteResta_un_buco_e_non_diventa_zero_nell_Odp`.
- Formats.OpenXml/Charts/ChartSpaces.cs:391 — chart and axis titles linked to a cell (`c:tx/c:strRef`, as other
  suites write them) were read as no title. The cached text is read now. Test `IlTitoloCollegatoAUnaCellaSiLeggeDallaCache`.

### Minor
- Sheets.UI/LedgerChartCommands.cs:194 — `ledger.chart.edit` opened the dialog on a protected sheet and refused only
  after it closed. Checked before and after now (`RefusesObjects`). Test `Sheets.UI.Tests/DeepAuditRound28ChartTests`.
- UI.Controls/Charts/ChartLegendPainter.cs — top/bottom legends measured each label three times per paint; lines are
  now measured once and carry their widths (`MeasuredItem`). Performance only, no test.

## Open
- Closed on 2026-11-05 by decision 25 (chart-series-sources.md). XlsxChartMapper.cs:68 (`SheetReferenceUnion`) — a foreign XLSX chart whose series refer to different sheets is read
  as one range on the first sheet. Our writer never produces it; it needs a model with per-series sources or a declared
  loss code (left to the coordinator: no code range was assigned).
- ChartReading.cs:220 — inline header row ignores `number-columns-repeated`; fallback series name "Serie" is not
  localised (the XLSX reader says "Series").
- ChartReading.cs:14 — only the first paragraph of a multi-paragraph ODF title is read.
- PivotRenderer.cs:138 — `Measure` with no rows or columns reports one row/column beyond the headers.

## Tests run
- Formats.OpenXml.Tests, filter Chart|Pptx|Docx|DeepAuditRound28: 858 passed.
- Formats.Odf.Tests, filter Chart|Odp|Deck|Parity: 214 passed.
- Sheets.UI.Tests, filter Chart|DeepAuditRound28: 6 passed.
- UI.Controls.Tests compiled (no chart tests there).

## Live checks
- Deck: insert a chart, rename a series in the chart editor, save as PPTX and reopen: the name stays.
- Deck: open a PPTX whose chart has no categories, save as ODP, reopen: the bars are there.
- Ledger: protect the sheet, select a chart, Edit the chart…: refused at once, no dialog.
- Any module: a chart with the legend at the bottom and many series lays out correctly.
