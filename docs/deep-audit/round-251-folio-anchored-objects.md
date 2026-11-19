# Round 251: Folio anchored objects

Area: anchoring, wrapping, alignment and relations, z-order and their DOCX and ODT round trips.

## Defects found

| Severity | Defect | Status |
|---|---|---|
| Medium | DOCX `wp:anchor/@relativeHeight` was divided by 1024: files from other suites that number the stack 1, 2, 3 read every object at the same depth, so overlapping objects lost their stacking order. | Fixed |
| Medium | ODT frames read only the graphic properties of their own automatic style. Frames from other applications that inherit wrap, position, relation, run-through or spacing from a named graphic style (the usual "Graphics" or "Frame" style) or from the graphic default style came in at the top left with parallel wrap. | Fixed |
| Medium | DOCX `relativeFrom` values `rightMargin`, `bottomMargin`, `insideMargin` and `outsideMargin` fell back to column and paragraph, moving the object by the width of the text area. | Fixed |
| Minor | `wp:anchor/@layoutInCell` and `@allowOverlap` are always written as true and not read; the model has no place for them. | Queued |
| Minor | ODT has no bottom-margin vertical relation: it is written as `page` plus our own `pt:vertical-rel="bottom-margin"`, so other applications measure the offset from the page top. | Queued |
| Minor | Word's inside and outside margin relations are read as left and right margin (odd pages). | Queued |

## Fixes

- The DOCX reader keeps the raw relative height and, once the body is read, renumbers the floating objects by rank
  (ties keep document order); objects as characters keep zero.
- `OdtAutomaticStyles.GraphicPlacement` merges the placement and wrap attributes of a frame's automatic style with its
  named parent chain and the graphic default style (own attributes win; other attributes stay from the own style).
- New relations `HorizontalRelation.EndMargin` (DOCX `rightMargin`, ODT `page-end-margin`) and
  `VerticalRelation.BottomMargin` (DOCX `bottomMargin`, ODT as above), laid out from the end of the text area, offered
  in the position dialog with texts in the five languages.

## Tests

`Round251AnchoredObjectTests` (OpenXml and Odf) and `Round251MarginRelationTests` (layout).
