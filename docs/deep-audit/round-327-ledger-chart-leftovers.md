# Round 327: Ledger chart leftovers

Area: the chart items queued by rounds 323 and 324. No builds or tests were run in the round (audit cycle rule); the
coordinator runs them at the join.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Medium | Inserting or deleting columns inside a compact range with series in columns (rows with series in rows) added or removed a series, but series names, colours, formats and named line series kept their index, so they moved to another series. | `ChartSeriesShift`, run by the structure rewrite after `ChartPointShift`, moves `SeriesNames`, `SeriesColors`, `SeriesFormats` and `LineSeriesIndices` with their columns, leaves the inserted series automatic and drops the deleted ones. The shared position arithmetic is `ChartStructureOffsets`. |
| Medium | Pie of pie and bar of pie always put the last third of the points in the second plot; Excel's split type, split value and second plot size were dropped on read and never written. | `ChartPieSplit` (automatic, position, value, percent, custom points; second plot size 5–200 %) on `ChartDefinition`; `PieSplitter` chooses the second plot points; XLSX `c:splitType`, `c:splitPos`, `c:custSplit`/`c:secondPiePt`, `c:secondPieSize` (`OoxmlPieSplits`, also PPTX and DOCX charts); ODS `pulseoffice:pie-split`, `pie-split-threshold`, `pie-split-points`, `second-plot-size` on `chart:chart` (`OdfPieSplits`; ODF has no pie of pie class, so the type itself is already our extension); chart editor "Split series" section on the series page (`ChartPieSplitEditor`, five languages); `PiePlot` sizes the second pie or bar from the size. |
| Medium | XLSX open-high-low-close charts were written as four line series with no markers and no up-down bars: other applications showed only high-low lines, with open and close invisible. | The open and close series carry dash markers (the usual way to draw ticks in a stock chart); the reader treats dash markers in a stock chart without up-down bars as automatic, so the series formats stay plain. An Excel OHLC chart (with up-down bars) still reads back as Candlestick, which is what Excel draws for it. |
| Minor | Best fit data labels drew at the automatic position. | `ChartLabelLayout.BestFit` tries the automatic place and then the other placements (outside end, inside end, centre, inside base for bars and slices; above, right, below, left otherwise) and keeps the first that does not overlap a label already drawn. |
| Minor | The line series of a column and line combination drew its markers in the series colour, not the point colours. | `BarPlot.DrawCombination` uses `PointBrush` per point. |
| Minor | ODF legend positions `top-start`, `top-end`, `bottom-start`, `bottom-end` read back as Right. | `OdfChartVocabulary.LegendOf` maps them to Top and Bottom. |

## Tests

`Round327ChartSeriesShiftTests` (Sheets.Engine), `Round327PieSplitTests` (Charts: splitter and settings mapper),
`Round327PieSplitXlsxTests` and `Round327StockChartTests` (Formats.OpenXml), `Round327PieSplitOdsTests` and
`Round327OdfChartLegendTests` (Formats.Odf), `Round327ComboMarkerTests` and `Round327BestFitLabelTests` (UI.Controls).

## Research

ECMA-376 `CT_OfPieChart` orders `gapWidth`, `splitType`, `splitPos`, `custSplit`, `secondPieSize`, `serLines`; the
default split is automatic, which is not written. The ODF chart schema has no pie of pie class and no split
attributes; the reference suite's own pie of pie support uses a vendor extension that this project does not mirror, so
our own `urn:pulseoffice:chart` attributes carry the split. Excel's stock chart gallery has no tick-style OHLC: its
"open-high-low-close" chart draws up-down bars, so a file with up-down bars is a candlestick in our model.

## Open (queued)

- Pie split, OHLC tick markers and ODS pie split attributes not opened in Excel or the reference suite.
- Best fit is a greedy pass in drawing order; labels can still overlap the plot marks (bars, slices) or each other when
  every candidate is taken.
- Chart styles and colour palettes against Excel's style gallery, and stock, statistical, hierarchy and meter plots
  beyond label and axis consistency, remain unaudited (from 317, 319, 324; not in this round's area).
