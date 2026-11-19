# Round 238: PDF export and printing

Area: PDF export (`PulseOffice.Formats.Pdf`), imposition and the print dialog (`PulseOffice.Printing`,
`PrintPromptView`), the module printables (Folio, Deck, Ledger). Started from the open items of round 237.

## Defects found

| Severity | Defect | Status |
|---|---|---|
| Medium | N-up and brochure scaled every page by the first page's (or the setup's) size: a landscape page among portrait ones overflowed its cell or was clipped | Fixed: each slot is fitted by its own page size (`PrintImposition.Fit`) |
| Medium | Brochure printing sent the duplex chosen in the dialog (often one-sided or long edge), so folded booklets came out upside down or single-sided | Fixed: `PrintJobOptions.EffectiveDuplex` forces short edge for brochures; the dialog disables the duplex and orientation choices while brochure is on |
| Medium | The print dialog had no paper size or orientation override | Fixed: Paper (as in the document, or any size of `IPaperCatalog`) and Orientation (automatic, portrait, landscape); pages are fitted to the chosen sheet |
| Medium | Single-line PDF text (`DrawText`: Ledger cells, Deck notes, headers, footers) was not reordered by the bidirectional algorithm | Fixed: `PdfBidiText` resolves levels with `IBidiAlgorithm`, orders level runs visually, reverses right-to-left runs piecewise (shaped complex spans stay whole, other graphemes are mirrored) |
| Medium | Deck notes pages cut long notes at the bottom of the notes box silently | Fixed: notes shrink down to 60 % of the master's size, then the last line ends with an ellipsis |
| Medium | PDF had no page labels | Fixed: `IPrintableDocument.PageLabel` (default none), `PdfPageLabels` writes `/PageLabels` ranges; Folio gives each page its page style's number format and restart number; composite documents forward the labels |
| Medium | Deck shape hyperlinks and Ledger object hyperlinks were not links in the PDF | Fixed: `SlideLinkAreas` (interaction hyperlinks, text elements linked as a whole) and Ledger placed-object hyperlinks become link annotations |
| Medium | No form fields for Folio content controls and form controls | Open, queued |
| Minor | Deck text elements with several different run links, and links inside shape text, have no link area (no run geometry reaches the printable) | Open, queued |
| Minor | Deck "go to slide" and next/previous/first/last slide actions are not internal PDF links | Open, queued |
| Minor | Page labels are not written for Deck (slide numbering start) or Ledger (first page number) | Open, queued |

## Notes

- The sheet size of N-up and brochure still comes from the first selected page when no paper is chosen, as other
  suites do; only the per-page scale changed.
- `PdfBidiText` is injected into `PdfExporter` and through it into `PdfContentCanvas`; text without right-to-left
  characters or embeddings takes the old path unchanged.
