# Shared components A2: thumbnail gallery (C2) and previews (C3)

Usage notes for blocks B–G of the [UX plan](../ux-plan.md). Everything lives in `PulseOffice.UI.Controls`, except the
neutral page model, which is in the core (`PulseOffice.Document.Pages`, no dependency on Avalonia).

## C2 — `ThumbnailGallery<T>` (`Galleries/`)

```csharp
var gallery = new ThumbnailGallery<MyItem>(
    items,                       // IReadOnlyList<ThumbnailGalleryItem<T>>(Value, Caption, Group?, Keywords?)
    painter,                     // IThumbnailPainter<T>: Create(item, size) and Repaint(thumbnail, item)
    navigator,                   // IGalleryNavigator, singleton in DI (GalleryNavigator)
    new ThumbnailGalleryOptions { TileWidth = 112, ThumbnailHeight = 66, Columns = 4 },
    metrics);
gallery.SelectionChanged += (_, item) => model.Choose(item);   // click, arrows, Home, End
gallery.Invoked += (_, item) => Confirm();                      // double click, Enter
```

- **Painter (Strategy)**: the gallery does not know what it draws. `Create` builds the thumbnail control
  once (when the tile is realised), `Repaint` updates it; `RefreshThumbnails()` repaints the realised
  tiles when the model state changes (Fontwork text, table header row…).
- **Selection**: `Select(index)` / `Select(item)` are silent (they sync the view with the model);
  `SelectionChanged` fires only from user actions. The "chosen" state is the `checked` class of the theme's
  `Button.ribbon.tile` tiles, which also provide hover and focus.
- **Keyboard**: arrows, Home, End, Enter are handled by the gallery. A view that already has its own tunnelling
  handler (Fontwork: arrows work even with focus on the dialog) calls `HandleKey(key)` or `Navigate(move)`.
  Up and Down cross groups keeping the column; the computation is in `GalleryNavigator`, pure and tested.
- **Groups**: the item's `Group`; the gallery sorts items by group (in order of first appearance) and puts
  a small-caps header above each. Without groups, no headers.
- **Filter**: `Filterable = true` adds a search box (caption and `Keywords`, case- and
  accent-insensitive); `EmptyText` is the message when nothing is left. Down from the box goes to the first tile.
- **Sizes**: `TileWidth`, `ThumbnailHeight`, `ThumbnailWidth` (NaN = tile width), `TilePadding`,
  `CaptionGap`, `FrameRadius`, `FrameBorder`, `Spacing`; `Columns` fixes the grid width, otherwise
  columns follow the available width. `Scrolls` wraps the grid in a vertical scroller.
- **Virtualisation**: above `VirtualizeAbove` items (150) the gallery switches to rows in a
  `VirtualizingStackPanel` at most `MaxViewportHeight` tall: only visible thumbnails are created, and keyboard
  navigation brings the row into view before giving focus.

Galleries already ported, with appearance verified pixel by pixel by `GalleryPixelTests` (reference images taken from
the previous code in `tests/PulseOffice.Desktop.Tests/Golden/`):

| Gallery | Painter | Options |
|---|---|---|
| Fontwork (`FontworkGalleryView`) | `FontworkWarpPainter` | 112 × 66, 4 columns, `Radius` frame |
| Table styles (`TableInsertView`) | `TableLookPainter` | 116 × 58, 4 columns, padding 5, `RadiusTight` frame |
| Deck themes (`ThemeDialogView`) | `ThemeThumbnails` | 150, thumbnail 140 × 16:9, Document/Catalogue groups, borderless, scrolling |

`Tables/TableStyleGalleryView` (Table style in Ledger, Deck and Folio) now also uses the gallery, with the same `TableLookPainter`
(public in `Tables/`, reads header row and banded rows from `ITableLookBanding`): 136 × 72, 4 columns, ribbon tiles.

## C3 — `LivePreviewPane` and `PagePreview` (`Preview/`)

```csharp
var pane = new LivePreviewPane(source, timers, new LivePreviewOptions
{
    Backdrop = PreviewBackdrop.Checkerboard,     // Surface, Checkerboard, Page (white sheet with shadow)
    UnavailableText = texts["preview.unavailable"],
}, metrics, logger);
model.Changed += (_, _) => pane.Invalidate();   // 60 ms debounce (Delay)
```

- `IPreviewSource.Render()` returns the control to show, or `null` if the preview is not possible; a state
  or argument exception also leads to the "unavailable" state (text in `UnavailableText`, English warning
  in the log).
- `FitToFrame` (default) shrinks the content to the frame without enlarging it (`Viewbox` `DownOnly`).
- The debounce goes through `IDebounceTimerFactory` (in DI `DispatcherDebounceTimerFactory`): tests use a manual
  timer. `RefreshNow()` updates immediately; `Dispose()` stops the timer.

`PagePreview` draws a core `PageSketch` (`PulseOffice.Document.Pages`): width and height in any
unit as long as it is consistent, orientation (the long side goes where `Orientation` says), margins, header and footer
bands (`PageSketchBand`: height, distance from the body, left, centre, right texts), columns with spacing, body as text lines, cells or blank, paper colour and watermark. The geometry is computed by
`IPageSketchLayout` (in DI `PageSketchLayout`), pure and tested without UI. Each module translates its own
model into a `PageSketch`:

- **Folio, Page setup**: page format and margins, `Header`/`Footer` with height and spacing, section
  columns, `Body = TextLines`, watermark.
- **Ledger, Page setup**: format and margins, header and footer with the three areas as texts, `Body = Cells`.
- **Deck, slide size / header and footer**: slide dimensions, zero margins, `Footer` with
  date, footer and number as left/centre/right texts, `Body = Blank`.
