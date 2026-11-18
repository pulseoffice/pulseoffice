# Round 197: Folio page layout

Area: sections and section breaks, page size, orientation and margins, columns, headers and footers, page numbering,
page borders and background, line numbering, gutter and mirrored margins; layout and the DOCX and ODT round trip.

Method: reading of the section codec (`DocxSectionCodec`), the DOCX reader and writer settings, the page geometry of
the layout (`PageGeometries`, `PageFrames`) and the table insertion width. No build or test run in this round: the
coordinator runs `tools/run-tests.py --changed` at the join.

## Defects fixed

| Severity | Defect | Fix | Test |
|---|---|---|---|
| Medium | Page number formats other than Arabic, letters, romans and none were dropped silently in DOCX (written as nothing, read back as Arabic), although ODT keeps them and Word's `w:pgNumType/@w:fmt` has them. | Word's number format table moved into the shared `DocxNumberFormats` service, used by lists and sections. | `Round197FolioPageLayoutTests` (OpenXml) |
| Medium | A table inserted on a landscape page took the portrait text width (`Size.Width` instead of the oriented `Width`). | `DocumentEditor` uses the oriented width. | `Round197FolioPageLayoutTests` (Documents) |
| Medium | Word's gutter (`w:pgMar/@w:gutter`) was dropped on reading, shifting the text of bound documents. | Folded into the left margin, or the top one with `w:gutterAtTop`. | `Round197FolioPageLayoutTests` (OpenXml) |
| Minor | Word columns of different widths were read as equal columns without a diagnostic. | New read diagnostic DOCX245 (also in the corpus audit's declared features). | `Round197FolioPageLayoutTests` (OpenXml) |
| Medium | Mirrored page styles (page dialog "mirrored margins", ODT `page-usage="mirrored"`) were ignored by the layout: even pages kept the odd pages' inner and outer margins on screen, in PDF and in print. | `PageFrames.Geometry` mirrors content, body and decoration frame on even pages of a mirrored style. | `Round197FolioPageLayoutTests` (Layout) |
| Medium | Mirrored margins were lost in DOCX (declared as DOCX037) although Word has `w:mirrorMargins`. | Written when every used page style is mirrored, read back as mirrored usage; DOCX037 no longer fires then. | `Round197FolioPageLayoutTests` (OpenXml) |

## Checked without defects

Section types (continuous, next, odd and even page through starter styles), page size and orientation, header and
footer distances, first-page and even-page variants, page borders with their text offset, column count, gap and
separator, `w:titlePg`, `w:pgNumType/@w:start`.

## Open items

Listed in the Folio section of `docs/audit-queue.md` (marked 197).
