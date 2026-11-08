# Deep audit: drawing, charts, rasterization, embedding, exchange

Area: `src/PulseOffice.Drawing` (8,488 lines), `Drawing.Printing`, `Charts`, `Formats.Rasterizing`, `UI.Embedding`, `Exchange`.

## Coverage

| File | Lines read/total | Outcome |
|---|---|---|
| Drawing/Assets/Gif/GifCodec.cs | 433/433 | per-frame cap (taken over from the previous agent); LZW code beyond `next` now closes the stream (fixed) |
| Drawing/Assets/ImageHeader.cs | 161/161 | PNG chunk length bounded (previous agent) |
| Drawing/Assets/Filters/PixelFilters.cs | 341/341 | 2 medium defects fixed |
| Drawing/Shapes/Geometry/EnhancedFormula.cs | 284/284 | stack protected (previous agent); division by zero already 0, NaN already 0 |
| Drawing/Shapes/Geometry/DrawingGuideEvaluation.cs | 130/130 | 2 medium defects fixed |
| Drawing/Shapes/SvgPathParser.cs | 217/217 | infinite numbers (`1e999`) became coordinates: fixed |
| Drawing/Shapes/VectorOutline.cs | 255/255 | same for attributes and `points`: fixed; note: `Transform` and the `defs` filter walk up the ancestors for every element (O(n·depth), acceptable for icons) |
| Drawing/Shapes/SvgTransform.cs | 75/75 | no defects |
| Drawing/Shapes/ArcFlattener.cs | 106/106 | NaN handled (saturating conversion), sweep limited to 2π |
| Drawing/Shapes/ObjectSnapping.cs | 109/109 | grid already bounded; NaN and zero step give an empty grid |
| Drawing/Shapes/ConnectorRouter.cs | 185/185 | no defects |
| Drawing/Shapes/Geometry/EnhancedPathInterpreter.cs | 276/276 | arcs with NaN do not loop (comparisons false); no defects |
| Drawing/Shapes/Geometry/GeometryPathBuilder.cs | 118/118 | sweep bounded and finite |
| Drawing/Shapes/Geometry/PresetGeometryEngine.cs | 119/119 | no defects |
| Drawing/Shapes/Geometry/AdjustHandles.cs | 349/349 | solver: NaN limits would give NaN, but guides and formulas are already cleaned upstream |
| Drawing/Freeform/PathEditing.cs | 273/273 | indices validated; no defects |
| Drawing/Assets/FolderAssetLibrary.cs | 212/212 | malformed manifest tolerated; no defects |
| Drawing/Assets/Qr/QrLayout.cs | 177/177 | no defects |
| Drawing/Assets/Qr/QrEncoder.cs | 132/132 | content too long rejected with a declared exception; no defects |
| Drawing/Assets/Qr/QrPenalty.cs | 85/85 | rule 4 conforms to the standard |
| Drawing/Assets/Qr/QrReedSolomon.cs | 62/62 | no defects |
| Drawing/Assets/Qr/QrVersionTable.cs, QrVersion.cs, QrBitBuffer.cs, QrMask.cs, QrErrorCorrection.cs | 48+47+22+20+9 | tables and alignment centres verified |
| Drawing/Freeform/PathAuthoring.cs | 157/157 | iterative Douglas-Peucker; NaN tolerance does not simplify but does not loop |
| Drawing/Freeform/PathNormalizer.cs | 131/131 | no defects |
| Drawing/Freeform/FreeformPath.cs | 126/126 | no defects |
| Drawing/Freeform/ShapeConversion.cs | 82/82 | steps per curve without a cap: fixed |
| Drawing/Fontwork/Fontwork.cs | 117/117 | NaN advances degrade to end of path, no loop |
| Drawing/Shapes/ArrowGeometry.cs | 113/113 | no defects |
| Drawing/Shapes/ShapeGeometry.cs | 281/281 | NaN arc angles and radii erased the shape: fixed |
| Drawing/Shapes/ShapeStyle.cs | 344/344 | dash with NaN distance: fixed |
| Drawing/Shapes/ObjectManipulator.cs | 167/167 | no defects |
| Drawing/Shapes/ShapeTextEditing.cs | 148/148 | ranges bounded; no defects |
| Drawing/Shapes/FillEditing.cs | 137/137 | NaN stop: fixed |
| Drawing/Shapes/ShapePresets.cs | 133/133 | no defects |
| Drawing/Shapes/Geometry/PresetFormulaTranslator.cs | 259/259 | `wdN` with N=0 translated to `logwidth/0`, which the ODF evaluator already turns into 0; no defects |
| Drawing/ConditionalIcons/IconGlyphs.cs | 178/178 | fills from a constant catalogue; no defects |
| Drawing/Shapes/Geometry/NativeShapeDefaults.cs | 113/113 | no defects |
| Drawing/Shapes/Geometry/PresetShapeCatalog.cs | 107/107 | embedded resource, lazy and thread-safe loading |
| Drawing/Shapes/ExtendedShapes.cs | 97/97 | no defects |
| Drawing/Shapes/Geometry/NativeShapeModifiers.cs | 93/93 | NaN modifiers arrive as NaN guides, cleaned downstream in `Define` |
| Drawing/Shapes/Geometry/PresetShapeDefinition.cs | 84/84 | model only |
| Drawing/Assets/VectorAsset.cs | 84/84 | model only; share-alike licences not preinstallable |
| Drawing/Shapes/ShapeSegmentVisitors.cs | 82/82 | `SegmentMapper.Map(outline)` lost the figure properties: fixed |
| Drawing/Shapes/PathSink.cs | 77/77 | no defects |
| Drawing/Shapes/OutlineBuilder.cs | 77/77 | empty `Polygon`/`Polyline` throw, but only with constant points from the code |
| Drawing/Shapes/ShapePath.cs | 78/78 | no defects |
| Drawing/Shapes/Geometry/ComputedGeometry.cs | 70/70 | model only |
| Drawing/ConditionalIcons/ConditionalIconCatalog.cs | 67/67 | non-thread-safe cache on a singleton: fixed |
| Drawing/Shapes/Connector.cs | 65/65 | model only |
| Drawing/Shapes/ShapeDefinition.cs | 59/59 | model only |
| Drawing/Assets/AssetCatalog.cs | 54/54 | OpenMoji (BY-SA) optional only, per decision 11 |
| Drawing/ConditionalIcons/IconModel.cs | 53/53 | no defects |
| Drawing/Assets/GraphicObjects.cs | 53/53 | model only |
| Drawing/Shapes/Geometry/ComputedGeometryEngine.cs | 48/48 | no defects |
| Drawing/Shapes/ShapePathWriter.cs | 47/47 | no defects |
| Drawing/DrawingObjectVisitor.cs | 47/47 | no defects |
| Drawing/Assets/Filters/ColorReplacement.cs | 46/46 | NaN tolerance replaces nothing; no defects |
| Drawing/Shapes/ShapeTextInk.cs | 45/45 | no defects |
| Drawing/Shapes/ShapeFamilies.cs | 45/45 | constant data |
| Drawing/Layout/BoxDistribution.cs | 40/40 | non-finite values already excluded |
| Drawing/Shapes/ConnectorFramer.cs | 37/37 | no defects |
| Drawing/Shapes/ObjectTransformSession.cs | 36/36 | no defects |
| Drawing/Shapes/Geometry/ImplicitPresetGeometry.cs | 33/33 | no defects |
| Drawing/Shapes/GraphicStyles.cs | 29/29 | no defects |
| Drawing/Shapes/ShapeGroup.cs | 27/27 | model only |
| Drawing/Text/ObjectFontNames.cs | 25/25 | fonts of text runs were not listed: fixed |
| Drawing/Shapes/ContentFlipping.cs | 23/23 | no defects |
| Drawing/Shapes/ShapeTextRun.cs | 18/18 | model only |
| Drawing/Assets/VectorRasterizer.cs | 11/11 | no defects |
| Drawing.Printing/ObjectPathBuilder.cs | 266/266 | connector head without `ArrowScale`: fixed; heads on shapes and connector dashes are not exact in print, but `IsExact` declares it and the caller falls back to raster |
| Charts/PlotScale.cs | 102/102 | `Divisions(most ≤ 0)` looped forever: fixed |
| Charts/ChartStatistics.cs | 221/221 | histogram and density on extreme values: fixed |
| Charts/ChartDefinition.cs | 94/94 | no defects |
| Charts/ChartRecommendations.cs | 93/93 | no defects |
| Charts/ChartSettings.cs | 76/76 | no defects |
| Charts/ChartFamilies.cs | 72/72 | completeness verified in the constructor |
| Charts/ChartCatalog.cs | 37/37 | enumeration only |
| Formats.Rasterizing/SkiaJpegEncoder.cs | 28/28 | `width*height*4` in `int` would overflow with RGB buffers beyond ~1.6 GB (not provable without allocating them): left, minor |
| Formats.Rasterizing/SkiaVectorRasterizer.cs | 41/41 | SVG cleaned before loading; null surface handled |
| UI.Embedding/SheetObjectFactory.cs | 53/53 | side bounded to 4096 px |
| UI.Embedding/ContentPainters.cs | 154/154 | palettes created once, not during drawing; no defects |
| Exchange/ExchangeModel.cs | 154/154 | no defects |
| Exchange/ObjectExchange.cs | 150/150 | no defects |
| Exchange/ExchangeSerializer.cs | 134/134 | 256 MB cap, depth 128, polymorphic types only derived from the declared abstract types |
| Exchange/CellGridConversions.cs | 107/107 | HTML encoded; no defects |
| Exchange/ExchangeContentGuard.cs | 40/40 | non-positive object sizes: fixed |
| Exchange/Clipboard.cs | 25/25 | no defects |
| Exchange/ExchangeServiceCollectionExtensions.cs | 17/17 | no defects |

Changes inherited from the interrupted agent, completed and verified: ChartPalette with negative indices, `ChartTypeInfo`
(dead code with Italian-only names) removed, caps on arcs, dashes, snap grid, NaN gradients,
denoise without allocations.

## Defects

| Severity | File | Defect | Status |
|---|---|---|---|
| medium | PixelFilters.cs `ColorAdjustmentFilter` | NaN/infinite brightness, contrast, gamma or channels made the image black | fixed |
| medium | PixelFilters.cs `NeighbourhoodFilter.Apply` | pixels shorter than width×height×4: IndexOutOfRange | fixed |
| medium | DrawingGuideEvaluation.cs `DrawingBuiltIns` | `wd0`/`hd0`/`ssd0` gave infinity | fixed |
| medium | DrawingGuideEvaluation.cs `Define` | an infinite or NaN guide propagated to the whole shape | fixed |
| medium | SvgPathParser.cs `Next`, VectorOutline.cs `Num`/`Points` | out-of-range SVG numbers (`1e999`) gave infinite coordinates | fixed |
| minor | GifCodec.cs `Decompress` | code beyond `next` accepted as KwKwK | fixed |
| medium | ShapeGeometry.cs `EllipseArcs.Normal` | `draw:start-angle="NaN"` (accepted by the ODF reader) gave an arc of NaN coordinates, invisible shape | fixed: non-finite angle = 0, full ellipse |
| minor | ShapeGeometry.cs `Outline(kind, …)` | `Math.Clamp(NaN)` stays NaN: NaN corner radius or inner ratio erased the shape | fixed: default values |
| minor | ShapeConversion.cs `ToPolygon` | `stepsPerCurve` without a cap (int.MaxValue exhausts memory) | fixed: cap 256 |
| minor | ShapeStyle.cs `ShapeDashPattern.InLineWidths` | `Math.Max(NaN, 0.01)` is NaN: non-finite distance or lengths gave a NaN dash to the painter | fixed |
| minor | FillEditing.cs `GradientStopEditing.Add`/`Move` | NaN position entered as a stop | fixed: ignored |
| medium | ShapeSegmentVisitors.cs:55 `SegmentMapper.Map(ShapeOutline)` | rebuilt the figures with `new ShapeFigure`, losing `Filled`, `Stroked` and `Shade`: in printing (`ObjectPathBuilder`), in Deck's ODF (`DeckObjectCodecs`), in connectors and icons, paths without fill (cylinder lid, open arcs) came back filled and shading disappeared | fixed: `figure with { Segments = … }` |
| medium | ConditionalIconCatalog.cs:16 | `Dictionary` cache written by several threads on the singleton shared by Ledger's grid and printing: «concurrent update … corrupted its state» | fixed: `ConcurrentDictionary.GetOrAdd` |
| medium | ObjectFontNames.cs:18 | counted only the font of the shape style, not those of the runs (`Runs[].Family`): fonts used in a run were missing from Deck's and Ledger's font lists | fixed |
| minor | ObjectPathBuilder.cs:100 | in print the connector head ignored `ArrowScale` (on screen it did not) | fixed |
| medium | ChartStatistics.cs `Histogram` | with extreme values (±1e308) `(max-min)` is infinite: bins with NaN edges | fixed: halves and interpolation of the edges |
| minor | ChartStatistics.cs `Density` | overflowed mean: NaN points; negative `samples` threw | fixed |
| minor | PlotScale.cs `Divisions` | `most ≤ 0` infinite loop | fixed |
| medium | ExchangeContentGuard.cs `VisitObjects` | a clipboard package with zero or negative sizes reached Folio as such (Ledger and Deck already fell back to the natural size) | fixed: the guard substitutes the natural size |
| minor | SkiaJpegEncoder.cs:14 | `new byte[width*height*4]` in `int` arithmetic | left (a 1.6 GB buffer would be needed to prove it) |

## Still to read

Nothing: `Drawing`, `Drawing.Printing`, `Charts`, `Formats.Rasterizing`, `UI.Embedding` and `Exchange` are read in full.

## Live tests

Colour adjustment of an image with extreme values; OOXML shapes with `wd0` guides.
ODF arcs with `draw:start-angle="NaN"`; a connector with an enlarged head printed to PDF; Ledger with conditional icons while printing; pasting into Folio an object copied from Deck.
