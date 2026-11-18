# Round 233: Folio images and anchored objects

Area: inserting images, anchoring and moving between anchors, wrapping, positions relative to margin, page, column and
paragraph, crop and adjustments, alt text, linked and embedded images, rotation and flip, ODT and DOCX round trip.
No builds or test runs were made in the round; the coordinator builds and tests after the join.

## Defects fixed

| Severity | Defect | Fix |
|---|---|---|
| Serious | A linked image was written to DOCX as an empty embedded media part, and a Word picture whose blip has `r:link` was dropped when read. | `DocxPictureCodec` writes an external image relationship (`a:blip r:link`) for linked images and reads it back as a linked image, with its crop, greyscale and duotone, using the same link helpers as the XLSX image mapper. |
| Medium | The ODT `fo:clip` of cropped images was measured on the displayed frame, while ODF measures it on the original image, so other applications cropped a different region and their cropped images were read wrong. | New `OdtImageClip`, shared by the ODT writer and reader: the original size is the pixel size at 96 dpi, or, without pixels, the frame size taken as unscaled. |
| Medium | Word `wp:align` and ODF `style:horizontal-pos`/`style:vertical-pos` (left, centre, right, top, middle, bottom) were ignored: a picture centred on the page was drawn at the left edge, and the writers always wrote an offset. | `ObjectPlacement.HorizontalAlign`/`VerticalAlign` (`ObjectAlignment`); the layout places the object in its relation area; ODT and DOCX read and write the alignment (`DocxObjectAlignments`, `OdtFrameCodec`). The layout snapshot includes it, so a change relays out. |
| Medium | Objects placed relative to the left margin (ODF `page-start-margin`, Word `leftMargin`) were laid out from the text area instead of the page edge. | `ObjectPlacement` in the layout measures that relation from the page edge. |
| Medium | Rotated Folio frames carried their angle only in our own attribute, so other applications showed them unrotated. | The ODT frame carries a standard `draw:transform` (rotation and position, no `svg:x`/`svg:y`); the reader takes the position from it and still prefers our exact angle. |
| Minor | Image filters other than greyscale were dropped silently in DOCX (ODT declared it). | Declared as DOCX046. DrawingML blip effects have no sepia, invert, posterize or the other filters; the app bakes them into the pixels anyway, so only filters read from our own files reach the writer. |
| Minor | Nudging an aligned object with the arrow keys did nothing (introduced by the alignment model). | The nudged axis turns into a page position from the laid-out box. |

## Checked and found sound

Wrap modes and distances in both formats (square, tight and through with contour polygon, top and bottom, behind and in
front), DOCX crop (`a:srcRect`), flips and rotation in DOCX, alt text title and description in both formats, images in
headers, footers and notes (each part gets its own image part), insertion as one undo step, compression updating the
pixel size.

## Open items

Listed in the Folio section of `docs/audit-queue.md`: the meaning of the vertical "margin" relation differs between
ODT, DOCX and the layout; DOCX reads the column relation back as paragraph; the position dialog does not offer aligned
positions; Deck still measures `fo:clip` on the frame; the same image inserted twice is stored twice.

## Tests

`Round233FolioImageTests` (Formats.OpenXml and Formats.Odf) and `Round233ObjectPlacementTests` (Documents.Layout), all
on one-byte or header-only images and single objects. The nudge fix has no test of its own.
