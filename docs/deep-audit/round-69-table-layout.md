# Deep audit round 69: Folio table layout

Area: the limits left open by round 67 (`round-67-folio-tables-3.md`): vertical text in cells, per-cell padding in the
paginator, the rest of `Paginator.cs` for tables.

## Coverage

| File | Lines read / total | Outcome |
|---|---|---|
| `src/PulseOffice.Documents.Layout/Flow/Paginator.cs` | 1282/1282 | defects 2, 3 |
| `src/PulseOffice.Documents.Layout/Flow/Stories.cs` | 397/397 | defects 1, 3 |
| `src/PulseOffice.Documents.Layout/Tree/LayoutTree.cs` | 1-500 | defect 1 |
| `src/PulseOffice.Documents.Layout/Queries/LayoutQueries.cs` | 104/104 | defect 1 |
| `src/PulseOffice.Documents.UI/Rendering/PageSurfaces.cs` | all | defect 1 (print fills ignored turns) |
| `src/PulseOffice.Documents.UI/Rendering/PageRenderer.cs` | 380-438 | defect 1 |
| `src/PulseOffice.Documents.UI/Canvas/FolioCanvas.cs` | 900-1000 (turned stories) | defect 1 |
| `src/PulseOffice.Documents.UI/Commands/FolioTableDialogs.cs` | 1-240 | defect 1 (UI) |
| `src/PulseOffice.Documents/Editing/TableEditing.cs` | 225-255 | defect 4 |
| `src/PulseOffice.Documents.UI/Shell/Snapshot/BlockConverters.cs` | 85-115 | defect 5 |

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | serious | layout, screen, print/PDF, hit testing, UI | `TableCell.TextDirection` was kept but cells were set horizontally | `BlockStacker` composes a vertical cell lying down (line length = exact row height, otherwise the natural length of its longest line, capped at 22 in) and stands it up: the cell's placement story is one atom as tall as the text length, so the row grows to it and the paginator treats it as unbreakable; the content is emitted in a `TurnedBox` (90 degrees for top-to-bottom, 270 for bottom-to-top) whose frame is centred on the cell's content area; vertical alignment acts across the lines. `TurnedBox` and `ObjectBox` share `ITurnedFrame`; `PageBox.TurnedFrames` finds them all; `LayoutQueries.HitTest` untwists a point inside a turned frame and searches only its lines, and ignores turned lines otherwise; `FolioCanvas` turns caret and selection for any turned frame; the page painter opens a turn for `TurnedBox` (screen transform, print glyph rotation, print fills turned too). Table properties dialog: "Text direction" (horizontal, top to bottom, bottom to top) in five languages with three icons, `ITableFormatting.SetTextDirection` (one undo step) |
| 2 | medium | `Paginator` | rows split across pages used the table's vertical inset (`2 * inset`) for every cell, so a cell with a larger own top/bottom padding had its last line inside the padding or past the cell bottom | every slice, carry, continuation and part height uses the cell's own `Insets` (`ITableComposer.Insets`); row-level checks use the largest padding of the row |
| 3 | serious | `BlockStacker.Compose/ComposeFrom` | a nested table was one atom of its cell's story: a nested table taller than a page could not break and was drawn past the page bottom (content lost on screen and in PDF) | a nested table contributes one atom per row group (rows tied by vertical merges), so the outer row breaks between nested rows |
| 4 | minor | `TableEditing.Heir/Like` | merging/splitting cells and inserting rows/columns dropped the text direction and own padding of the model cell | copied |
| 5 | minor | snapshot `TableCellConverter` | the editor snapshot (recovery) dropped text direction and own padding | written and read |

## Paginator, verified without defect

- A row taller than a page that may not break (`cantSplit`, exact height) is placed whole on an empty column (`free && !Splittable`), so the loop terminates; it overflows the page bottom as in the other word processors.
- A splittable row taller than a page progresses at least one atom per page (`force` on an empty column); `Split` returns `None` only on a non-empty column, which then advances.
- Repeated header rows are emitted only when `header < Available`, so the following `Available <= Epsilon` check cannot loop.
- `Overflowed` marks all atoms consumed, so a continuation never re-emits content.

## Open / declared limits

- A single nested row taller than a page, and a vertical cell longer than a page, still overflow the page (unbreakable unit).
- Inline objects inside a vertical cell are printed unrotated (`CanvasSurface.Embedded` ignores turns); PDF link/bookmark marks in vertical cells are at the unrotated position.
- Arrow-key navigation in a vertical cell follows the lying lines (up/down move across lines, i.e. left/right on screen).

## Tests

- `tests/PulseOffice.Documents.Layout.Tests/TableTurnedAndSplitTests.cs`: turned layout for both directions, exact row, hit test inside and beside a turned cell, own padding on a split row (failed before the fix: line bottom 181 inside the 25 pt padding of a cell ending at 194), nested table breaking between rows.
- `tests/PulseOffice.Documents.UI.Tests/FolioTurnedCellPaintTests.cs`: screen painter opens a 90/270 turn around the vertical text only; print canvas receives the glyph run rotated by -90/-270, the plain cell unrotated.
- Runs: Documents.Layout 236/236, Documents.UI 338/338, Documents 348/348, Formats.Pdf 76/76, Localization 32/32, Architecture 40/40.
