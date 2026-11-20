# Round 285: table region overrides, preset text areas, image compression and aspect lock (2026-11-20)

Area: the round 279 and 281 leftovers (Folio table templates, shape text areas), then Folio image compression and
resizing with the aspect ratio kept, across the three modules.

| Severity | Defect | Fix |
|---|---|---|
| Medium | A derived table style could not take away a region line or font that its parent sets: a zero line width or an emptied font or size field was stored as "not set", so the parent's value came back | The editor stores a cleared field as an explicit override: a zero-width `BorderLine` on every side, an empty font, a size of 0. `TableAppearance` treats zero-width lines, empty fonts and zero sizes as "none" after merging. DOCX writes a zero line as a `w:val="nil"` side of `tcBorders` and reads an explicit `nil`/`none` side back as a zero line (Word's own built-in styles use `nil` this way). ODT writes only the sides a region sets (`fo:border-*`, `none` for a zero line) in `pt:table-region` and in the native template cell styles, and reads an explicit `none` back as a zero line. |
| Medium | Pictures resized from a corner lost their proportions unless Shift was held, in Folio, Ledger and Deck; Word, Excel, PowerPoint and the reference suite keep a picture's proportions by default | `IObjectManipulator.KeepsAspect` (raster and vector pictures) and `Begin(..., keepsAspect)`; the transform session inverts Shift for pictures, so Shift frees them. Folio's object gestures, Ledger's object drag and Deck's transform gesture pass the selected content. |
| Medium | Many preset shapes (arrows, flowchart shapes, octagon, hexagon, can, document, parallelogram, trapezoid…) used hand-written text areas that differed from the DrawingML text rectangles, so text wrapped differently from PowerPoint and Word | `ShapeTextArea` evaluates the preset `a:rect` of every preset shape through `PresetGeometryEngine.TextBounds` (the full definitions are already in `PresetShapeDefinitions.xml`); only the rounded rectangle keeps its own rule, because its corner comes from the model's `CornerRatio`. |
| Medium | Shapes with an ODF enhanced geometry got no text inset at all; `draw:text-areas` was kept but ignored | `IEnhancedPathInterpreter.TextArea` evaluates the first text area (equations, modifiers, view box) into fractions of the box, and `ShapeTextArea` applies it to formula geometries. |
| Minor | Native ODT table templates named no paragraph styles; other ODF applications take the text formatting of a template part from `table:paragraph-style-name` | Each template part with text formatting gets a paragraph style (`<style>.<part>.paragraph`) named by `table:paragraph-style-name` and marked with our `pt:paragraph-style` attribute; the reader skips those paragraph styles (they do not appear as user styles) and, for templates from other applications, layers the part's paragraph style under its cell style's text properties. |
| Minor | Compress Pictures could not delete the cropped areas | The compression dialog shows "Delete cropped areas" (on by default, only for a cropped picture); the pixels outside the crop are dropped and the crop is cleared, in the three modules (shared `RasterImageFiles`). |

Research: ECMA-376 17.7.6 (`tblStylePr`/`tcBorders`: `nil` removes an inherited border) and 20.1.9.22 (`a:rect` of
preset geometries); ODF 1.3 part 3 for `table:paragraph-style-name` on template parts and `draw:text-areas`.
Office's compression dialog sets a target resolution relative to the displayed size and can apply to all pictures.

Compatibility note: `pt:table-region` borders written by round 279 (a few hours earlier, never released) wrote `none` for
unset sides; those are now read as explicit "no line".

Tests: `Round285ShapeTextAreaTests`, `Round285PictureAspectTests` (Drawing), `Round285TableRegionClearTests`
(Documents), `Round285TableStyleClearFormTests` (Documents.UI), `Round285TableRegionClearStyleTests`,
`Round285TableTemplateParagraphTests` (Formats.OpenXml), `Round285CompressCropTests` (UI.Controls).

Open (queued):
- DOCX cannot say "no font" or "no size" in a conditional format: a cleared region font or size in a derived style is
  not written to DOCX, so Word shows the parent's value.
- No per-object "lock aspect ratio" flag in the model: pictures always keep proportions on corner drags, shapes never;
  DOCX/PPTX/XLSX `a:picLocks/@noChangeAspect` is always written as true and not read; no Size dialog checkbox.
- Compression offers a maximum edge rather than a resolution relative to the displayed size, and applies to one picture
  only (no "all pictures in the document").
- The rounded rectangle's text area still uses the model's corner ratio rule rather than `roundRect`'s `adj`.
