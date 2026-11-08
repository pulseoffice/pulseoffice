# Deep audit round 19: Ledger grid view

Scope: the view part of `src/PulseOffice.Sheets.UI` (grid painting, headers, frozen panes, viewport, selection and fill
drag, in-cell editing, overlays, zoom, page breaks). `src/PulseOffice.Sheets.Engine` untouched (another agent).

## Coverage

Previous logs (`ledger-ui*.md`, `ledger-round7.md`, `ledger-commands-round6*.md`, `ledger-core-round15.md`) already list
every grid file as read in full. A cross-check of all non-command `.cs` files of the project against the file names in
`docs/deep-audit/*.md` left one file never read:

| File | Lines read / total | Outcome |
|---|---|---|
| GridParts/Painting/GridPaints.cs | 57/57 | clean (bounded caches, dash styles are shared statics) |
| GridParts/Painting/GridOverlayPainter.cs | 202/202 (re-read for this round's targets) | 1 defect (G19-1) |
| GridParts/Painting/GridCellPainter.cs | 231/231 (re-read) | clean: loops bounded to the visible range, overflow reach 32 columns clamped to XFD |
| GridParts/Gestures/FillGesture.cs | 45/45 (re-read) | clean |
| LedgerViewParts/LedgerCellOperations.cs (`ApplyFill`) | 100-171 (re-read) | verified, not a defect (see below) |

## Defects

### Minor
- **G19-1** `GridParts/Painting/GridOverlayPainter.cs:101` (before the fix): the row-and-column highlight was a fixed
  4 % black tint, invisible on the dark theme's dark surface. Now `CrossTint` derives the tint from the theme text
  colour (6 %), passed in by `SheetGridView`. Test: `LedgerGridRound19Tests.The_row_and_column_highlight_follows_the_theme_text_colour`
  (failed before the fix with the old colour).

### Verified, not defects
- Fill drag of whole columns sideways or whole rows downwards (A:A → A:C, 2 M target cells): finishes well under three
  seconds and respects undo; empty sheet produces no history step. Regression tests kept in `LedgerGridRound19Tests`.
- Hidden markers and page breaks at row 1048576 / column XFD: `CellAddress` is a plain record and layout lookups out of
  range return the default, so no exception.

### Remaining (minor, not fixed)
- Per-frame allocations of `Pen`/`SolidColorBrush` in `GridOverlayPainter.PaintSelection`, `PaintPageBreaks`,
  `PaintHiddenMarkers`, `PaintChoiceButton` and in `GridCellPainter.Paint`/`PaintFrozenPanes`: a handful per frame, not per
  cell; could go through `IGridPaints`.
- Page-break pen is fixed `MediumBlue`, readable on both themes but not themed.

## Live checks
- Dark theme, View > highlight row and column: the cross must be visible.
- Drag the fill handle of a whole selected column two columns to the right: quick, one undo step.
