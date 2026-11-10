# Round 132: Ledger print text (wrap, rotation, decorations)

## Defects

| Severity | Where (before) | Defect |
|---|---|---|
| Serious | `src/PulseOffice.Sheets.Printing/SheetPrintDocument.cs:471` | A wrapped cell printed its text on one line, elided at the column edge; on screen it wraps. |
| Serious | same line | A rotated cell (any angle) printed flat; on screen it is turned. |
| Medium | same line | A line break inside the cell text reached the canvas as a control character in a single line. |
| Medium | `SheetPrintDocument.cs:461` | Underline, double underline and strikethrough were dropped: `PrintFont` had no decorations. |
| Minor | `SheetPrintDocument.cs` `Align` | Justify and Fill cells fell back to the value's own alignment (numbers right) while the grid draws them from the left. |

Tests written first and seen failing (5 of 5): `tests/PulseOffice.Sheets.Printing.Tests/CellTextPrintRound132Tests.cs`.

## Shared canvas extension (`PulseOffice.Printing`)

- `PrintFont` carries `Underline` (`PrintUnderline` None/Single/Double) and `Strikethrough` as init properties; `Undecorated`
  strips them. Scaled and clipped canvases forward them unchanged (`with` keeps them).
- `ITextDecorationPainter` / `TextDecorationPainter` draws the lines for a `PrintTextSpan` (start, baseline, width, font,
  colour, rotation) through `IPageCanvas.DrawLine`, turned with the baseline. Every concrete canvas calls it after drawing
  text: `PdfContentCanvas` (DrawText and DrawGlyphs), `AvaloniaPageCanvas` (DrawText inside its clip, DrawGlyphs), and the
  default `IPageCanvas.DrawGlyphs` fallback (glyphs drawn undecorated, one decoration across the run).
- `IPrintTextBlock` / `PrintTextBlock`: multi-line text in a box, word wrapping through the existing `ITextWrapper`
  (break at spaces, long words split by character, explicit line breaks kept), line height 1.2 em, vertical alignment of
  the whole block, clip only when the block overflows.
- `IPrintRotatedText` / `PrintRotatedText`: turned text as one `PrintGlyphRun` with measured advances, centred on a point;
  every canvas already draws turned glyph runs (PDF text matrix, Avalonia transform, clipped canvas splitting).

## Ledger

- `ISheetCellTextPainter` / `SheetCellTextPainter` (new, `PulseOffice.Sheets.Printing`) maps the cell style to a decorated
  `PrintFont` and chooses flat, block or turned drawing with the grid's rules: wrap only when `WrapText`, not rotated, not a
  right-aligned value, and the text does not fit; turned text is placed by the grid's own `ICellTextPlacement`.
- `CellTextPlacement` (with `CellBox`) moved from `PulseOffice.Sheets.UI` to `PulseOffice.Sheets` (`PulseOffice.Sheets.Styling`)
  so the grid and printing share it; it never depended on Avalonia.
- Wrapped and rotated cells no longer spill into neighbours when printed (as `CellOverflow` on screen).
- Not applicable: the cell model has no shrink-to-fit and no vertical stacked text, and the grid draws neither.

## Folio and Deck

Folio draws its own decorations (styles, colours, wave lines) as lines from the layout and passes undecorated fonts;
Deck replays Avalonia drawings whose decorations already arrive as lines. Both keep identical output; their test projects
pass unchanged.

## Tests run (one project at a time)

Sheets.Printing 44/44, Printing 72/72, Formats.Pdf 82/82, Architecture 43/43, Sheets.UI 494/494, Documents.UI 370/370,
Presentations.UI 118/118, Desktop.Tests filtered on printing 1/1. Solution build: 0 warnings, 0 errors.

## To check live

Print preview and PDF export of a Ledger sheet with: a wrapped cell in a tall row (top/middle/bottom), a cell turned 45
and 90 degrees, single and double underline, strikethrough, a cell with a line break; compare with the grid.
