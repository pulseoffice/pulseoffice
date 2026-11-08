# Deep audit round 37b: dimension lines and 3D scenes in use, executable checks

## Coverage log

| File | Lines read | Outcome |
|---|---|---|
| docs/deep-audit/round-37-drawing-use.md | all | the five checks come from its gaps |
| tests/PulseOffice.Formats.Odf.Tests/OdtDimensionLineTests.cs | all | base for check 1 |
| tests/PulseOffice.Formats.OpenXml.Tests/DimensionLineAndSceneTests.cs | all | base for check 1 |
| src/PulseOffice.Drawing/Shapes/DimensionLineGeometry.cs | all | check 1 relies on Fitted/Frame: ok |
| src/PulseOffice.Formats.Odf/OdfDimensionLines.cs | 1-140 | check 1: fitted offsets written, frame rebuilt on read: ok |
| src/PulseOffice.Drawing.Printing/ObjectPathBuilder.cs | all | IsExact excludes dimension lines and arrows, but Folio ignored it |
| src/PulseOffice.Documents.UI/Rendering/PageSurfaces.cs | 115-215 | defect: CanvasSurface printed dimension lines (and arrowed lines) as bare outlines (fixed) |
| src/PulseOffice.UI.Controls/Shapes/ShapeRenderer.cs | 140-200 | screen/raster painting draws label and heads: ok |
| src/PulseOffice.Presentations.UI/Rendering/SlidePagePainter.cs | 55-90 | Deck consults IsExact: ok |
| src/PulseOffice.Sheets.Printing/SheetPrintDocument.cs | 300-335 | check 5 |
| src/PulseOffice.Formats.Odf/Presentation/OdpElementRotation.cs | all | skips draw:measure (no svg:x): rotation of a dimension line was dropped (fixed in OdfDimensionLines) |
| src/PulseOffice.Formats.Odf/Shapes/DeckObjectCodecs.cs | 30-70, 150-175 | caller of the placed dimension |
| src/PulseOffice.Formats.Odf/Presentation/Writing/ElementWriters.cs | 150-200 | caller of the placed dimension |
| src/PulseOffice.Formats.OpenXml/ShapeStyles/DrawingDimensionLines.cs | all | defect: Restore cleared the flips (fixed) |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxDrawingCodec.cs | 29-62, 195-300, 690-710 | defect: dimension reader ignored xfrm flips (fixed) |
| src/PulseOffice.Formats.Odf/Odt/OdtDrawings.cs | 20-75 | defect: item mirror applied after the baked rotation (fixed by folding) |
| src/PulseOffice.Formats.Odf/Odt/OdtContours.cs | 1-30 | ok |
| src/PulseOffice.Sheets.Printing/SheetObjectImages.cs | all | restructured: images carry their own area |
| src/PulseOffice.Sheets.UI/SheetChartImages.cs (now SheetObjectRasterizer.cs) | all | defect: Ledger print rasterized only charts (fixed) |
| src/PulseOffice.Sheets.UI/SheetPrintDocumentBuilder.cs | all | wiring |
| src/PulseOffice.Presentations.UI/DeckActions.cs, DeckElementActions.cs, DeckSlideActions.cs | object and slide actions | ok |
| src/PulseOffice.Presentations/SlideFactory.cs | Clone | animations retargeted, ids unique: ok |
| src/PulseOffice.Presentations.UI/SlideImageRenderer.cs, DeckVideoFrameSource.cs, DeckPrintable.cs, DeckPublisher.cs | painting paths | ok |
| src/PulseOffice.Presentations.UI/SlideView.cs | selection, ElementAt | ok |
| src/PulseOffice.Sheets.UI/LedgerViewParts/LedgerObjectEditing.cs | 1-60, 190-260 | ok |

## Checks

1. **Resized dimension line round trip** (ODT, ODS, ODP, DOCX, XLSX, PPTX; three heights): passed. Extended to rotated and
   flipped dimension lines, which exposed defects 1-4.
2. **Deck, dimension line and 3D scene**: select (SlideView.ElementAt), move, resize, delete, undo/redo of each step: passed.
3. **Deck, animated 3D scene**: duplicate slide keeps scene and animation (retargeted, unique ids), PNG, PDF and video
   frame paths paint the scene: passed.
4. **Folio, 3D scene and dimension line**: move, delete, undo/redo passed; print/PDF: defect 5.
5. **Ledger, dimension line**: move, resize, rotate, delete, undo/redo passed; print/PDF: defect 6.

## Defects (all fixed, tests failed before the fix)

1. **Grave**, ODF writers (ODT, ODS, ODP): a rotated or flipped dimension line lost rotation and flips silently
   (draw:measure has no transform; OdpElementRotation skips it). `OdfDimensionLines.Placed` now bakes fitting, rotation and
   flips into the measured points (placing toggled on a mirror) and both style and element use it.
2. **Medium**, OOXML: `DrawingDimensionLines.Restore` cleared the flips read from xfrm.
3. **Medium**, DOCX: the dimension reader did not read xfrm flips.
4. **Minor**, ODT: an item-level mirror on a dimension line was applied after the baked rotation; it is now folded
   into the shape (`OdtDrawingWriter.MirrorFolded`) and style:mirror is not written for it.
5. **Grave**, Folio print/PDF: `CanvasSurface` drew every shape as bare paths, ignoring `IObjectPathBuilder.IsExact`, so
   dimension lines lost label and arrowheads (and any arrowed or shadowed shape lost them too). Non-exact objects are
   now rasterized, as in Deck.
6. **Grave**, Ledger print/PDF: only charts were rasterized; pictures, 3D scenes, vector graphics and fontwork were not
   printed, dimension lines without label and heads. `SheetChartImages` became `SheetObjectRasterizer` (charts plus every
   non-exact drawing object via `IDrawingObjectRenderer`, rotated area), `ISheetObjectImages` returns the drawn area.

## Tests

Formats.OpenXml `DimensionLineResizeRoundTripTests` (21), Documents.UI `FolioSceneAndDimensionTests` (4),
Presentations.UI `DeckSceneAndDimensionTests` (4, headless added to the project), Sheets.UI `LedgerDimensionLineTests` (2,
headless added to the project).
