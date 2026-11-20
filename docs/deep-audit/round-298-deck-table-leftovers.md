# Round 298: Deck table leftovers (2026-11-20)

Area: the Deck table items queued by round 297. No builds or tests were run in the round (audit cycle rule); the
coordinator builds and tests at the join.

## Defects found

| Severity | Defect | Status |
|---|---|---|
| Medium | The slide renderer drew every cell with a fixed padding and horizontally, ignoring the cell margins and text direction the codecs read | Fixed |
| Medium | No dialog edited cell margins or text direction | Fixed |
| Medium | Arrows and Tab stepped into cells covered by a merge | Fixed |
| Medium | Typing into a selected cell did nothing; cells could be edited only in the table dialog | Fixed (type-to-edit) |
| Medium | A grid table was written to PPTX without `a:tableStyleId`, so other applications drew it with no lines | Fixed |
| Minor | No Insert Row Above / Insert Column Left | Fixed |
| Medium | ODP grid tables carry no `table:template-name` | Open, queued |
| Medium | Per-run formatting inside PPTX and ODP table cells is flattened to the cell look | Open, queued |

## Fixes

- `ITableCellBox` (`PulseOffice.Data.UI`) turns a cell area and its `TableCellMargins` into the text box; the shared
  `DataTableRenderer` uses it for tables with their own look and for any cell with a layout, and draws `Down` and `Up`
  text turned by 90 and 270 degrees inside that box and `Stacked` text one grapheme per line. Vertical alignment acts
  across the turned line, as in PowerPoint.
- `IDeckTableCellLayout` and the command `deck.table.cellLayout` (context menu and Format › Table): a form with the four
  margins in points and the direction, applied to the selected cells or the whole table, keeping each cell's vertical
  alignment and look; one undo step.
- `DeckTableKeys` anchors the current cell to its merge origin, moves Right and Down past the merge span, snaps Left and
  Up onto the origin of the merge they land in, and walks Tab and Shift+Tab over uncovered cells only.
- Typing a character with a table cell selected replaces the cell's text (merge origin, header names included); further
  characters append and Backspace erases the last grapheme while typing continues; Enter or any move ends it. Each
  keystroke is its own undo step (minor, queued).
- `deck.table.insertRowAbove` and `deck.table.insertColumnLeft` insert before the first selected row or column.
- PPTX: a table without its own look and with the grid on names the built-in "No Style, Table Grid" style
  `{5940675A-B579-460E-94D1-54222C63F5DA}`. Our reader does not resolve that id to a look (only the Medium Style 2
  family and the plain id are built in), so it reads back as a grid table; PowerPoint draws its thin black lines. The
  header tint and banding fills of our grid look are not part of that style (minor, queued).

## Researched, still open

- ODP `table:template-name` names a `table:table-template` in the styles; writing a name needs that template in
  `styles.xml` (cell styles for first row, banding and body), which our ODP writer does not emit yet. Without it the
  name points nowhere, so it was not written.
- Rich text in cells needs the table model to carry paragraphs with runs instead of plain values; both codecs, the
  renderer, the editor and exchange depend on the plain value, so it is a model change for its own round.
