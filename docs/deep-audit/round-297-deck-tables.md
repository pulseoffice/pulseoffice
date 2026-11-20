# Round 297: Deck iterated effect leftovers and Deck tables (2026-11-20)

## Leftovers from round 292
- Fixed (medium): the unsized `IShapeGeometry.Outline(shape)` drew a model rounded rectangle with elliptic corners;
  it now uses the shape's natural size, so the corners are circular with the radius taken from the short side, as
  the sized outline does.
- Fixed (medium): the automatic ink of a fading or moving unit of shape text ignored the average colour of a picture
  fill. The picture backdrop mix moved into `IShapeBrushes.Behind`, shared by the shape renderer, the shape text
  editor and the slide renderer's iterated reveal.
- Fixed (minor): a moving unit's layer carried its paragraph's list label, so the label flew with each unit. Labels
  now belong to the still pass, which shows them as soon as a unit of the paragraph has started; layers pin the run
  colours and hide the label.
- Open: an iterated zoom still scales about the element's centre (no per-unit glyph bounds in the renderer). Queued.

## Deck tables
Audited insertion, styles and banding, merge and split, borders, cell margins and text direction, row and column
sizing, keyboard navigation, and the PPTX and ODP round trips.

- Fixed (serious): adding a row did not grow the table frame, and the table renderer stops drawing at the frame's
  bottom, so the new row was invisible. `EditTable` now grows or shrinks the frame by the change in total row height,
  in the same undo step.
- Fixed (medium): cell margins (`a:tcPr/@marL..marB`, `fo:padding-*`) and text direction (`a:tcPr/@vert`,
  `style:rotation-angle`, `style:direction="ttb"`) were dropped on import and never written. The model gained
  `TableCellFormat.Margins` and `Direction`; both codecs read and write them.
- Fixed (medium): no keyboard navigation between cells. Tab and Shift+Tab walk the cells, Tab on the last cell adds
  a row, arrows move and Shift+arrows extend the cell selection, and Delete or Backspace with cells selected clears
  their text instead of deleting the whole table (`IDeckTableKeys`).
- Open (queued): renderer and dialog for margins and direction; `a:tableStyleId` and `table:template-name` for tables
  without their own look; merged cells in keyboard navigation and in-place cell typing; Insert Above and Insert Left;
  per-run formatting inside PPTX cells.

Not covered in depth: the table style gallery's look presets against each other and the merge and split commands
were read only through their call sites; no defects were found there in this pass.
