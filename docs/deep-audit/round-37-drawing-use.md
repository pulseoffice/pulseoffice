# Deep audit round 37: dimension lines and 3D scenes in use (checks left by round 35c)

## Coverage log

| File | Lines read | Outcome |
|---|---|---|
| docs/deep-audit/round-35c-drawing-ui.md | all | open points taken from here |
| Drawing/Shapes/DimensionLineGeometry.cs | all | defect: offsets ignore the frame (fixed) |
| Drawing/Shapes/DimensionLine.cs | all | ok |
| Drawing/Shapes/DimensionLineTemplate.cs | all | ok, frame equals the natural layout |
| Formats.Odf/OdfDimensionLines.cs | 20-120 | wrote raw offsets, the reader rebuilds the frame from them (fixed) |
| Formats.OpenXml/ShapeStyles/DrawingDimensionLines.cs | 20-75 | ok: frame from xfrm, fitting is idempotent |
| Formats.Odf/Shapes/SheetShapeWriting.cs | 45-90 | caller updated |
| Formats.Odf/Presentation/Writing/ElementWriters.cs | 155-170 | caller updated |
| Drawing/DrawingObjectVisitor.cs | all | ok |
| Sheets.UI/LedgerViewParts/LedgerObjectEditing.cs, GridParts/GridViewport.cs | rotation switches | scenes and other kinds without rotation stay unrotated: by design |
| Scripting, Agent.UI (ScriptApiCatalog users), Documents.UI/Scripting/FolioMacros.cs | search + read of object members | shapes are not listed in the script API (only form controls): nothing for a dimension line or scene to break |

## Findings

- **Minor, fixed**: on a vertical resize of a horizontal dimension line the offsets stayed absolute lengths, so the guides
  went past the frame or left empty room. `IDimensionLineGeometry.Fitted(line, width, height)` scales all offsets
  (line distance, overhang, guide distance, start and end guide) by the largest factor that keeps every point of the
  layout inside the frame; the natural frame gives factor 1 (unchanged line). `Layout(line, width, height)` uses it, so
  canvas, thumbnails, PDF and raster print (all through `ShapeRenderer`) and the OOXML geometry fallback agree.
- The ODF writer now writes the fitted offsets in the graphic style (`WriteStyle(style, line, size)`): the ODF reader
  rebuilds the frame from offsets and measured points, so before the fix a resized dimension line came back with its
  original height.

## Not verified here

Selection handles, move, undo and 3D scene commands (delete, duplicate slide, animations) were not exercised headless in
this round; they go through the generic object paths. To try live: resize a dimension line vertically in Folio, Ledger
and Deck, save to ODT/ODS/ODP and reopen; export to PDF.

## Tests

Drawing 320 (new: fitting on vertical resize at three heights, natural frame unchanged, oblique line stays inside).
Formats.Odf and Formats.OpenXml dimension tests green.
