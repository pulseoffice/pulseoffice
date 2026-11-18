# Round 231: Deck outline structure, empty sections, first slide number

Area: the Deck items left open by rounds 227 and 229 in `docs/audit-queue.md`. No builds or test runs in this round;
the coordinator runs the tests at the join.

## Defects fixed

1. **Medium.** Deleting a title line in the outline shifted every later text up one slide instead of merging two
   slides. Outline entries are now matched to the slides they were rendered from (`OutlineSlideMatching`,
   `PulseOffice.Presentations`): first by title, in order, then edited titles by position within the gap after the
   previous matched slide. Unmatched slides are deleted, unmatched entries become new slides, and the slides are
   moved into the outline order, all in one undo step. A deleted title therefore merges its body into the slide
   before it; deleting every line of a slide deletes it. Alt+Shift+Up/Down in the outline swaps the block (title and
   body lines) under the caret with its neighbour (`OutlineBlockMove`), which moves the slide when the outline is
   applied. Test: `Round231OutlineMatchingTests`.
2. **Medium.** Empty sections were lost. `Presentation.EmptySections` holds sections without slides (name,
   identifier, the slide they precede or none for the end); sections with slides keep their marker on their first
   slide. `SlideSections.Ordered` gives the full ordered list. Deleting the last slide of a section turns it into an
   empty section (undoable). PPTX writes empty `p14:section` elements and reads them back; the leading unsectioned
   slides are written in a "Default Section" with a fixed identifier (`PptxLeadingSection`) and read back as no
   section. ODP writes `urn:pulseoffice:presentation-section` `empty-section` elements in the page they precede (the
   last page, with `after-page`, for trailing ones). Tests: `Round231PptxSectionTests`, `Round231OdpSectionTests`,
   `Round231SectionDeleteTests`.
3. **Medium.** There was no "number slides from" setting. `Presentation.FirstSlideNumber` (0 to 9999) is set in the
   slide size dialog (one undo step with the size change), read and written as `p:presentation/@firstSlideNum` and
   as `style:first-page-number` on the slide page layout (0, which ODF does not allow there, as our extension
   attribute), and used by slide number fields, header and footer chrome, exports and printing
   (`Presentation.NumberOf`). Tests: `Round231PptxFirstSlideNumberTests`, `Round231OdpFirstSlideNumberTests`.
4. **Minor.** Connector Ends was reachable only from the Advanced surface; it is now in the slide object context
   menu (`object/format`). Test: `Round231ConnectorContextMenuTests`.

## Open items

Listed in `docs/audit-queue.md` (Deck, round 231): empty sections are not shown in the filmstrip or sorter, title
matching with duplicate titles, ODP first number 0 and `text:page-adjust`, and nothing opened in other applications.
