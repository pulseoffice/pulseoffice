# Round 230: East Asian upright text and in-place stacked editing

Scope: the two Deck items left open by rounds 218, 225 and 227. No build or test was run in this round (the
coordinator builds and tests after the join).

## Done

1. **East Asian characters stand upright in East Asian vertical text.** `EastAsianUprightText.Clusters` finds the
   text elements whose first code point is an ideograph, kana, hangul, bopomofo, CJK symbol or full-width form. When a
   frame has the East Asian vertical flow, `TextBlockTypesetter` lifts those clusters out of every segment: the cluster
   is hidden in the segment's formatted text (transparent ink, so the advance and the line breaks do not change) and
   built as its own glyph. `UprightGlyphs` paints each lifted glyph centred in the cluster's cell, turned back by 90
   degrees around the cell centre, so under the frame's 90-degree turn it stands upright while Latin and digit runs keep
   turning with the line. Since the cell is the cluster's own advance, caret, selection and hit testing are unchanged
   and match the drawn glyphs. Shape text in the East Asian, Mongolian and stacked flows is now drawn through the shared
   composed text frame (`ShapeListText.Draws`), which carries the flow and the shape rotation (`ShapeListText.Framed`),
   instead of the single formatted text of `ShapeRenderer`; the stacking and Mongolian band code left in the renderer
   was removed. Text boxes of Deck and shapes of Deck, Ledger and Folio (all drawn by the shared renderer) are covered.
   PPTX226 and ODP217 are no longer declared: both readers dropped the loss. Tests:
   `Round230EastAsianUprightTests`; `Round225VerticalModesTests` now expects no declaration.
2. **Stacked and Mongolian text are edited in place.** `ComposedTextFrame.Stacked` marks stacked frames. Their caret
   is a horizontal bar across the stacked glyph (above it, or below the last one at the end of a stack), painted
   through `ITextCaretGeometry.Bar`; their caret rows are the stacks from left to right with the glyph height as the
   row coordinate. `CaretArrows` maps each arrow key through the frame rotation into the frame's own direction and,
   for stacked frames, turns up and down into moves along the stack and left and right into moves across stacks;
   `CaretRowNavigation` gained the matching `Previous` and `Next` moves. The inline editor intercepts all four arrows
   in turned or stacked frames (plain frames keep the text box's own left and right). Caret rows are ordered by their
   drawn position, and Mongolian paragraphs are typeset line by line with their bands mirrored (`TextLineMirror`), so
   up and down follow the mirrored line order and clicks land on the drawn line. Stacks now follow centre and right
   alignment. `ShapeTextEditor` (Deck and Ledger shape text) hosts the shared `InlineTextEditor`: for stacked and
   Mongolian flows it composes the live shape into the same frame the renderer draws and edits on it, with the hidden
   text box only carrying input; other flows keep the overlay text box. Tests: `Round230StackedEditingTests`.

## Open items

Listed in `docs/audit-queue.md` (Deck, round 230).
