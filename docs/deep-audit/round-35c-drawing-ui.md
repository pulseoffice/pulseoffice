# Deep audit round 35c: dimension lines and 3D scenes in use

## Coverage log

| File | Lines read | Outcome |
|---|---|---|
| docs/deep-audit/losses-b10.md | all | design of block B10 |
| Drawing/Scenes/Scene3DObject.cs | all | ok |
| Drawing/DrawingObjectVisitor.cs | all | ok, scenes fall to `Otherwise` in visitors that do not know them |
| Drawing/Shapes/DimensionLine.cs | all | ok |
| Drawing/Shapes/DimensionLineGeometry.cs | all | ok; layout in points, frame-relative measured points |
| Exchange/ExchangeSerializer.cs, ExchangeContentGuard.cs | all | dimension lines and scenes cross the native form (types discovered by reflection) |
| Drawing/Shapes/ShapePresets.cs | 1-120 | no dimension preset: dimension is a property of a line, not a kind |
| Presentations.UI/DeckInsertCommands.cs | 1-120, 320-385 | no way to insert a dimension line |
| Sheets.UI/LedgerShapeCommands.cs | 25-110, 385-440 | same |
| Documents.UI/Commands/FolioObjectInsertCommands.cs | 25-80 | same |

## Findings

- **Medium (missing feature)**: no module could insert a dimension line; the reference suites have one. Added the shared
  `IDimensionLineTemplate` (`Drawing/Shapes/DimensionLineTemplate.cs`, registered in `DrawingComposition`): a 6 cm
  horizontal dimension line in centimetres with arrowheads, frame exactly covering guides and line. Wired as
  `deck.insert.shape.dimensionLine`, `ledger.shape.dimensionLine` (menu insert/shape) and `folio.insert.dimensionLine`
  (advanced surface, next to the line), enabled with the same context rules as the plain line, icons and five languages.
- Exchange: test added that a dimension line (all fields) and a 3D scene (markup and styles) survive the native JSON form.
- **Minor (left)**: the dimension offsets are absolute lengths while the measured points are frame-relative, so a
  vertical resize of a horizontal dimension line leaves empty room or draws the guides past the frame; nothing is lost.

## Tests

Drawing 316 (new: template layout inside its frame, arrows, label), Exchange 25 (new: dimension and scene round trip).
