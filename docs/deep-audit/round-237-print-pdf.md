# Round 237: printing and PDF export

Area: the shared print dialog and imposition, PDF export options, the PDF writer, and the CUPS print path used on
Linux and macOS, across Folio, Ledger and Deck. No build or test run was made in the round; the coordinator runs the
tests after the join.

## Defects fixed

1. **Serious: every PDF and printed page took the first page's size.** `PdfExporter` wrote one `MediaBox` for the
   whole file, from `IPrintableDocument.Setup`, and the 1-up imposition, the print preview and the N-up clip did the
   same. A Folio document with a landscape page style, or a Ledger workbook mixing portrait and landscape sheets, came
   out with every page in the first page's size and orientation, cut or misplaced. `IPrintableDocument` now has
   `PageSize(int)` (default: the setup's size); `FolioPrintable` answers from each laid-out page,
   `CompositePrintDocument` from the part that owns the page, `FilteredPrintDocument` forwards it and
   `ImposedDocument` gives an unscaled single-slot sheet the size of its source page. The exporter, the clip of
   scaled slots and `PrintPagesView` use it per page. Tests: `Round237PrintPdfTests`.
2. **Medium: close opacities shared one graphics state.** The PDF canvas named its `ExtGState` by the opacity rounded
   to hundredths, so 0.496 and 0.504 on one page both became `GS50` and the later value painted both. Names now use
   thousandths, the precision the state itself is written with. Test: `Round237PrintPdfTests`.
3. **Medium: Ledger PDFs had no bookmarks.** Folio marks headings and Deck slide titles, but a Ledger sheet marked
   nothing, so a workbook export had no outline. The first page of each sheet now marks the sheet name. Test:
   `Round237SheetBookmarkTests`.
4. **Medium: Ledger cell hyperlinks were lost in PDF.** A cell with an external hyperlink now marks a link over the
   cell; internal `#Sheet.A1` targets are left out because no destination exists for them yet. Test:
   `Round237SheetBookmarkTests`.
5. **Minor: a print that selected no page passed the dialog.** A range such as `1` with the even subset on a one-page
   document was accepted, and the job failed after the dialog closed with an export error. The dialog now checks the
   pages left after the subset. Test: `Round237PrintSelectionTests`.

## Checked without defects

Page range parsing (`1-3,5`, open and reversed ranges, en dash), copies and collation through `lp -n` and
`collate`, duplex through `sides`, printer listing with `lpstat` under the C locale, the AES-256 revision 6 password
hash and permission flags, string encryption of literal and hex strings, outline counts, named destination order,
link and note annotations with appearance streams, PDF/A-2b metadata and output intent, JPEG pass-through with ICC
profiles and inverted Adobe CMYK, image downsampling, font fallback order and the missing-glyph diagnostic, and the
ActualText spans of shaped clusters.

## Open items

Moved to the "Shell and formats" section of `docs/audit-queue.md`: no tagged PDF structure; no page labels, form fields,
Deck links or Ledger object links; no bidirectional reordering in single-line PDF text; no paper or orientation
override in the print dialog, imposition scaled by the first page and brochure without forced short-edge duplex;
Deck notes pages cut long notes silently.
