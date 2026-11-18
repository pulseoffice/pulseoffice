# Round 218: shape text drawing and Ledger connector sites

Area: the round-214 Deck code items of the audit queue. Code was written without building or running tests; the
coordinator builds and runs the tests at the join.

## Fixed

1. **Shape and text box text follow the whole text layout.** The shared `TextFlowFrame` now decides, for all three
   modules, the box (swapped for vertical flow), whether glyphs are stacked and the angle: upright text keeps only its
   own rotation, dropping the shape's rotation and the flow rotation, and vertical upright text is stacked; otherwise
   the angle is text rotation plus shape rotation plus flow. `ShapeRenderer` uses it for plain and list text and
   splits wrapped text into columns through the new `ShapeTextColumns` (column width from the spacing, column height
   snapped to whole lines, each column drawn clipped with the text moved up by the columns before it). Text boxes
   stack characters: `TextBlockTypesetter` lays each text element of a paragraph below the previous one, and
   `TextStacks` places each stacked paragraph as its own column from the anchor, so the offsets of every segment stay
   right for the caret. `ComposedTextFrame` now carries its rotation (`Placement`): `RichTextFramePainter` and the
   shared `InlineTextEditor` paint under it, the editor's IME caret rectangle is mapped through it, and
   `TextCaretGeometry.Hit` maps the point back through its inverse and picks the line among those of the column under
   the point (columns and stacks alike). The overlay `ShapeTextEditor` used by Deck and Ledger shape text now turns and
   sizes itself with the text's own rotation and flow as well. Tests: `Round218ShapeTextDrawingTests`.
2. **Ledger connectors can be created with chosen sites.** Routing already used the endpoint sites read from XLSX and
   ODS; `ISheetConnectors.Connect` now takes the start and end sites as Deck's does, and automatic ends still leave
   from the side facing the other object. Tests: `Round218SheetConnectorRoutingTests`.

## Open items

Listed in `docs/audit-queue.md` (Deck, round 218).
