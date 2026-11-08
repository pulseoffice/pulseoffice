# Deep audit round 62: the last locale leftovers

Scope: the "Remaining" items of [round-58-chart-locales.md](round-58-chart-locales.md) and the open point of
[round-60-folio-layout.md](round-60-folio-layout.md). Same route as rounds 55 and 58: Folio and Deck use the document
language through `ITextCultures` (invariant when a file declares none), Ledger the spreadsheet locale
(`ISpreadsheetLocale`).

## Coverage

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Formats.Odf/Charts/ChartDocuments.cs | 310/310 | defect: the inline local table wrote `text:p` invariant; now `Build(..., culture)` and `Supplement(data, culture)` |
| src/PulseOffice.Formats.Odf/OdfDeckChartMapper.cs | 55/55 | `BuildDocument` takes the culture |
| src/PulseOffice.Formats.Odf/Presentation/Writing/Foundation.cs, ElementWriters.cs | session, chart writer | `OdpWriteSession.Culture`; the chart writer passes it |
| src/PulseOffice.Formats.Odf/OdpWriter.cs | 110/110 | ODP session culture from `Presentation.TextLanguage` |
| src/PulseOffice.Formats.Odf/Odt/OdtDrawings.cs (drawing session), Odt/OdtWriteSession.cs | session | ODT drawing session culture from `TextDocument.Language` |
| src/PulseOffice.UI.Controls/Services/EmbeddedObjectPicture.cs | 86/86 | defect: clipboard chart PNG with the OS culture; `Png(content, size, culture)`, painters take the culture |
| src/PulseOffice.Documents.UI/Exchange/FolioExchange.cs | capture path | passes the document language |
| src/PulseOffice.Sheets.UI/Exchange/LedgerExchange.cs | services, picture path | passes the spreadsheet locale |
| src/PulseOffice.UI.Controls/Text/Rich/TextTypesetting.cs | 414/414 | defect: `ParagraphTypesetter.Typeset` shaped with the OS culture; now the culture of the run at the piece start (run language, then `TextFields.Language`); hanging labels the first run's |
| src/PulseOffice.Presentations.UI/Rendering/ElementPainters.cs | prompt path | placeholder prompt is interface text: interface culture |
| src/PulseOffice.Charts/ChartRecommendations.cs | 103/103 | defect: numeric categories parsed with the OS culture; `Recommend(data, culture)` |
| src/PulseOffice.UI.Controls/Charts/ChartPicker.cs, ChartPickerView.cs, ChartPreview.cs, ChartEditor.cs, EmbeddedChartPrompt.cs, InlineChartData.cs | request paths | `ChartPickerRequest.Culture` and `ChartEditSource.Culture` required; recommendations, preview and thumbnails use it |
| src/PulseOffice.Sheets.UI/LedgerChartCommands.cs | picker paths | spreadsheet locale |
| src/PulseOffice.Documents.UI/Commands/FolioChartCommands.cs, FolioObjectInsertCommands.cs | chart paths | document language |
| src/PulseOffice.Presentations.UI/DeckInsertCommands.cs, ObjectDialogs/ChartEditDialog.cs | chart paths | deck text language |
| src/PulseOffice.Text.Rich/ParagraphFlowProperties.cs, RichText.cs | line height, line spacing | defect: unbounded proportional factor; `LineSpacingFactor.Maximum` (132, the largest multiple Word accepts) |
| src/PulseOffice.Documents/Styles/Properties.cs | `ParagraphProperties.LineSpacing` | bounded too |
| src/PulseOffice.Documents.Layout/Lines/LineSpacing.cs | 41/41 | `ProportionalSpacing` bounds the factor as well |

## Defects

| Severity | Where | Defect | Fix |
|---|---|---|---|
| Medium | ChartDocuments.cs `InlineChartDocumentBuilder.NumberCell` | the local table of charts embedded in ODT/ODP showed `1.5` in an Italian document | `text:p` in the document (ODT) or deck (ODP) language, `office:value` invariant |
| Medium | EmbeddedObjectPicture.cs `ChartPicturePainter` | the PNG put on the clipboard with a copied chart used the OS culture | `IEmbeddedObjectPicture.Png(..., culture)`; Folio document language, Ledger spreadsheet locale |
| Medium | TextTypesetting.cs `ParagraphTypesetter.Typeset` | Deck/shape text shaped with the OS culture (script-dependent glyph choice, line breaking) | culture of the run language, then the fields language |
| Minor | ChartRecommendations.cs `IsNumber` | `1,5` categories in an Italian sheet on an English system not read as numbers (no scatter suggested), and the reverse | parse with the sheet/document culture, then invariant |
| Minor | LineSpacing.cs / Text.Rich model | a proportional factor around 1e300 (macro, agent) gave infinite line heights | bounded at 132 in `ParagraphLineHeight`, `TextParagraph.LineSpacing`, `ParagraphProperties.LineSpacing` and in the layout rule |

## Tests (OS it-IT with document en-US and vice versa)

- tests/PulseOffice.Formats.Odf.Tests/DeepAuditRound62ChartTableTests.cs: ODT and ODP local tables.
- tests/PulseOffice.UI.Controls.Tests/DeepAuditRound62LocaleTests.cs: clipboard chart PNG, rich text shaping culture.
- tests/PulseOffice.Charts.Tests/DeepAuditRound62RecommendationTests.cs: numeric categories.
- tests/PulseOffice.Documents.Layout.Tests/RoundSixtyTwoLineSpacingTests.cs: bounded factor, finite lines.

## Remaining

- `ParagraphLineHeight with { Mode = Proportional }` on an existing huge exact value is not re-bounded in the model; the
  layout rule bounds it anyway.
- Deck has no clipboard PNG for charts (its exchange participant does not build pictures): nothing to pass.

## Live checks

Copy a chart from an Italian Folio document on an English system and paste it as a picture elsewhere: decimal commas.
Save an Italian Folio/Deck document with a chart as ODT/ODP and open the chart data table in another suite.
