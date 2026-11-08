# Deep audit round 58: chart, table, signature and small-caps locales

Scope: the "Remaining" list of [round-55-locale-leftovers.md](round-55-locale-leftovers.md). Same route as round 55:
the language travels in `ShapeSurroundings.Language` and becomes a culture through `ITextCultures`; Ledger uses the
spreadsheet locale (`ISpreadsheetLocale`, the culture of the cells).

## Coverage

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.UI.Controls/Charts/ChartText.cs | 21/21 | defect: labels and numbers with the OS culture; now built per render with a culture |
| src/PulseOffice.UI.Controls/Charts/ChartRenderer.cs | 192/192 | defect: no culture; new `Render(..., CultureInfo)`, per-render `ChartScene` (text, legend, axes) |
| src/PulseOffice.UI.Controls/Charts/ChartAxisPainter.cs, ChartLegendPainter.cs | 80/80, 99/99 | axis values and legend through the scene's `ChartText` |
| src/PulseOffice.UI.Controls/Charts/Plots/ChartPlot.cs, MeterPlot.cs, StatisticalPlot.cs, HierarchyPlot.cs, PiePlot.cs | label paths | defect: meter value and histogram bins with the OS culture; `ChartFrame.Culture` |
| src/PulseOffice.UI.Controls/Shapes/ShapeSurroundings.cs | 15/15 | carries the language (round 55) |
| src/PulseOffice.UI.Controls/Forms/SignatureLineRenderer.cs | 77/77 | defect: signing date with the OS culture; culture parameter |
| src/PulseOffice.Data.UI/DataTableRenderer.cs, TabularPreview.cs | 277/277, render path | defect: dates and numbers with the OS culture; culture parameter (import preview keeps the interface culture) |
| src/PulseOffice.UI.Embedding/ContentPainters.cs | all | defect: chart, table and signature painters ignored the surroundings language (Folio pages, print, PDF; Deck slides, exports) |
| src/PulseOffice.Sheets.UI/GridParts/Painting/GridObjectPainter.cs, GridObjectRenderers.cs | object painting | defect: Ledger charts and signature lines with the OS culture; spreadsheet locale |
| src/PulseOffice.Sheets.UI/SheetObjectRasterizer.cs, SheetPrintDocumentBuilder.cs | all | defect: printed/PDF Ledger charts with the OS culture; spreadsheet locale |
| src/PulseOffice.Documents/Controls/ContentControlEditing.cs | 257/257 | defect: date control without its own language fell back to the OS culture; now the document language |
| src/PulseOffice.UI.Controls/Text/Rich/TextTypesetting.cs | 414/414 | defect: small caps upper-cased with the OS culture; run language, then frame language |
| src/PulseOffice.Sheets.Engine/Operations/ChartDataExtractor.cs | 190/190 | writers used raw invariant cell text for cached labels |
| src/PulseOffice.Formats.OpenXml/XlsxChartMapper.cs, src/PulseOffice.Formats.Odf/OdfChartMapper.cs | chart writing | defect: cached category labels were invariant raw values; now presented in the workbook language |
| src/PulseOffice.Formats.Odf/Charts/ChartDocuments.cs | 310/310 | inline local table: `office:value` invariant (canonical); see Remaining |

## Defects

| Severity | Where | Defect | Fix |
|---|---|---|---|
| Medium | ChartText.cs:10-17 | chart value labels, axis values and titles formatted/shaped with the OS culture in all three modules | `ChartText(CultureInfo)`, `ChartRenderer.Render(..., culture)`; Folio/Deck: surroundings language, Ledger: spreadsheet locale |
| Medium | DataTableRenderer.cs:248-263 | data table dates and numbers with the OS culture | `IDataTableRenderer.Render(..., culture)` from the surroundings language |
| Medium | SignatureLineRenderer.cs:51 | signing date with the OS culture | `ISignatureLineRenderer.Render(..., culture)`; Ledger spreadsheet locale |
| Medium | XlsxChartMapper.cs:45, OdfChartMapper.cs:48 | cached category labels written as invariant raw values (`1.5`, date serials) | `DocumentLanguageChartCellText`: cells presented with their number format in the workbook language (invariant when none); values stay invariant |
| Minor | ContentControlEditing.cs:191 | date control without date language used the OS culture | `DateText(document, control, date)`: control language, then document language |
| Minor | TextTypesetting.cs:16 | small caps upper-cased with the OS culture (Turkish dotted I) | `RunText` with `ITextCultures`: run language, then `TextFields.Language` |
| Minor | MeterPlot.cs:65, StatisticalPlot.cs:129, ChartPlot.cs:97 | meter value and histogram bin labels with the OS culture | `ChartFrame.Culture` |

## Tests (OS it-IT with document en-US and vice versa)

- `tests/PulseOffice.Documents.UI.Tests/ChartLocaleTests.cs`: chart numbers, chart, data table and signature painters
  follow the surroundings language; small caps with tr-TR/en-US.
- `tests/PulseOffice.Documents.Tests/ContentControlEditingTests.cs`: `DateWithoutItsOwnLanguageFollowsTheDocumentLanguage`.
- `tests/PulseOffice.Formats.OpenXml.Tests/ChartCacheLanguageTests.cs`: cached categories in the workbook language,
  values invariant (verified failing with the old raw cell text).
- Runs: Formats.OpenXml.Tests 2375/2375, Documents.UI.Tests 335/335, Architecture.Tests 40/40, Formats.Odf.Tests
  chart/ODS 285/285, Sheets.UI.Tests chart/print/language 21/21, Documents.Tests content controls 15/15.

## Remaining

- Inline chart local table in ODT/ODP (`InlineChartDocumentBuilder.NumberCell`): the `text:p` shown with each
  `office:value` is invariant; readers use `office:value`. The ODT write session has no document language.
- `ChartPicturePainter` (clipboard PNG, Exchange area) renders with the OS culture: `IEmbeddedObjectPicture` has no
  language parameter.
- `ParagraphTypesetter.Typeset` still shapes with the OS culture (shaping only, no formatting).
- `ChartRecommendations.IsNumber` parses with the OS culture (UI recommendation only).
