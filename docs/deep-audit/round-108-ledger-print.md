# Deep audit round 108: Ledger printing and page setup

## Coverage (files read in full)

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Printing/SheetPrintDocument.cs | 559/559 | ok, see notes |
| src/PulseOffice.Sheets.Printing/SheetBands.cs | 33/33 | ok |
| src/PulseOffice.Sheets.Printing/SheetPrintOptions.cs | 26/26 | ok |
| src/PulseOffice.Printing/BandPaginator.cs | 77/77 | ok |
| src/PulseOffice.Sheets.UI/SheetPrintDocumentBuilder.cs | 85/85 | D1 |
| src/PulseOffice.Foundation/Pages/PageSetup.cs | full | no first page number / first-odd-even model (open point) |
| src/PulseOffice.Printing/HeaderFooterFields.cs | full | D4 |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxPageSetupCodec.cs | 181/181 | D2, D3 |
| src/PulseOffice.Formats.Odf/Spreadsheet/PageStyles.cs | 272/272 | D4 |
| src/PulseOffice.Foundation/Pages/PaperCatalog.cs | full | ok |
| src/PulseOffice.Formats.OpenXml/XlsxFormat.cs (fidelity part) | 270-300 | D3 declared |
| src/PulseOffice.Formats.Odf/OdsFormat.cs (fidelity part) | 100-140 | D4 declared |

## Defects

| Id | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| D1 | grave | SheetPrintDocumentBuilder.cs (Parts) | Manual row and column page breaks were never passed to the print document: print, preview and PDF ignored every break the user inserted or a file carried. | Option building moved into `ISheetPrintOptionsFactory` / `SheetPrintOptionsFactory` (Sheets.Printing), which passes `RowBreaks`/`ColumnBreaks`; the builder uses it. |
| D2 | medium | XlsxPageSetupCodec.cs Build | "Fit to N pages tall" with width unconstrained omitted `fitToWidth`, whose OOXML default is 1: Excel squeezed the sheet to one page wide. | With fit-to-pages on, both `fitToWidth` and `fitToHeight` are written, 0 meaning automatic. |
| D3 | medium | XlsxFormat.cs CheckFidelity | A custom paper (read from ODS `fo:page-width/height` not matching the catalogue) was written without `paperSize`, so Excel printed on Letter, silently. | Declared `XLSX234`. |
| D4 | medium | HeaderFooterFields.cs, PageStyles.cs | Excel header/footer formatting codes (`&"font"`, `&14`, `&B`, `&I`, `&U`, `&S`, `&K…`, `&G`, `&Z`) were printed as literal text and written into ODS regions as literal text. | Shared tokenizer `IHeaderFooterCodes` (Foundation.Pages) used by print expansion and the ODS writer; formatting is skipped, `&Z` prints the document name; loss declared `ODF273`. |

## Checked, no defect
- Pagination is linear: `BandPaginator` and the fit-to-pages sum run once over the area; 1,000,000 rows are not laid out twice.
- Repeated titles, hidden rows/columns and merged regions split across pages (clipped, drawn once per page) are consistent between `BandPaginator` and `RowsOf`/`ColumnsOf`.
- XLSX/ODS breaks, margins with header/footer heights, centring, order, gridlines and headings round trip.

## Open points (not fixed in this round)
- No model for first page number (`useFirstPageNumber`/`style:first-page-number`) nor for first/odd/even headers (`differentFirst`, `differentOddEven`, `style:header-first`/`header-left`): read from files and dropped on import, silently. Needs a model extension in `PageSetup` plus both codecs and print.
- Printing a whole workbook numbers pages per sheet (`&P`/`&N` restart for each sheet and print range); Excel continues numbering across the job.
- A manual break placed on a hidden row or column is ignored.
- Objects anchored in a hidden row or column are not printed.

## Tests
- tests/PulseOffice.Sheets.Printing.Tests/SheetPrintOptionsFactoryTests.cs (D1)
- tests/PulseOffice.Formats.OpenXml.Tests/SheetPageSetupRound108Tests.cs (D2, D3, D4)
- tests/PulseOffice.Printing.Tests/HeaderFooterFieldsTests.cs (D4)
