# Deep audit round 55: locale leftovers of round 54

Scope: the "Remaining" list of [round-54-field-locales.md](round-54-field-locales.md) and a new sweep of
`CultureInfo.CurrentCulture` in paths that produce document content. The Exchange/clipboard area belongs to another
agent and was not touched.

## Coverage

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.UI.Controls/Forms/FormControlRenderer.cs | all | defect: numbers and text shaped with the OS culture; fixed (culture parameter) |
| src/PulseOffice.UI.Embedding/ContentPainters.cs | all | defect: form painter had no document context; fixed (language from the surroundings) |
| src/PulseOffice.UI.Controls/Shapes/ShapeBackdrop.cs → ShapeSurroundings.cs | all | restructured: the paint surroundings now carry the language |
| src/PulseOffice.UI.Controls/Shapes/ShapeListText.cs, ShapeRenderer.cs | all / list text paths | defect: shape list text fields without language; fixed |
| src/PulseOffice.Sheets/FormControlBinding.cs | all | defect: booleans shown as 1/0; fixed; exposes the display culture |
| src/PulseOffice.Sheets.UI/GridParts/GridObjectRenderers.cs, Painting/GridObjectPainter.cs, SheetObjectRasterizer.cs | object painting | defect: no workbook language for shapes and forms; fixed |
| src/PulseOffice.Documents.UI/Rendering/PageSurfaces.cs, EmbeddedRasterizer.cs, FolioPrintable.cs, Canvas/FolioCanvas.cs | surfaces | defect: no document language for embedded content on screen and in print; fixed |
| src/PulseOffice.Presentations.UI/SlideRenderer.cs, Presentations/SlideFields.cs, Presentation.cs | scene, fields | defect: deck default text language ignored when no document language; fixed (`TextLanguage`) |
| src/PulseOffice.Documents.UI/Shell/FolioDocumentFactory.cs | all | defect: new documents took the OS culture; imports without language stayed without; fixed |
| src/PulseOffice.Presentations.UI/DeckDocumentFactory.cs | Build | defect: decks never received a language; fixed |
| src/PulseOffice.Sheets.UI/LedgerDocumentFactory.cs | Create* | defect: workbooks never received a language; fixed |
| src/PulseOffice.Localization/InterfaceCulture.cs (new), Sheets.UI/InterfaceSpreadsheetLocale.cs | all | interface-language culture shared by the three modules |
| src/PulseOffice.Formats.OpenXml/XlsxWriter.cs, XlsxFormat.cs | core properties, XLSX013 | workbook default language written as the package language; XLSX013 only for real losses |
| src/PulseOffice.Documents/Indexes/IndexEntries.cs | all | defect: alphabetical index sorted and lettered with the OS culture; fixed (document language) |
| Deck consumers of `Properties.Language` (header/footer, spelling, form fields, PPTX footer) | call sites | now `deck.TextLanguage` |

## Defects

| Severity | Where | Defect | Fix |
|---|---|---|---|
| Medium | FormControlRenderer.cs:78 | form control numbers drawn with the OS culture in Folio, Deck and Ledger | `Render(..., CultureInfo)`: Ledger the spreadsheet locale, Folio/Deck the language carried by `ShapeSurroundings` |
| Medium | FormControlBinding.cs:86 | linked booleans shown as `1`/`0` | localized TRUE/FALSE names of the interface language (`CultureBooleanNames`) |
| Medium | ShapeListText.cs:64 | fields in shape list text had no language (Ledger, Folio) | `ShapeSurroundings.Language` → `TextFields.Language`; Ledger passes `Workbook.TextLanguage` |
| Medium | FolioDocumentFactory.cs:22 | new documents took the OS culture, imports without `dc:language` kept none | new: interface language as default style and document language; imports: default style language, then interface language |
| Medium | DeckDocumentFactory, LedgerDocumentFactory | no default language at all | interface language as deck default text language / workbook default language when none is read |
| Medium | IndexEntries.cs:75,105,170 | alphabetical index order, letter separators and bibliography order followed the OS | document language |
| Minor | SlideFields and the deck call sites | deck default text language (`a:lang`, ODP default style) ignored | `Presentation.TextLanguage` |
| Minor | XlsxFormat.cs:155 | every workbook with a default language would declare XLSX013 | language written to core properties; loss only when it differs from the declared document language |

Files: ODF keeps the languages in `dc:language` and default styles (Folio `fo:language` of the default style, ODS
default cell style, ODP default text style); OOXML in `dc:language` of core properties, DOCX `w:docDefaults` `w:lang`
(new documents), PPTX `a:defaultTextStyle` `a:lang`, XLSX core language.

## Tests

- `tests/PulseOffice.Documents.UI.Tests/DocumentLanguageTests.cs`: form painter culture and page surface language
  (system it-IT with document en-US and vice versa), new and imported document languages.
- `tests/PulseOffice.Sheets.Tests/FormControlLocaleTests.cs`: booleans both directions.
- `tests/PulseOffice.Sheets.UI.Tests/ShapeFieldLanguageTests.cs`: shape list text fields, interface culture.
- `tests/PulseOffice.Presentations.Tests/FieldLanguageTests.cs`: deck default text language both directions.
- `tests/PulseOffice.Formats.OpenXml.Tests/XlsxAlignmentTests.cs`: workbook language through XLSX without XLSX013.
- `tests/PulseOffice.Documents.Tests/ThreadAndIndexLetterTests.cs`: index order and letters both directions.

## Remaining

- Chart number labels (`ChartText`) and data tables (`DataTableRenderer`) still format with the OS culture: the shared
  chart and table renderers get no document culture; same route as the form renderer, through `ShapeSurroundings`.
- `SignatureLineRenderer` signing date and `ContentControlEditing.DateText` fallback (no date language) use the OS culture.
- Small caps (`TextTypesetting`) upper-case with the OS culture.
- Remaining `CurrentCulture` hits are in dialogs and UI chrome (number boxes, previews, status messages).
