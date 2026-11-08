# Declared losses block B10: dimension lines (ODF190) and 3D scenes (ODP102)

## Coverage log

| File | Lines read | Outcome |
|---|---|---|
| docs/declared-losses-analysis.md | B7/B8 rows | ODF190 2 rows (a corpus case), ODP102 4 rows (a corpus case) |
| Formats.Odf/OdfDimensionLines.cs | all | detector only, raises ODF190 in ODT, ODP, ODS |
| Formats.Odf/OdpReader.cs | 150-260 | ODP102 raised for any `dr3d:scene` |
| Formats.Odf/Shapes/DeckObjectCodecs.cs | 140-300 | `draw:measure` read as a plain line (shared by Folio and Deck) |
| Drawing/Shapes/ShapeDefinition.cs, ShapeGeometry.cs | all / 1-100 | line outline (0,0)-(1,1) with flips |
| UI.Controls/Shapes/ShapeRenderer.cs | 1-320 | shared renderer of the three modules; arrows only on single-figure outlines |
| Formats.OpenXml/ShapeStyles/DrawingPresetGeometry.cs | all | single geometry writer of DOCX, XLSX, PPTX |
| Formats.OpenXml OoxmlGeometry.cs, OoxmlObjectNameMapper.cs | all | custGeom writer; extLst pattern |
| Formats.OpenXml DocxDrawingCodec.cs, SheetShapes.cs, Shapes/DeckObjectCodecs.cs | shape readers/writers | three hooks needed |
| Formats.Odf Presentation/Reading/PageReaders.cs | 1-140 | typed object readers, ids remembered for animations |

## Spec check

- ODF 1.3 part 3 §10.3.12 `draw:measure`: `svg:x1..y2` are the measured points; the look comes from graphic properties
  §20.x `draw:line-distance`, `draw:guide-overhang`, `draw:guide-distance`, `draw:start-guide`, `draw:end-guide`,
  `draw:placing`, `draw:parallel`, `draw:measure-align`, `draw:measure-vertical-align`, `draw:unit`, `draw:show-unit`,
  `draw:decimal-places`. The text is a `text:p` that may hold `text:measure` fields (§7.3.x, kinds value/unit/gap).
- OOXML has no dimension object (ECMA-376 §20.1.9 preset list has none); a custom geometry (§20.1.9.8 `a:custGeom`)
  can draw the guides and the dimension line, and `a:spPr/a:extLst` (§20.1.2.2.35) carries our extension.
- ODF 1.3 §10.5 `dr3d:scene` (lights, cube, sphere, extrude, rotate children) has no PresentationML twin: DrawingML
  `a:scene3d`/`a:sp3d` (§20.1.5) only extrude 2D shapes.
| Formats.Odf Shapes/SheetPrimitiveShapeReader.cs, SheetShapeWriting.cs, SheetShapeReading.cs | all / 1-160 | ODS line reader and writer, style lookup |
| Formats.Odf Odt/OdtDrawings.cs, OdtBodyWriting.cs | 20-250 / 440-510 | ODT drawings go through the Deck object writer chain |
| Formats.Odf Presentation/Writing/ElementWriters.cs, OdpWriter.cs, OdtWriter.cs | writers | object writer chain |
| Formats.OpenXml Presentation/Reading/SlideParts.cs, OoxmlValues.cs, PptxDeckShapeMapper.cs | readers | `DeckShape` names route to the preset codec |
| UI.Controls Shapes/Painting/PaintVisitor.cs, Painters.cs, DrawingObjectRenderer.cs | all | shared painters |
| Drawing.Printing/ObjectPathBuilder.cs | 30-240 | exact vector print path works in twips |

## Design

- **Dimension lines** are a `ShapeObject` (kind `Line`) carrying `DimensionLine` (shared drawing model): measured points
  normalised in the frame, which covers the measured points, guides and dimension line; ODF lengths (line distance,
  overhang, guide distance, start/end guide), placing, unit, decimals, alignment, explicit label. `DimensionLineGeometry`
  lays them out; `ShapeGeometry` gives the dimension outline, `ShapeRenderer` paints guides, arrowheads on the dimension
  line only and the measured label rotated along the line, readable side up. Printing falls back to the raster path for
  them (`ObjectPathBuilder` works in twips, the layout in points).
- ODF: `OdfDimensionLines` reads `draw:measure` (style chain for the graphic properties; `text:measure` fields mean an
  automatic label, free text an explicit one) in Deck, Folio (shared codec) and Ledger (`SheetDimensionLineReader`), and
  writes `draw:measure` with the properties in its automatic graphic style and value/unit `text:measure` fields.
- OOXML (DOCX, XLSX, PPTX): `DrawingDimensionLines` writes a `a:custGeom` with the dimension line and guides as
  unfilled paths and the arrowheads as filled paths (fill = line colour, no line ends, so every consumer draws the same
  thing), the label as shape text, and the model in `a:spPr/a:extLst/a:ext` (`urn:pulseoffice:dimension-line`). The
  three shape readers restore the dimension line from it; the DOCX freeform codec leaves such shapes to the shape codec.
- **3D scenes**: `Scene3DObject` (shared drawing model, visitor `VisitScene`) keeps the `dr3d:scene` XML and the
  automatic graphic styles it uses. ODP and ODT read and write it natively (styles renamed into the session, position
  and size from the element, `draw:transform` dropped since rotation is written by the element writer); the markup is
  validated on write (root `dr3d:scene`, only dr3d/draw/svg descendants, 4 MB cap, DTD prohibited). PPTX writes a
  dashed placeholder rectangle named `DeckScene` with a "3D" caption and the scene in `urn:pulseoffice:scene3d`;
  PulseOffice reads it back. The shared `ScenePainter` draws a placeholder (soft panel, isometric cube, "3D").
  Animations keep their target because the scene is read through the ordinary object reader (ids remembered).
- Codes: ODF190 removed (all readers, five languages, declared-features). ODP102 narrowed and moved to the PPTX writer
  ("other programs show a placeholder"). DOCX002 now names 3D scenes (Folio cannot write them to DOCX).

## Outcome

Tests: Drawing 313, Formats.Odf 774, Formats.OpenXml 2256, Presentations 318, Architecture 40, all green. Corpus: LOSS 0,
DECL 131, no ODF190/ODP102 rows. Live checks left: dimension label placement and arrowheads in the three modules, PPTX
placeholder and dimension custom geometry in PowerPoint/Word/Excel.

## Follow-up (2026-11-05): inherited scene styles, rotated labels, guide lengths

- **3D scene styles**: `DeckSceneCodec` (Formats.Odf) now flattens each graphic style a `dr3d:scene` uses on read: the
  named parent chain (styles.xml, resolved by `OdfGraphicStyleInheritance`) is merged into a copy of the automatic style,
  which keeps its name and loses `style:parent-style-name`. Writing still drops the parent, but nothing inherited is lost;
  a scene element pointing straight at a named style is preserved the same way. Test
  `La_proprieta_ereditata_da_uno_stile_con_nome_resta_nella_scena` (ODP→ODP and ODP→PPTX→ODP).
- **Dimension label**: the label centre (`IDimensionLineGeometry.LabelCentre`) and the readable angle (`ReadableAngle`)
  moved into the geometry; `ShapeRenderer` maps the centre and the dimension line through the shape transform (rotation
  and flips) and draws the label at the on-page line angle, so a rotated dimension line carries its label with it.
- **Guides**: the guide end is now `line distance + overhang` whatever the sign of the line distance, matching the
  reference geometry; guide starts were already `guide distance - start/end guide`. Geometry tests added.
- Tests: Drawing 315, Formats.Odf 776, Formats.OpenXml dimension/scene suite green. Corpus `one` on
  three corpus cases, a corpus case: LOSS 0.
