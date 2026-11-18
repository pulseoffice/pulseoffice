# Round 161: Ledger print output

Area: the print rendering of `PulseOffice.Sheets.Printing` (page splitting, page order, repeated titles, hidden
lines, grid lines and headings, headers and footers, scaling and fit-to-pages, area choice) and the model limit left
open by round 159: print titles held as a count from the top. The branch merges round 159 and main with audit cycle 1
first, and builds on round 159's `PrintTitlesPart`.

## Defects fixed

| Severity | Defect | Fix | Test |
|---|---|---|---|
| Medium | Print titles were a count from the top: XLSX `$3:$4` / `$C:$E` and ODS `table:table-header-rows` or `table:table-header-columns` away from the first line were read as "the first N lines"; header rows below the last used row or inside a row group were not written | Model restructured: `Worksheet.PrintTitleRows` / `PrintTitleColumns` are a `PrintTitleBand` (start, count). XLSX writes and reads `_xlnm.Print_Titles` with the real start; ODS reads the band from the position of the header element among all rows or columns (also inside groups) and writes it at any position, inside a row group that contains it, splitting column groups around it; structure edits move the band when lines are inserted or deleted before it (`TitleBandShift`), grow or shrink it inside; snapshots, sheet copy and workbook copy carry the band; print options pass the band to the paginator. The Page Setup dialog keeps the start and edits the count; "Repeat rows" uses the selected rows | `Round161XlsxPrintTitleTests`, `Round161OdsPrintTitleTests`, `Round161PrintTitleStructureTests` |
| Medium | Fit to pages ignored the repeated titles printed on every page, the headings and the split at whole lines, so a sheet set to fit on N pages could print on N+1 | The scale is the largest one whose actual pagination fits the requested pages (bisection over the paginator, 0.1 to 1) | `Round161SheetFitTests` |

## Checked without defects

- Page order (down then across, across then down) and page numbering across sheets of a whole-workbook job.
- Repeated rows and columns lying outside the print area are printed on every page, and are not repeated on the
  page where they appear in the body.
- Hidden rows and columns are skipped in the body, the titles, the headings and the fit computation.
- Grid lines (outer edge on the first row and column of each page), row and column headings, centring.
- Header and footer fields `&P`, `&N`, `&D`, `&T`, `&A`, `&F`; selection printing builds only the chosen range.

## Open

- Minor: `&P+n` / `&P-n` page arithmetic in header and footer codes prints the offset as text.
- Minor: under fit-to-pages manual page breaks still apply; the other spreadsheet applications ignore them there.
- Minor: the ODS reader does not flatten the `table:table-rows` wrapper element when counting rows.

## Status

Closed for this round; nothing was built or run here (the coordinator builds and runs the tests after the join).
