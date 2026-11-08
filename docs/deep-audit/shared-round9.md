# Deep audit round 9: shared area

Area: `src/PulseOffice.UI.Controls`, `src/PulseOffice.Drawing`, `src/PulseOffice.Charts`, `src/PulseOffice.Exchange`,
`src/PulseOffice.UI.Embedding`. Earlier rounds: [ui-controls.md](ui-controls.md), [drawing.md](drawing.md).
Priority: what all three modules use (exchange, embedded content painters, charts, shared fields).

## Coverage

| File | Lines read/total | Outcome |
|---|---|---|
| Exchange/ExchangeSerializer.cs | 134/134 | ok |
| Exchange/ObjectExchange.cs | 150/150 | ok |
| Exchange/ExchangeContentGuard.cs | 45/45 | fixed: object sizes without an upper bound |
| Exchange/Clipboard.cs | 25/25 | ok |
| Exchange/ExchangeModel.cs | 154/154 | ok |
| Exchange/CellGridConversions.cs | 107/107 | fixed: cell colours written into HTML `style` after a length check only |
| Exchange/ExchangeServiceCollectionExtensions.cs | 17/17 | ok |
| UI.Embedding/ContentPainters.cs | 154/154 | ok (fixed light palettes are document paper, not chrome) |
| UI.Embedding/SheetObjectFactory.cs | 53/53 | ok (callers always pass the ODS representation) |
| Charts/PlotScale.cs | 103/103 | ok |
| Charts/ChartStatistics.cs | 223/223 | ok |
| Charts/ChartSettings.cs | 76/76 | ok |
| Charts/ChartDefinition.cs | 94/94 | ok |
| Charts/ChartRecommendations.cs | 93/93 | ok |
| UI.Controls/Charts/ChartRenderer.cs | 193/193 | ok |
| UI.Controls/Charts/Plots/StatisticalPlot.cs | 361/361 | fixed: empty cells became zeros |
| UI.Controls/Charts/ChartText.cs | 21/21 | ok |
| UI.Controls/Charts/Plots/PiePlot.cs | 100/100 | ok |
| UI.Controls/Charts/Plots/BarPlot.cs | 211/211 | ok (brushes per bar: minor, known) |
| UI.Controls/Fields/MeasureField.cs | 403/403 | ok |
| UI.Controls/Fields/MeasureEntry.cs | 100/120 | ok |
| UI.Controls/NumericBounds.cs, NumberEntry.cs | 80/80 | ok |
| UI.Controls/Fields/AngleDial.cs | 279/279 | fixed: NaN accepted; unbounded range made the tick loop run for ever |
| UI.Controls/Fills/FillEditor.cs | 322/322 | ok (image «Choose» is an async lambda; faults reach `InterfaceFaultGuard`) |
| UI.Controls/Objects/RasterImageFiles.cs | 179/179 | ok |
| UI.Controls/Shapes/ShapeRenderer.cs | 360/360 | fixed: invalid font sizes threw while drawing |
| Drawing/Shapes/ShapeStyle.cs | 344/344 | ok (model is permissive; the renderers are the guard) |
| UI.Controls/Formulas/FormulaObjectRenderer.cs | 38/38 | fixed: glyph size without upper bound or NaN check |
| UI.Controls/Text/Rich/TextTypesetting.cs | 374/374 | fixed: paragraph and run sizes reached `FormattedText` unchecked |
| UI.Controls/Shapes/Painting/Painters.cs | 251/251 | fixed: every vector graphic re-parsed its SVG at every repaint |
| UI.Controls/Shapes/FontworkRenderer.cs | 94/94 | fixed: glyph size proportional to the frame without upper bound |
| UI.Controls/Shapes/DrawingObjectRenderer.cs | 70/70 | ok |
| Drawing/Shapes/VectorOutline.cs | 45/255 | ok (data types; reader read in round 1) |
| UI.Controls/Charts/Plots/HierarchyPlot.cs | 147/147 | ok |
| UI.Controls/Charts/Plots/ChartPlot.cs | 80/112 | ok |

## Defects

### Medium
- Exchange/ExchangeContentGuard.cs:23 — a pasted package could carry an object size up to `int.MaxValue` twips; the
  receiving modules add positions and sizes in `int` (`DocumentRect.Right`) and overflow. Sizes are now scaled down
  proportionally to `MaximumExtent` (500 cm); an empty size still falls back to the natural size.
  Test `DeepAuditRoundNineExchangeTests.UnOggettoSmisuratoArrivaConDimensioniEntroIlTetto` (failed before).
- UI.Controls/Charts/Plots/StatisticalPlot.cs:23 — statistical charts did not keep gaps, so the renderer turned empty
  cells into zeros: a histogram, box plot, density or Q-Q plot of `10, 20, (empty), 30` counted a 0. Besides, the
  box, dot and default scales took `Min()`/`Max()` over raw values, so a NaN collapsed the scale to 0..1. The plot now
  keeps gaps and drops non-finite values; in a waterfall an empty step contributes 0. Tests
  `DeepAuditRoundNineTests` (three, all failed before).

### Serious
- UI.Controls/Shapes/ShapeRenderer.cs:282 — the shared shape renderer (Folio, Deck, Ledger, thumbnails, printing)
  passed `ShapeTextStyle.Size` and `ShapeTextRun.Size` straight to `FormattedText`: 0, a negative size, NaN, infinity or
  anything above ~35,791 pt throws `ArgumentOutOfRangeException` inside `Render`, which takes down the drawing of the
  whole canvas. The model accepts any value (file readers, macros, agent). Sizes are now clamped to 1–1638 pt and a
  non-finite or non-positive size falls back to the default (runs keep the paragraph size). Test
  `UnaFormaConUnCorpoDelTestoNonValidoSiDisegnaSenzaEccezioni` (5 of 6 cases failed before).
- UI.Controls/Formulas/FormulaObjectRenderer.cs:28 — the glyph size of an embedded formula is the frame height / 1.1;
  a frame taller than ~39,000 px (large object at high zoom, or NaN bounds) threw from `FormattedText`. Now clamped to
  8–1638 and NaN falls back to 8. Test `UnaFormulaMoltoAltaSiDisegnaSenzaEccezioni` (both cases failed before).

- UI.Controls/Text/Rich/TextTypesetting.cs:38,61 — the rich text typesetter shared by Deck text frames, Folio text
  boxes and the frame-fitting measure used `Math.Max(4, size × 20 × scale)`: NaN passes `Math.Max` and a paragraph or
  run of more than ~1,800 pt at 100 % (less at higher zoom) exceeds Avalonia's em limit; both throw while drawing or
  measuring. The new `ParagraphTypesetter.Em` clamps to 4–30,000 px and maps NaN to the 18 pt default. Test
  `UnTestoRiccoConCorpoNonValidoSiMisuraSenzaEccezioni` (all 4 cases failed before).
- UI.Controls/Shapes/FontworkRenderer.cs:27 — the em size of Fontwork is 0.7 × the frame height, so a large text effect
  at high zoom (frame above ~51,000 px) threw from `FormattedText`; NaN bounds slipped through the `<= 0` test. Frames
  larger than 16,000 px are now drawn scaled down and then enlarged by a transform. Test
  `UnTestoArtisticoMoltoGrandeSiDisegnaSenzaEccezioni` (both cases failed before).

### Medium (continued)
- UI.Controls/Shapes/Painting/Painters.cs:39 — `VectorPainter` parsed the SVG of each vector graphic (XML, paths, arc
  flattening) at every repaint: a slide or page with many icons re-parsed all of them on every scroll, drag or
  animation frame. The parsed drawing is now cached per SVG string in a `ConditionalWeakTable` (released with the
  asset). Test `UnaGraficaVettorialeNonVieneRiletteAOgniDisegno` (5 reads for 5 frames before, 1 now).
- UI.Controls/Fields/AngleDial.cs:69 — the shared dial accepted NaN as initial value, through `Value` and through
  `Step`, and raised `ValueChanged(NaN)`; with unbounded limits (the default of `MeasureOptions`) the tick loop in
  `Render` would iterate about 10^14 times. Today `FormPromptView` and `CellAlignmentPage` pass bounded ranges, so the
  hang is latent. Limits are now clamped to ±360 and non-finite values ignored. Tests
  `IlQuadranteDegliAngoliNonAccettaUnValoreNonNumerico`, `IlQuadranteDegliAngoliRestaEntroUnGiroAncheConLimitiSmisurati`
  (both failed before).

### Minor
- Exchange/CellGridConversions.cs:98 — `ColorHex`/`BackgroundHex` went into the HTML `style` attribute after a length
  check only (`"><b>xy` passed). Content arriving from outside is cleaned by the guard, but a participant's capture is
  not; the conversion now admits only `#RRGGBB`. Test `UnColoreDiCellaConCaratteriNonEsadecimaliNonEntraNellHtml`.
- UI.Controls/Charts/Plots/StatisticalPlot.cs:336 — the waterfall parsed two colours and allocated a brush per bar;
  the brushes are now built once per drawing.

## Remaining (not fixed)
- Minor, UI.Controls/Shapes/ShapeRenderer.cs:146-215: glow, shadow and soft-edge blur radii are not capped and glow
  opacity is not clamped (shadow opacity is). The headless renderer does not apply effects, so no failing test could
  be written; to be watched live with a shape whose glow radius comes from a damaged file.
- Minor, UI.Controls/Shapes/FontworkRenderer.cs: three `FormattedText` per character at every repaint (natural width,
  fitted width, glyphs) plus `BuildGeometry`; acceptable for short texts, a cache would help long ones.
- Minor, UI.Controls/Fields/AngleDial.cs:208 and Charts plots: pens and brushes allocated at every repaint (known
  pattern, also listed in earlier rounds).
- Minor, UI.Controls/Fills/FillEditor.cs:209: the image «Choose» button is an async lambda; a fault from the system
  picker reaches `InterfaceFaultGuard` (logged, app kept alive) without a status message.
- Minor, Exchange/CellGridConversions.cs:85: `Table` marks a column as numeric but stores the cell text, not the number.

## Closing of round 9
- Coverage: Exchange (7 files) and UI.Embedding (2 files) re-read in full; Charts core (5 files) and the shared chart
  renderer plus four plots; shared fields (measure, angle, numeric bounds); fill editor and raster files; the shape,
  vector, Fontwork, formula and rich-text renderers that all three modules draw through.
- Defects: 4 serious, 4 medium, 2 minor fixed; 5 minor remaining.
- Tests run (one project at a time): Exchange.Tests 19/19, UI.Controls.Tests 329/329, Charts.Tests 44/44,
  Architecture.Tests 38/38, Presentations.UI.Tests 63/63; full solution build with `-m:2`, 0 warnings.

## Live tests
- Deck: a text box whose paragraph size is edited to an absurd value by a macro (e.g. 100000) must still draw.
- Folio/Deck: a Fontwork object enlarged to the full page at 400 % zoom.
- Any module: a page with 30 or more icons from the vector gallery; scrolling and dragging must stay fluid.
- Ledger: histogram, box plot and waterfall over a range with empty cells: empty cells must not count as zeros.
- Paste between modules of an object whose size has been forced huge (agent or macro): it arrives within 500 cm.
