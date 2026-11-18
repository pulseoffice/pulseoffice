# Round 226: splitting the oversized Folio canvas and paginator

Queue item: `FolioCanvas` (1,091 lines by type) and `Paginator` (1,285 lines by type, the nested pagination run
included) were over the 800-line cap of `DesignRulesTests` and carried exemptions. Both were split along their
responsibilities with no behaviour change; the two exemptions were removed. A survey of every other type in `src` found
none over 800 lines outside the remaining declared exemptions (`FunctionNameVocabulary`, `PulseMenuLayout`, `SlideView`,
`RtfReadSession`).

## Paginator (`src/PulseOffice.Documents.Layout/Flow`)

- `Paginator.cs` (78 lines): the request, the interface and the `Paginator` service, whose injected collaborators are
  now `internal` properties read by the run. Its constructor is unchanged, so no call site or test moved.
- `PaginationRun.cs` (600 lines): the page and column cursor, the paragraph visitor (breaks, keep-with-next,
  keep-together, widows and orphans, wrapped lines around obstacles), sections and column regions with balancing, and
  page open, reopen, close and new page. It exposes to its collaborators only what they read or move: geometry,
  cursor, column metrics, `Available`, `Advance`, `NewPage`, `Reopen`, the line and table lists.
- `FootnoteFlow.cs` (212 lines, with `PlacedNote`): the footnote area: placed and pending notes, page and per-column
  note heights, the cost of notes for a line, partial note fitting, continuation notices, widow and orphan cuts inside
  a note, carrying notes to the next column or page, and building the footnote `AreaBox` when the page closes.
- `TableFlow.cs` (437 lines, with `SplitOutcome`): table placement across pages: row groups, row splitting by cell
  atoms, carried row-spanning cells, repeated header rows, continuation heights; it owns the row, cell offsets and
  carried cells of the resume point and stamps them on each new `PageStart` (`Mark`).
- `ObjectFlow.cs` (146 lines, with `Obstacle`): anchored objects and frames: placement candidates, wrap obstacles and
  their outlines, committing objects when their paragraph is placed, page-anchored objects, frame and group member
  content, inline objects of a line.

## FolioCanvas (`src/PulseOffice.Documents.UI/Canvas`)

- `FolioCanvas.cs` (727 lines by type): measure and arrange on the desk, render order, pointer routing, caret, text
  hit testing, column-boundary drag, block selection, direct cursor, header and footer offer, drawing sketches,
  comment balloons.
- `FolioTextOverlays.cs` (229 lines): what is painted over and under the text of a page: selection map and selection,
  content-control ranges (with their cache) and title tabs, comment ranges, proofing waves, selected table cells,
  dot and baseline grids and helplines, and the rotation of lines inside turned frames.
- `FolioObjectGestures.cs` (191 lines): object hit testing and link members, the move, resize and rotate gesture with
  grid snapping and its commit, shape adjust handles, and painting of the primary and secondary object selection
  frames; the canvas forwards pointer moves, releases and capture loss to it and asks it for the helpline box.

`Round225HardCodedColourTests` allowances follow the moved literals (canvas 9, overlays 7, gestures 2; 18 before).
Builds and tests are run by the coordinator after the join.
