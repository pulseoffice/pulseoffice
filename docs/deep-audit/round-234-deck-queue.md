# Round 234: Deck items of the audit queue

Scope: the Deck code items of `docs/audit-queue.md` left by rounds 229 to 233. No builds or test runs in this round;
the coordinator builds and tests at the join.

## Defects fixed

1. **Medium. Upright East Asian glyphs overlapped.** Each upright cluster now gets a cell as long as its glyph is tall:
   `StyledParagraph` (shared rich text typesetting) knows the upright clusters of an East Asian vertical paragraph
   (`Cells`) and, whenever it builds formatted text, enlarges the hidden cluster's font size by the ratio of the glyph
   height to its advance, so line breaking, caret, selection and hit testing all see the wider cell. The painted glyph
   is built at its own size (`StyledParagraph.Glyph`) and, through `VerticalForms` (`PulseOffice.Text.Rich`), the
   full-width punctuation that stands upright is replaced one for one by its vertical presentation form
   (U+FE10–FE19, U+FE30–FE4F), so text indices do not move. Test: `Round234UprightCellTests`.
2. **Medium. Vertical, 270-degree and upright shape text was edited in a turned overlay box.** `TextFlowFrame.Composes`
   is the single rule for flows that go through the composed text frame: any vertical flow, upright text and stacked
   text. `ShapeListText.Draws` (renderer) and `ShapeTextEditor.InPlaceFor` (editor) both use it, so these flows are
   drawn on the composed frame and edited in place through the shared `InlineTextEditor`, as round 230 did for stacked
   and Mongolian text. Deck and Ledger shape text are covered. Test: `Round234UprightCellTests`; the round 230 test now
   expects the vertical flow to be composed.
3. **Medium. Outline edits lost run formatting.** `ParagraphLineMerge` (`PulseOffice.Text.Rich`) maps outline lines
   to paragraphs by a longest common subsequence of their texts: unchanged paragraphs are kept as they are, edited
   lines are spliced into the paragraph at the same place in the gap, new lines take the formatting of the paragraph
   before them, removed lines drop their paragraph. `DeckTextActions.SetSlideOutline` uses it for titles and bodies.
   Titles with several paragraphs are shown on one outline line with a pilcrow separator and split back into their
   paragraphs (`DeckOutlineText`). Test: `Round234OutlineFormattingTests`.
4. **Medium. Empty sections were invisible in the app.** `SlideStripLayout` (`PulseOffice.Presentations`) lists the
   headers and slides of the filmstrip and sorter in order, empty sections included. The shared `DeckSectionHeaders`
   builds the headers for both views (empty ones in the disabled text colour), renames them in place on double click
   and offers Rename and Remove in the header's context menu; `DeckSlideActions.RenameEmptySection` and
   `RemoveEmptySection` are undoable. The sorter is now a column of section headers and wrap panels; its drop target is
   measured on the slide frames. Moving a slide that an empty section precedes re-points the section to the slide that
   followed it, so the section stays in place. Test: `Round234EmptySectionTests`.
5. **Minor. "Number slides from 0" in ODP relied on our extension only.** Every `text:page-number` field in content
   and styles now carries `text:page-adjust="-1"`; the reader takes a uniform negative adjust as the first number when
   no page layout attribute gives one. Test: `Round234OdpSlideNumberAdjustTests`.
6. **Medium. Deck ODP `fo:clip` was measured on the frame.** Folio's `OdtImageClip` became the shared `OdfImageClip`
   (`Shapes/OdfImageClip.cs`), injected into `ImageObjectWriter` and `DeckImageCodec`: the clip is measured on the
   original image (pixel size at 96 dpi, or the frame unscaled by the crop when the size is unknown), so other
   applications crop the same area. Test: `Round234OdpImageClipTests`.

## Open items

Listed in `docs/audit-queue.md` (Deck, round 234): the line thickening caused by the widened upright cells, the
changed drawing path of vertical shape text, a pilcrow inside a title, dropping a slide into an empty section, and
nothing seen live or opened in other applications.
