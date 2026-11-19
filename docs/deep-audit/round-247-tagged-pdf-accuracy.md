# Round 247 (2026-11-19): tagged PDF accuracy

Area: the structure layer of rounds 239 and 243 (`PrintStructure`, `PdfStructure`, `IPageCanvas.Tag`, artifacts,
`IPdfAnnotationTagging`). Round 248 worked in parallel on PDF form widgets, so the form writer and the exporter's
annotation call were not touched.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Medium | Deck table cells were matched to drawn text by a sequential text match: a value shown in another format than the stored one (numbers, dates) advanced to the next cell, so a cell drawn in several runs split across cells | `PrintTextSegments.Locator` (`IPrintSegmentLocator`): `SegmentTaggingCanvas` asks it for the element at the centre of each drawn run (rotation included) before falling back to the text match; `SlideTableCellLocator` maps the point back into the table's frame (unrotating around the element centre) and finds the cell with the shared `TableGeometry`, a covered cell resolving to its merge origin. `SlidePagePainter` passes each element's placement (`SlideElementPlacement`) |
| Medium | A link annotation whose target had no tagged run on its page (Ledger cell links, Deck link areas) became a root-level `Link` | `PdfStructureTree` records, per page, the drawn area of every marked element and its ancestors (`Cover`); the annotation's `Link` becomes a child of the element covering the link area best: the largest share of the element inside the link, then the largest shared area (a cell rather than its row or table, a text box section rather than one paragraph) |
| Medium | A Deck table had no `BBox`, since its fills and borders are artifacts and its text carried no area | Text runs now mark their drawn area (glyph runs with rotation, `DrawText` with its shown width); a `Table` or `Figure` ancestor of the current element grows with it, and with the artifacts painted under a table tag |
| Minor | A rotated figure's `BBox` was the unrotated frame | `DrawPath` marks the axis-aligned box of the rotated frame |
| Minor | A table spread over several pages (a Ledger sheet) got the box of its first page only | A table whose content spans pages writes no `BBox` (the attribute describes one rectangle on the element's `/Pg` page) |
| Minor | Deck paragraphs: text that matched no paragraph (a field shown with its value) stayed with the previous paragraph even when that one had been drawn in full | A miss moves on to the next paragraph once the current one's text is consumed |
| Minor | Deck data table header row ignored merges (covered header cells were emitted) and the corner cell had scope `Column` with a first column header | Covered header cells are skipped, header spans come from merges, the corner is `/Scope /Both` |

## Decided

Header scope comes from the table style's header row and first column: those are the only header semantics the model,
ODF (`table:table-header-rows`, the first-column template flag) and PPTX (`firstRow`, `firstCol`) carry, so nothing
else can be derived honestly. A figure over several pages keeps the box of its first page, which is what a single
`BBox` relative to `/Pg` can say.

## Open

Deck paragraph text is still matched by text (positional paragraph boxes would need the shape text layout to expose
its line boxes to the print path); autofit text shown with other line breaks keeps working because runs are substrings.
The PDF/UA identifier still waits for a checker run.

## Tests

`Round247TaggedPdfTests` (link joining the covered cell, rotated figure box, table box), `Round247SegmentTaggingTests`
(locator over text, consumed paragraph advance), `Round247SlideTableLocatorTests` (cell by position, merge origin,
corner scope, rotated table).
