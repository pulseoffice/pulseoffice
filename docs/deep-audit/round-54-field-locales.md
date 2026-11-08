# Deep audit round 54: field values follow the language of their text

From round 53 ("Remaining"): number and date values shown by fields, form controls and table caches were formatted with the
operating system culture. Word and the reference suites format a field with the language of the text it sits in (the run
language, falling back to the document default language); a spreadsheet form control follows the spreadsheet locale.

## Shared service

`ITextCultures` / `TextCultures` (`src/PulseOffice.Text.Rich/TextCultures.cs`): the first parseable language among the
candidates (run language, then document language) gives the culture; empty tags, `zxx`, `und`, `none`, `x-none` and unknown
tags are skipped. Only when no candidate declares a language does the injected fallback apply (the default constructor uses
the current culture: a document without any language has nothing else to follow; Folio documents get a language on creation).
Used by Folio field sources, the Deck/shape text field resolver, ODT table caches and Deck date footers.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Text.Rich/TextFields.cs | 122/122 | defect: slide number, count, date, time with OS culture; fixed |
| src/PulseOffice.Documents/Fields/FieldSources.cs | 243/243 | defect: property dates, clock, statistics with OS culture; fixed |
| src/PulseOffice.Sheets/FormControlBinding.cs | 149/149 | defect: linked numbers with OS culture; fixed (ISpreadsheetLocale) |
| src/PulseOffice.Formats.Odf/Odt/OdtBodyWriting.cs | 690-873 | defect: table cell values parsed with OS culture; fixed |
| src/PulseOffice.Formats.OpenXml/Presentation/PptxHeaderFooter.cs | 1-60 | defect: date footer cache with OS culture; fixed |
| src/PulseOffice.Presentations/HeaderFooter.cs | 25-60 | defect: on-screen date footer and slide number with OS culture; fixed (deck language) |
| src/PulseOffice.Presentations/SlideFields.cs | 1-25 | defect: deck language never reached the field resolver; fixed |
| src/PulseOffice.Presentations/HeaderFooterDateFormats.cs | full | correct: takes the culture as a parameter |
| src/PulseOffice.Formats.Odf/Presentation/OdfHeaderFooter.cs | 1-50 | correct: ODP writes no cached date text, only a data style |
| src/PulseOffice.Presentations.UI/Design/HeaderFooterView.cs | 26-45, 80-115, 283-292 | defect: previews and default fixed date with OS culture; fixed |
| src/PulseOffice.Presentations.UI/DeckFieldCommands.cs | 20-31, 140-155 | defect: fixed date/time fields with OS culture; fixed (resolver, deck language) |
| src/PulseOffice.Presentations.UI/DeckFormFieldCommands.cs | 14-22, 55-106 | defect: form fields filled from data with OS culture; fixed |
| src/PulseOffice.Documents.UI/Editing/FolioEditor.cs | 696-740 | defect: `Culture()` ignored the run language; now run then document language |
| src/PulseOffice.Documents.UI/Fields/FolioFieldInsertion.cs | 1-40 | defect: fixed date/time inserted with OS culture; fixed |
| src/PulseOffice.Documents.UI/Commands/FolioInsertCommands.cs | 75-95 | defect: quick date/time fields with OS culture; fixed |
| src/PulseOffice.Documents.UI/Fields/FolioFieldOptions.cs | 70-95 | defect: format examples with OS culture; fixed |
| src/PulseOffice.Documents.UI/Shell/FolioMergeOutput.cs | 18-45 | defect: merge data dates/numbers with OS culture; fixed (document language) |
| src/PulseOffice.Sheets.UI/LedgerView.cs, SheetGridView.cs, LedgerDocumentFactory.cs | constructors | defect: the form binding was never injected and the grid had its own; now one injected binding |

## Defects

| Severity | Where | Defect | Fix |
|---|---|---|---|
| Medium | TextFields.cs:18-45 | Deck and shape text fields (date, time, slide number, count) used the OS culture | `TextFieldResolver` resolves the culture from run then deck language |
| Medium | FieldSources.cs:50-102 | Folio clock, property date and statistic fields used the OS culture | sources take `ITextCultures`, run then document language |
| Medium | OdtBodyWriting.cs:725, 822, 860 | ODT table cell `office:value` parsed with the OS culture (a value typed in the document language was lost or misread); currency symbol of the data style from the OS | culture of the cell text (first run language, then document language); canonical attributes stay invariant |
| Medium | PptxHeaderFooter.cs:32 | PPTX date footer cache written in the OS culture | deck language |
| Medium | HeaderFooter.cs:48-54 | Slide date footer on screen, in pictures and print in the OS culture | deck language passed by all five callers |
| Medium | FormControlBinding.cs:85 | Ledger form controls showed linked numbers in the OS culture (and with full round-trip digits) | `ISpreadsheetLocale`, `G15` |
| Minor | LedgerView.cs:198 / SheetGridView.cs:68 | two separate default bindings; the DI one was never used | one binding injected from `LedgerDocumentFactory` |
| Minor | Folio insertion, options, merge, Deck fixed fields and form fields, header/footer dialog | previews and fixed values in the OS culture | editor/deck culture |

DOCX: field results are written from the run text already computed by the field sources, so DOCX and ODT caches follow the
language through the model; no DOCX writer formatted dates itself.

## Tests

- `tests/PulseOffice.Text.Rich.Tests/FieldLanguageTests.cs` (system it-IT with en-US text and vice versa, run over document, fallbacks)
- `tests/PulseOffice.Documents.Tests/FieldLanguageTests.cs` (clock and property fields, both directions, run over document)
- `tests/PulseOffice.Presentations.Tests/FieldLanguageTests.cs` (date footer and slide date field, both directions)
- `tests/PulseOffice.Sheets.Tests/FormControlLocaleTests.cs` (list items and linked text box, both directions)
- `tests/PulseOffice.Formats.Odf.Tests/OdtFieldLanguageTests.cs` (table cell value, document and run language, both directions)
- `tests/PulseOffice.Formats.OpenXml.Tests/PptxFieldLanguageTests.cs` (PPTX date footer cache, both directions)

## Remaining

- `FormControlRenderer` (shared UI) draws a numeric control value with the current culture: it has no document context; to be
  given the module's culture together with the control.
- Ledger form controls show booleans as numbers (`1`/`0`) rather than the localized TRUE/FALSE names.
- Ledger shape text fields get no workbook language (`ShapeListText` builds `TextFields` without one): runs carrying a language
  are honoured, otherwise the fallback applies.
- With no language at all on run and document the fallback is the current culture; Folio documents receive a language on
  creation, Deck decks may not.
