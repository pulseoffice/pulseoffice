# Round 229: Deck slide management and outline

Area: slide sorter and filmstrip moves, sections, outline view, hidden slides, header and footer chrome, slide size,
reused slides, PPTX (`sldIdLst`, `p14:sectionLst`, header and footer placeholders) and ODP (page order, section
extension, footer and date-time declarations) round trips. No builds or test runs in this round; the coordinator
runs the tests at the join.

## Defects fixed

1. **Medium.** The outline view showed every body paragraph with one tab and stripped extra tabs when applied, so
   bullet levels could not be seen, promoted or demoted from the outline. The outline text is now the shared
   `DeckOutlineText` service (`PulseOffice.Presentations`): one tab per level (four spaces count as one), parsed into
   `OutlineEntry` items; `DeckTextActions.SetSlideOutline` applies titles, body text and levels in one undo step.
   Tests: `Round229OutlineTextTests`, `Round229OutlineActionTests`.
2. **Medium.** A title with several paragraphs produced several unindented outline lines, so on leaving the outline
   every later slide received the texts of the slide before it. Titles are rendered on one line and left untouched
   when that line did not change. Test: `Round229OutlineTextTests`.
3. **Medium.** Slides typed after the last one in the outline were dropped silently; they are now inserted as new
   slides in the same undo step as the other outline edits.
4. **Medium.** PPTX header and footer placeholders were written on every slide even when the slide's master hides
   date, footer or slide number, so other applications showed chrome that Deck hides. Test:
   `Round229PptxHeaderFooterTests`.
5. **Medium.** Reading a foreign PPTX always set "show on title slide", so a deck whose title slide carries no date,
   footer or number gained them. The setting is now read from the title-layout slides. Test:
   `Round229PptxHeaderFooterTests`.
6. **Medium.** With slides before the first section, `p14:sectionLst` did not list every slide, which PowerPoint
   requires. Those slides are written in a leading "Default Section". Test: `Round229PptxSectionTests`.
7. **Minor.** A layout change dropped the slide's section identifier, so the section GUID changed on the next save.
   Test: `Round229RelayoutSectionTests`.
8. **Minor.** Deleting the first slide of a section handed only the name to the next slide, and moving slides moved
   section names without their identifiers. Both now move name and identifier together (`SectionMarker`). Tests:
   `Round229SlideSectionActionTests`, `Round229SectionMoveTests`.

## Checked without defects

Hidden slides (one undo step, kept by duplicate, relayout and import), slide numbering counting hidden slides,
slide size change with and without fitting (masters and elements in one step), section add, rename and remove as one
step each, ODP section attributes and footer declarations.

## Open items

Listed in `docs/audit-queue.md` (Deck, round 229): outline deletion of title lines, empty sections and the leading
default section, and the missing first slide number.
