# Deep audit round 71: turned cells and oversized nested rows

Area: the limits declared open by round 69 (`round-69-table-layout.md`).

## Changes

| # | Severity | Where | Limit | Fix |
|---|---|---|---|---|
| 1 | serious | `BlockStacker.RowAtoms` (`Flow/Stories.cs`) | a single nested-table row taller than a page was one unbreakable atom of its outer cell and overflowed the page | a splittable nested row (one row group, breaking allowed, not exact, no vertical cell, no row span) contributes one atom per band (`RowBands`: band k ends at the lowest bottom of line k of every cell). `Emit` coalesces consecutive bands of the same row into a single `TableBox`, so an unsplit row is emitted exactly as before; a split row is emitted with per-cell `CellSlice`s and continues on the next page, as Word does |
| 2 | serious | `BlockStacker.Place/TurnedLength` | a vertical cell's length was capped only at 22 in, so a long vertical text overflowed the page | `ITableComposer.Place(table, width, turnedLimit)`; the paginator passes the body height. The lying line length is capped at the page minus the cell padding, and the text wraps into more lying lines (continues in the next line slice). Exact rows keep their height (unbreakable, as in the other word processors). Nested tables still use the 22 in cap |
| 3 | medium | `CanvasSurface.Embedded` (`PageSurfaces.cs`) | inline objects inside a vertical cell were printed unrotated | the object's centre is turned with the open turns and the turn degrees are added to its rotation. Turn stack factored into `ObjectTurns` (shared with print marks) |
| 4 | medium | `FolioPrintMarks.MarkVisitor` | PDF links, bookmark/heading targets and comment notes inside a vertical cell used lying coordinates | the visitor follows `VisitTurn/LeaveTurn` and maps every mark through `ObjectTurns.Bounds` |
| 5 | medium | `CaretNavigator`, `LayoutLines`, `LineCells` | arrows in a vertical cell followed the lying lines; lines of a turned frame were not mapped to their cell, so up/down could jump out by lying coordinates | `ILineCells.TurnOf`, `ILayoutLines.TurnOf`; arrows are remapped to the reading direction (90: up/down move along the text, left = next line, right = previous line; 270: mirrored); line moves inside a turned frame stay among its lines; plain lines ignore turned lines |

## Tests

- `tests/PulseOffice.Documents.Layout.Tests/TableTurnedAndSplitTests.cs`: `ASingleNestedRowTallerThanAPageContinuesOnTheNextPage`, `AVerticalCellLongerThanAPageIsCappedAtThePageAndWrapsIntoMoreLines` (both failed before the fix).
- `tests/PulseOffice.Documents.UI.Tests/FolioTurnedCellNavigationTests.cs`: arrows in both directions, link mark standing inside the cell, inline object printed turned.
- Runs: Documents.Layout 238/238, Documents.UI 345/345, Documents 348/348, Formats.Pdf 77/77, Architecture 40/40.

## Remaining limits

- A row that may not break (exact height, cantSplit) taller than a page still overflows, as in the other word processors.
- Vertical cells inside nested tables are capped at 22 in, not at the page.
- At the edge of a vertical cell the line moves stop (they do not leave the cell sideways).
- `ColumnBalancer` still places tables with the 22 in cap (only an estimate).

## Live checks for the coordinator

- Nested table with one very tall row: continues on the next page; borders closed on each page.
- Long vertical text: row stops at the page bottom, text wraps into more columns.
- PDF export: link in a vertical cell clickable at the turned position; image in a vertical cell printed rotated.
- Arrow keys inside a vertical cell, both directions.
