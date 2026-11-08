# Round 70: render cost outside Folio

Round 68 ([round-68-folio-scale.md](round-68-folio-scale.md)) found that Folio repainted every page on every frame.
This round looked for the same class of defect in Ledger and Deck. Folio table layout was out of scope (another round).

## Checked

- **Ledger grid** (`SheetGridView.Render`, `GridGeometry.VisibleCells`, `SheetLayout` offsets): the visible range is
  computed from the scroll position and the surface size, not the control bounds; row/column offsets are cached
  (`AxisOffsets`) and `RowAt`/`ColumnAt` are binary searches. No timer drives the grid. Measured headless with a
  million used rows (1,200 x 800 px, `RenderTargetBitmap`, Debug): frame at the top 4.0 ms, at the bottom 4.8 ms,
  scroll frame 4.9 ms, typing (write + frame) 5.0 ms per key. No defect.
- **Deck filmstrip and slide sorter**: `SlideFrameCache` already rebuilds only the thumbnails whose
  `SlideThumbnailStamp` changed. 200 slides, editing one: 1 rebuild per edit, 0.46 ms per refresh.
- **Presenter view**: the 250 ms timer only sets two clock texts. **Deck caret pulse**: a one-shot ribbon refresh.

## Defect found (medium)

`SlideView` starts a 50 ms `DispatcherTimer` that calls `InvalidateVisual` whenever its slide holds an animated GIF.
Filmstrip and slide sorter thumbnails are `SlideView`s, so each thumbnail of a slide with an animated GIF repainted its
whole slide 20 times a second, visible or not, and each thumbnail also decoded the GIF with its own probe cache. A
deck with many GIF slides kept the UI thread busy just by being open.

Fix: new Strategy `StillImageProbe` (`PulseOffice.UI.Controls.Objects`, implements `IAnimatedImageProbe`, never
animated). `DeckFilmstrip` and `DeckModeViews` (sorter) give it to their thumbnails; the editing canvas, the show and
the presenter keep the animating probe. `SlideView.PlaysAnimatedImages` exposes whether the clock runs.

## Tests

- `tests/PulseOffice.Sheets.UI.Tests/LedgerRenderCostTests.cs` (serial collection `Ledger performance`): a million
  used rows, frames at top and bottom, scrolling and typing within bounds; numbers in
  `$TMPDIR/pulseoffice-ledger-render.txt`.
- `tests/PulseOffice.Presentations.UI.Tests/DeckRenderCostTests.cs` (serial collection `Deck performance`): a GIF slide
  animates on the canvas but not in a thumbnail; 200 slides, editing one rebuilds one thumbnail per edit; numbers in
  `$TMPDIR/pulseoffice-deck-render.txt`.

## Not covered here

Ribbon/status bar, comment and review panels were not measured in this round.
