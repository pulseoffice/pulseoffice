# Round 235: Folio items of the audit queue

Area: object positions, shared image media, footnote continuation notices, DOCX line number restarts. No builds or
test runs were made in the round; the coordinator builds and tests after the join.

## Defects fixed

| Severity | Defect | Fix |
|---|---|---|
| Medium | The vertical "margin" relation meant the paragraph area in ODT, the top margin in DOCX and the text area in the layout. | One meaning: `VerticalRelation.Margin` is the top margin area (page top to the top margin), as Word's `topMargin`; the layout places it there. ODF has no top-margin relation, so ODT writes `style:vertical-rel="page"` (offsets measured from the same edge) plus our `pt:vertical-rel="top-margin"`, read back first. ODF `paragraph-content` is the new `VerticalRelation.ParagraphArea` (Word `paragraph`). DOCX `w:framePr` `margin` anchors, which mean the text area, are read as `PageArea`. |
| Medium | DOCX read the column relation back as paragraph. | The relation table lists Column first, so `relativeFrom="column"` reads as Column; the layout treats both alike. |
| Medium | ODF `inside`/`outside` and Word mirrored-page alignment were read as plain left/right. | `ObjectAlignment.Inside`/`Outside`, written and read in both formats (ODF horizontal only; Word both axes). The layout resolves them by page parity: inside is left on odd pages and right on even pages, outside the opposite; vertically they act as top and bottom. |
| Medium | Aligned positions relative to the paragraph or line could only be aligned at the top. | `ObjectFrame` carries the paragraph height and the first line height; middle and bottom work for both relations. |
| Medium | The position dialog did not offer aligned positions, nor the column and paragraph-area relations (an object with them showed the first entry). | Two choices, horizontal (by offset, left, centre, right, inside, outside) and vertical (by offset, top, middle, bottom, inside, outside), and the two relations, in five languages; the horizontal margin entry now reads "Left margin" and the vertical one "Top margin". "Align vertically" commands clear the alignment and use the top margin height for that relation. |
| Minor | The same image inserted twice was stored twice. | ODT (`OdtWriteSession.SharedMedia`) and DOCX (`DocxWriteContext.SharedImage`) key media by type and SHA-256: the second picture points to the same `Pictures/` entry, or to the same image part (a relationship from another part when needed). |
| Minor | Continuation notices used the footnote paragraph style and had no way to be set in the app. | New default paragraph style "Footnote Continuation" (child of Footnote, right-aligned, italic); the note settings dialog shows the notices with editable texts whose defaults are in five languages. |
| Minor | A DOCX line number restart on the first paragraph was lost. | The first section's `w:lnNumType/@start` carries it, without turning numbering into per-section restarts; DOCX248 now lists only what is still lost, in five languages. |

## Open items

In the Folio section of `docs/audit-queue.md`: continuation notices are still layout-only text (no caret or copy);
vertical inside/outside is top/bottom; the ODT top-margin relation degrades to `page` for other applications'
alignment; inside/outside is not part of the incremental layout's stability check.

## Tests

`Round235ObjectPositionTests` (Documents.Layout, Formats.Odf, Formats.OpenXml), `Round235SharedImageTests` (Formats.Odf,
Formats.OpenXml), `Round235ContinuationNoticeTests` (Formats.Odf), `Round235LineRestartTests` (Formats.OpenXml): single
objects, one-byte images, two-paragraph documents.
