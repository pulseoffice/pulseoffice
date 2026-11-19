# Round 260: leftovers (continuation notices, Deck paragraph tags, filter scans, ODT margin offsets)

Area: four queue items carried over from rounds 243 to 255. No builds or tests were run in the round (audit cycle
rule); the tests below run at the join.

## Defects

| Severity | Defect | Status |
|---|---|---|
| Medium | Folio continuation notices could be selected only by a click: a drag or Shift+click from body text, and Select All, did not extend into them. | Fixed |
| Medium | Tagged PDF: Deck paragraphs were matched to drawn text by text, so two paragraphs with the same words (or a paragraph whose text appears in an earlier one) could take each other's tag. | Fixed for text elements |
| Minor | XLSX date-group and dynamic month filters looked up every row of the filter range at load (a million for a whole column). | Fixed |
| Minor | Folio bottom-margin and vertical outside-margin offsets went to ODT as `page` with the model offset, so other applications placed the object near the page top. | Fixed (initial page style) |

## Fixes

- `ILayoutReadOnlyText.OnPages` lists the notices of a page range; `IFolioLayoutTextSelection` (Documents.UI) derives
  the notices a selection covers: those of every page from the page of the selection start up to the page before its
  end, through the last page when the selection reaches the end of its story, plus a notice the pointer is on.
  `FolioEditor.LayoutTexts` exposes them; the overlay paints them all, a copy with no body text copies their text.
  The canvas passes the notice under the pointer while dragging, and Shift+click on a notice extends the selection.
- `IRichTextLineBoxes` (UI.Controls, shared) gives each composed line's paragraph, label and body box in page
  coordinates. `ISlideTextLines` composes a Deck text element for print and `SlideElementPlacement.Lines` carries the
  boxes; `SlideParagraphLocator` tags glyphs by position (the list label by its own box), as the table cell locator
  does; rotation handling moved to `SlideElementPlacement.Unrotated`, shared by both locators. Without boxes the text
  matching stays the fallback.
- `XlsxFilterDateGroups` and `XlsxDynamicFilterCodec` stop at `Worksheet.LastUsedRow`.
- `OdtFrameCodec.NativeVerticalShift`: for an offset (not aligned) object in the bottom or vertical outside margin the
  ODT writer adds the page height less the bottom margin of the initial page style to `svg:y` and keeps the model
  offset in `pt:y`, which our reader prefers. Vertical inside and the horizontal inside and outside relations are
  already native for odd pages.

## Tests

`Round260NoticePagesTests` (Documents.Layout.Tests), `Round260NoticeSelectionTests` (Documents.UI.Tests),
`Round260SlideParagraphLocatorTests` (Presentations.UI.Tests), `Round260FilterUsedRangeTests`
(Formats.OpenXml.Tests), `Round260NativeMarginOffsetTests` (Formats.Odf.Tests). All use one small in-memory input.

## Open

- Copying a selection that holds body text copies the body only, not the notices it crosses.
- Text drawn in shape objects on slides is still tagged by text matching; the line boxes come from a second
  composition of the frame, not the painted one.
- ODT native offsets use the initial page style, and inside/outside relations are native only for odd pages; aligned
  objects in those margins still align within the whole page in other applications.
- None of this has been opened in Word, the reference suite or a PDF/UA checker.
