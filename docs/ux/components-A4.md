# Components A4: BorderEditor (C6) and FillEditor (C7)

## BorderEditor (C6)

**Neutral model, no Avalonia** — `src/PulseOffice.Document/Borders/`:

- `BorderStroke(Dash, WidthPoints, ColorHex)`, `BorderDash` (solid, dotted, dashed, double).
- `BorderFrame`: eight positions (`Top`, `Bottom`, `Left`, `Right`, `InsideHorizontal`, `InsideVertical`, `DiagonalDown`,
  `DiagonalUp`), each with a stroke or absent. Immutable, value equality.
- `BorderFrameScope(InsideHorizontal, InsideVertical, Diagonals)` plus `Dashes`: what the surface can write.
  Single cell = no inside borders; range = inside borders in the directions where it has more than one row or column.
- `IBorderPreset` (Strategy) and `IBorderPresetCatalog`: none, outline, all, inside (ranges only), thick outline
  + thin inside, bottom, top, left, right, top and bottom. Adding a preset = one class.
- `IBorderFrameEditing`: toggles a side (same stroke → removes it, different stroke → restyles it), restyles all.
- `IBorderFrameResolver`: from (frame before, frame after, cell position in the range) to `CellBorderEdits`,
  side by side `Keep` or `Set(stroke)`. **Only changed positions are applied**: in a mixed range the dialog
  does not erase what the user did not touch.

**Adapters**:

| Surface | Class | Notes |
|---|---|---|
| Ledger, cells | `Sheets.Styling.ICellBorderFrames` | width → Thin/Medium/Thick (≤1 pt, ≤2 pt); diagonals yes |
| Folio, paragraph | `Documents.Editing.Borders.IBoxBorderFrames` | four sides; spacing from the border remains a separate field |
| Folio, table | `Documents.Editing.Tables.ITableBorderFraming` | frame from the selection, per-cell application with merges |
| Deck, table | `Presentations.UI.IDeckTableBorders` | solid stroke only (the `TableBorderLine` model has no style); a table with the generic look is first "materialised" into its own look with the same tints |

**View** — `src/PulseOffice.UI.Controls/Borders/`:

- `BorderEditor` (via `IBorderEditors.Create(scope, initial, metrics)`): presets with drawn icons,
  clickable diagram (`BorderSchema`), a row of toggles for each side with an accessible name, drawn line
  samples, width (`NumberBox` in pt), colour (`ColorFieldControl`). Changing style, width or colour restyles
  the sides that are on.
- `BorderSchema`: click on the nearest side (9 px tolerance); keyboard: arrows choose the side, Space/Enter
  toggles it; the focused side is dashed in the accent colour. Colours from the theme, redrawn on theme change.
- `BorderFramePainter` and `BorderSchemaGeometry` are services: the ribbon icons (`BorderGallerySlot`) can
  draw the same thumbnails with `BorderGlyph`.
- `BorderEditorField` (tabbed-form field) and `IBorderDialog` (standalone dialog with footer).

**Wiring**: Ledger's Format cells (Borders tab, replacing the seven drop-downs; cell or range depending on the
selection), Folio's Paragraph (Borders tab, replacing four check boxes + style/width/colour), Folio's Table
properties (replacing preset/style/width/colour), new Deck command `deck.table.borders`
("Table borders…", table context menu, on the cell selection or the whole table).

## FillEditor (C7)

**Neutral model** — `src/PulseOffice.Drawing/Shapes/FillEditing.cs`: `IGradientStopEditing` (add with the
interpolated colour, remove without going below two, move with reordering, recolour, reverse) and `IFillPatternCatalog`
(twelve patterns as hatch presets, recognised on the way back).

**View** — `src/PulseOffice.UI.Controls/Fills/`:

- `FillEditorModel`: current `ShapeFill`, page, selected stop. `FillPage` pages: none, solid colour,
  gradient, pattern, hatch, image; `FillEditorOptions(Pages)` says which to offer (`ShowTransparency`,
  `ShowPreview`). Pattern and hatch both write `ShapeFillKind.Hatch`: the pattern is a gallery, the
  hatch the free parameters. No new type in the model, hence no effect on ODF/OOXML.
- `FillEditor` (via `IFillEditors.Create(fill, options, metrics)`): tabs, `GradientStopBar` (click to add,
  drag to move; arrows ±1 %, ⇧ ±10 %, Del removes, Ins adds, Ctrl+Tab switches stop), stop colour and
  position, reverse; pattern gallery; transparency slider; checkerboard preview (`FillSwatch`).
  Event `FillChanged`.

**Wiring**: Fill tab of `ShapeStyleView` (Deck, Folio, Ledger: shapes and the slide background that uses it) and
Background tab of Ledger's Format cells (none/solid pages). Ready for Folio's paragraph and page background and
for the slide background on its own: just pick the pages the surface can write.
