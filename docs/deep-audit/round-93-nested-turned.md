# Deep audit round 93: nested vertical cells and caret navigation

Area: the limits left open by round 71 (`round-71-turned-cells.md`), then Folio's caret navigation
(`CaretNavigator`, `LayoutLines`, `LineCells`) for tables, nested tables, frames, notes, headers/footers, RTL and fields.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| `src/PulseOffice.Documents.Layout/Flow/Stories.cs` | composer part (140-470) | defects 1 |
| `src/PulseOffice.Documents.Layout/Flow/Placement.cs` | balancer (160-270) | defect 2 |
| `src/PulseOffice.Documents.UI/Editing/CaretNavigator.cs` | 129/129 | defects 3, 4, 5 |
| `src/PulseOffice.Documents.UI/Editing/LayoutLines.cs` | 128/128 | defects 4, 5 |
| `src/PulseOffice.Documents.Layout/Queries/LineOffsets.cs` | 116/116 | no defect |
| `src/PulseOffice.Documents.UI/Editing/FolioEditor.cs` (`Move`) | 280-315 | no defect |
| `src/PulseOffice.Documents.UI/Editing/FolioKeyMap.cs` (motions) | 50-90 | no defect |
| `src/PulseOffice.Documents/Editing/Stories.cs`, `ParagraphOrder.cs` | story catalog and order | no defect |

## Defects and fixes

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | medium | `BlockStacker.Place/Compose/Standing` (`Flow/Stories.cs`) | a vertical cell inside a nested table was capped at 22 in, not at the page: long vertical text overflowed the page | the turned limit is threaded through the nested composer: `Compose(items, width, turnedLimit)` (private), each cell passes the outer limit minus its own vertical padding to its content (horizontal and lying), `ComposeFrom` (linked frames) uses its own height limit |
| 2 | minor | `ColumnBalancer.Atoms` (`Flow/Placement.cs`), `Paginator` | the balancer estimated tables with the 22 in cap, so balanced heights differed from what the paginator placed | `BalanceRequest.TurnedLimit` (the paginator passes the body height, as for its own placement); the balancer places tables with it and reuses the paginator's cached placement |
| 3 | medium | `CaretNavigator.Vertical` | at the edge of a vertical cell, line moves stopped inside the cell | in a turned cell, a line move with no further lying line leaves the cell in reading order: onward to the start of the next paragraph of the story (the next cell), back to the end of the previous one; the preferred x is reset |
| 4 | medium | `LayoutLines.Neighbour` | header/footer tables are laid out as `StoryKind.Cell` lines and were treated as body: Down from the last body line of a page landed in the next page's header table (another story); Up from the first body line likewise | `LineScope`: a line move only admits lines whose paragraph belongs to the caret's story (notes keep moving between notes of the same kind); also fixes moves between different frames, which all share `StoryKind.Frame` |
| 5 | medium | `LayoutLines.Neighbour` | Down from the last line of a header or footer went to the copy on the next page, i.e. to the header's first paragraph again (or, in a header table, back into the same cell) | repeated stories (header/footer default, first, even) do not cross pages: the move stays on the page and ends at the line end |

## Checked without defects

- Home/End, word moves, document start/end: logical, story-bounded (`ParagraphOrder.StoryOf`), clusters respected.
- Selection extension: `FolioEditor.Move` keeps the anchor and uses the same navigator, so every fix above applies to Shift+arrows.
- Fields: their result text is source text of the paragraph, so `LineOffsets` maps offsets one to one; note citations (zero source length) are skipped by `AfterCitations`.
- Nested tables: `LineCells` maps nested cells recursively; `Reachable` keeps moves inside the same row of the same table.
- Footnotes: lines of all footnotes of a page share the flow, as in Word; unchanged.

## Remaining limits

- Folio has no paragraph direction (no RTL/bidi model): arrows are logical; visual RTL movement would first need bidi in the model and in layout.
- In a vertical cell Left/Right collapse a selection to its logical start/end, not to the physical side.

## Tests

- `tests/PulseOffice.Documents.Layout.Tests/TableTurnedAndSplitTests.cs`: `AVerticalCellInsideANestedTableIsCappedAtThePageNotAtTwentyTwoInches` (failed before).
- `tests/PulseOffice.Documents.Layout.Tests/ColumnBalancerRound17Tests.cs`: `TheBalancerEstimatesTablesWithThePageLimitOfVerticalCells` (failed before); `LayoutFixture` takes an optional service replacement.
- `tests/PulseOffice.Documents.UI.Tests/FolioTurnedCellNavigationTests.cs`: `LineMovesAtTheEdgeOfAVerticalCellLeaveTheCellInReadingOrder` (both directions, failed before).
- `tests/PulseOffice.Documents.UI.Tests/FolioCaretStoryBoundsTests.cs`: body to next page does not enter the header table, Down from the last header line stays, Down from a header table cell stays (the first two failed before).
- Runs: Documents.Layout 240/240, Documents.UI 360/360, Architecture 43/43.

## Live checks for the coordinator

- Nested table with a long vertical cell: the row stops at the page bottom and the text wraps into more columns.
- Two-column balanced section containing a vertical cell: columns balanced as laid out.
- Arrow keys at the first/last column of a vertical cell: the caret leaves the cell to the next/previous cell.
- Header with a table: Down at the bottom of page 1 body goes to page 2 body; Down at the end of the header stays in it.
