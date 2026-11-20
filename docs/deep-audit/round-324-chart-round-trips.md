# Round 324: Ledger chart round trips and styles

Area: the queue item marked (317, 319): per-type XLSX/ODS round trips other than pie, doughnut and scatter, chart
styles and palettes against Excel's gallery, keyboard resizing of charts, and stock, statistical, hierarchy and meter
plots. Round 323 worked in parallel on rendering and the data label model; this round did not touch the label model.

## Covered

- XLSX chart families (`ChartFamilies.cs`): bar/column (clustered, stacked, percent, 3D), line (plain, smooth,
  stacked, percent), area (plain, stacked, percent, 3D), radar (marker, filled), bubble, stock (HLC, OHLC,
  candlestick), column-line, surface (read only). ODS vocabulary (`OdfChartVocabulary.cs`) and the reader's type
  refinement and combo detection.
- Chart editor axis page enablement per plot.

## Defects

- **Medium, fixed.** Stacked and percent-stacked bar and column charts were written to XLSX without `c:overlap
  val="100"` (and without `c:gapWidth`). Excel draws a stacked bar group without overlap 100 with every series shifted
  sideways instead of on one bar. `BarFamily.Close` now writes `c:gapWidth 150` and, for flat stacked groups,
  `c:overlap 100` (3D bars have no overlap element). PPTX charts share the writer. Test:
  `ChartFamilyTests.Le_barre_impilate_si_sovrappongono_in_XLSX_e_ogni_variante_si_rilegge`, plus a theory over every
  line and area variant.
- **Minor, fixed (queued 319).** The chart editor disabled the axis scale page for radar charts though the radar plot
  honours value bounds and the reversed category axis. Plots now declare `ScalesAxes` (default `DrawsAxes`, true for
  the radar plot) and the page follows it. Test: `Round324ChartAxisPageTests`.

## Not covered (queued)

- Chart styles and colour palettes against Excel's style gallery: not audited.
- Keyboard resizing of charts: Ledger, Deck and Folio share `ObjectNudgeKeys`, where Shift+arrow is a large nudge.
  Office resizes the selected object with Shift+arrow; changing the shared key map changes all three modules and was
  left for a round that can run the UI tests.
- Stock, statistical (box and whisker, histogram, Pareto), hierarchy (treemap, sunburst) and meter plots beyond label
  and axis consistency: not audited.
- ODF legend positions `top-start`, `top-end`, `bottom-start`, `bottom-end` read back as Right.
- XLSX OHLC stock charts are written without up-down bars, which Excel always draws for the OHLC subtype; an Excel
  OHLC chart reads back as Candlestick.
