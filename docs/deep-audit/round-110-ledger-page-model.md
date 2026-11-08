# Round 110: Ledger page model (first page number, first/odd/even headers, job numbering, hidden breaks, objects)

Follows the open points of [round-108-ledger-print.md](round-108-ledger-print.md).

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Foundation/Pages/PageSetup.cs | full | model extended (first page number, first/even headers and footers) |
| src/PulseOffice.Printing/CompositePrintDocument.cs | full | job numbering added |
| src/PulseOffice.Printing/HeaderFooterFields.cs | full | ok |
| src/PulseOffice.Printing/BandPaginator.cs | full | D3 |
| src/PulseOffice.Sheets.Printing/SheetPrintDocument.cs | full | D2, D4, D5 |
| src/PulseOffice.Sheets.Printing/SheetBands.cs | full | ok |
| src/PulseOffice.Sheets.Printing/SheetPrintOptions*.cs | full | ok |
| src/PulseOffice.Sheets.UI/SheetPrintDocumentBuilder.cs | full | D2 |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxPageSetupCodec.cs | full | D1 |
| src/PulseOffice.Formats.Odf/Spreadsheet/PageStyles.cs | full | D1 |
| src/PulseOffice.Formats.Odf/Spreadsheet/SheetPartReaders.cs | 140-290 | D6, D7 |
| src/PulseOffice.UI.Controls/Printing/PrintPreviewView.cs | full | ok |
| src/PulseOffice.Sheets/SheetPlacement.cs | full | ok |
| src/PulseOffice.Sheets.UI/PageLayout/SheetPageSetupView.cs | full | D1 (UI); custom paper shown wrong (open point) |
| src/PulseOffice.Sheets.UI/Scripting/PrintFacade.cs | full | D8 |
| src/PulseOffice.Printing/ClippedPageCanvas.cs | full | culls, does not clip (see open points) |

## Defects

| Id | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| D1 | grave | PageSetup, XlsxPageSetupCodec, PageStyles (ODS) | First page number (`pageSetup@firstPageNumber/useFirstPageNumber`, `style:first-page-number`) and first/even headers and footers (`differentFirst`, `differentOddEven`, `firstHeader`…, `style:header-first`, `style:header-left`…) were dropped on read, silently, in both formats. | `PageSetup` gains `FirstPageNumber`, `DifferentFirstPage`, `DifferentOddEven`, `FirstHeader/FirstFooter/EvenHeader/EvenFooter` and `HeaderOn/FooterOn`; both codecs read and write them; the page setup dialog edits them (Header/Footer tab «Variants», Sheet tab «Numbering»), five languages. |
| D2 | medium | SheetPrintDocument, SheetPrintDocumentBuilder | Printing the workbook restarted `&P`/`&N` for every sheet and print range. | `IJobPageNumbering` (Printing): `JobPageNumbering` numbers the parts of the job once, continuing across sheets and ranges, restarting at a sheet's first page number; `&N` is the job's total; `OwnPageNumbering` for a single part. |
| D3 | medium | BandPaginator.Split | A manual break on a hidden row or column was ignored. | A break seen on a hidden line is kept pending and applied at the next visible line. |
| D4 | medium | SheetPrintDocument.PaintObjects | Objects anchored in a hidden row or column were never printed, while the screen shows them. | Objects are placed from the layout's row and column offsets (hidden lines measure zero), so they print where the screen shows them, on the page where their top-left corner falls. |
| D5 | minor | SheetPrintDocument.PaintObjects | Objects were painted in list order, not z-order. | Painted by `ZOrder` (stable). |
| D6 | medium | PrintRangePartReader (ODS) | A print range of one cell (`Sheet.$B$3`, no colon) was skipped. | Read as a one-cell range. |
| D7 | minor | LayoutPartReader (ODS) | A break style on a repeated row/column applied only to the first of the repeat. | Applied to every repeated line (capped at 4096). |
| D8 | minor | PrintFacade.Sheet | An out-of-range active sheet index threw `ArgumentOutOfRangeException` from macros. | Returns no sheet. |

## Excel behaviour checked
- `&N` is the number of pages of the print job; `firstPageNumber` restarts the count of its sheet and the following sheets continue from it.
- First-page header applies to the first page of each sheet; even header to even page numbers.
- Objects: Excel's "move and size with cells" objects shrink and disappear when their rows/columns are hidden. The model has no anchoring mode (objects keep their size, as on screen), so printing now follows the screen; matching Excel needs an anchoring-mode model (`xdr:twoCellAnchor@editAs`, ODS `table:end-cell-address`) — open point.

## Open points
- Anchoring mode (move-and-size vs move-only) is not modelled: objects over hidden rows keep their size.
- An object crossing a page edge prints on its first page only, uncut: `IPageCanvas` has no clip primitive (`ClippedPageCanvas` culls whole images and paths).
- The page setup dialog shows the first paper of the list for a custom paper read from ODS (the setup itself keeps it until another paper is chosen).
- The first page number and the variants are not in the macro `PrintFacade` API.

## Tests
- tests/PulseOffice.Sheets.Printing.Tests/SheetPageModelRound110Tests.cs (D2, D3, D4, first/even headers)
- tests/PulseOffice.Formats.OpenXml.Tests/SheetPageModelRound110Tests.cs (D1 both formats, D6)
- tests/PulseOffice.Desktop.Tests/LedgerFormatSurfacesTests.cs (dialog keeps the variants and first page number)
