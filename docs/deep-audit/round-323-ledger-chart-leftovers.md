# Round 323: Ledger chart leftovers

Area: the chart items queued by rounds 318 and 319. No builds or tests were run in the round (audit cycle rule); the
coordinator runs them at the join.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Medium | An ODS column and line combination whose line series is not the last was read with the last series as the line. | `ChartDefinition.LineSeriesIndices` names the line series when they are not trailing; `IsLineSeries` honours it, so writers, rendering and the ODS reader agree. |
| Medium | Point formats kept their index when rows or columns were inserted or deleted inside the source, so a colour moved to another point. | `ChartPointShift`, called by the structure rewrite, shifts point indices along the points' axis for the compact range and for per-series sources, and drops the formats of deleted points. |
| Medium | Data label positions existed in the model, editor and both formats but rendering ignored them. | `ChartLabelLayout.Origin` takes the requested placement; bar and pie labels carry their base and tip, so inside end, inside base, outside end and centre are placed along the bar or slice radius; above, below, left and right are honoured for every plot. |
| Medium | Pie of pie and bar of pie drew as plain pies. | `PiePlot.Slices` lays out a main pie with an "other" slice facing a second pie or stacked bar holding the last third of the points, with connector lines; labels follow the same layout. |
| Minor | No best fit placement. | `DataLabelPlacement.BestFit`: XLSX `bestFit` (pie series), ODS `avoid-overlap`, editor choice in five languages. Drawn at the automatic position. |
| Minor | Line, area, radar and scatter markers drew in the series colour even with point colours. | Markers use `PointBrush`; line markers are drawn once per data point (stepped lines no longer draw markers on step corners). |
| Minor | XLSX stock series lost their colour (the writer cleared the shape properties). | The colour moves to the marker's shape properties, which the reader already falls back to. |
| Minor | Resizing an object past the left or top edge moved the opposite edge. | `SheetObjectEdges.Clamp` stops the dragged edge when resizing and keeps the size when moving. |
| Minor | The chart editor disabled the axis page for radar charts although rendering honours value bounds. | `IChartPlot.ScalesValues` (true for radar) enables the page. |
| Minor | Horizontal stacked bar legends were not reversed. | `BarPlot.Legend` reverses stacked and 100% stacked bar legends placed left or right, as Excel does. |

## Tests

`Round323ChartPointShiftTests` (Sheets.Engine), `Round323ChartLeftoverTests` (Formats.OpenXml: combo line order, best
fit in both formats, stock colour), `Round323ChartRenderingTests` (UI.Controls: label placements, pie of pie, legend
order, radar scaling), `Round323ObjectEdgeTests` (Sheets.UI).

## Research

Pie of pie split: Excel stores `c:splitType`, `c:splitPos` and `c:secondPieSize`; ODF has no pie of pie chart class,
so the type is already our extension there. The split is not modelled yet (queued); the drawing uses the last third of
the points. Best fit maps to the ODF `chart:label-placement` value `avoid-overlap`.

## Open (queued)

- Column insertion inside a compact range with series in columns shifts series, but series names, colours and formats
  keep their index.
- Pie of pie split settings not in the model.
- Best fit drawn at the automatic position; not opened in another application.
- Combination line series markers ignore point colours.
