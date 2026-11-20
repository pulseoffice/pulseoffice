# Round 330: XLSX chartex charts

## Found
- Serious: treemap, sunburst, histogram, Pareto, box plot, waterfall and funnel charts were written to XLSX as column
  charts (XLSX087), so Excel showed a different chart.
- Medium (not fixed, queued): chartex layout properties are not read into the model; treemap and sunburst draw one
  level; box and whisker draws one box per series; `c:style` and `cs:colorStyle` are not kept.
- Minor (not fixed, queued): no rendering test for the categorical Pareto plot.

## Fixed
- `ChartExSpaceWriter` builds a `cx:chartSpace` (data blocks with `strDim`/`numDim`, formulas and caches copied from
  the classic series, all category levels; series with `layoutId`, Pareto line on a percentage axis, default layout
  properties: treemap overlapping parent labels, box plot exclusive quartiles with mean marker and outliers, histogram
  automatic binning, Pareto aggregation; axes; title; legend). `ChartExAnchors` writes it as an `ExtendedChartPart`
  behind `mc:AlternateContent Requires="cx1"` (`cx2` for funnel), with the classic anchor (carrying our type
  extension) as the fallback. On read the classic fallback is preferred when it carries one of these types, so the
  full model survives; files from Excel (shape fallback) are read from the chartex part as before.
- XLSX087 is no longer raised for these seven types.

## Not covered
The layout properties model, multi-level treemap and sunburst rendering, per-category box plots, chart style id and
colour style parts, and the Pareto rendering test were not done in this round and are queued (330). The written
chartex part has not been opened in Excel.
