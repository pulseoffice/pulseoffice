# Round 184: the minor chart rendering gaps left by round 181

Four rendering gaps declared open by round 181 are closed in the shared chart renderer
(`src/PulseOffice.UI.Controls/Charts`), so they apply to Ledger, Deck and Folio alike.

## Category labels follow the category axis

`ChartAxisMarks.CategoryAxisLine` (pure, in `PulseOffice.Charts`) gives the position of the category axis line: the
plot edge when the crossing is automatic, otherwise the position of the value where the category axis crosses.
`ChartRenderer` passes it to `ChartAxisPainter.Categories`, which draws the labels below that line on vertical charts and
to the left of it on horizontal ones.

## Stacked lines and areas on the secondary axis

`LinePlot` now splits stacked and percent-stacked line and area charts as `BarPlot` splits stacked bars: primary and
secondary series form two separate stacks, each accumulated (and, for percent charts, normalised to 100) on its own,
and each drawn on its own scale.

## Trendlines and error bars on horizontal bars

`ChartAnalysisPainter` takes the orientation. The category coordinate of each anchor is its Y on horizontal charts and
its X otherwise; trendline points, value error bars and category error bars are built by the public
`TrendPoints`, `ValueErrors` and `CategoryErrors` and transposed for horizontal charts; end caps are drawn
perpendicular to each whisker. The renderer no longer skips the analysis on horizontal bar charts.

## Reversed category axis

`IChartPlot.DrawsReversedCategories` (true for `BarPlot` and `LinePlot`) says a plot honours `CategoryAxis.Reversed`.
`ChartPlotBase.Categories` now takes the frame and flips the `CategoryScale` when the flag is set; the renderer builds
the label scale and the value axis crossing the same way, so labels, bars, lines, areas and the combination chart
follow the reversed order, vertical and horizontal.

## Tests

`tests/PulseOffice.UI.Controls.Tests/Round184CategoryLabelLineTests.cs`, `Round184StackedLineSecondaryTests.cs`,
`Round184HorizontalAnalysisTests.cs`, `Round184ReversedCategoryTests.cs`: geometry checks on a 300 by 200 plot with
two or three points, no drawing context.

## Open

- Not built or tested by this round's agent; the coordinator builds and runs the tests at the join.
- Statistical, financial, radar and other plots do not honour the reversed category flag (they keep their own order).
- No test yet for the DOCX/PPTX/ODT/ODP chart loss declarations (from round 172).
