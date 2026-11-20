# Round 334 (2026-11-20): chartex layouts, hierarchies, box groups, chart style parts

Area: the chartex items round 330 did not reach.

## Defects found

| Severity | Defect | Fix |
|---|---|---|
| Serious | Chartex layout properties were never read, and the writer emitted fixed defaults: a file's histogram binning, box plot quartile method, mean and outlier visibility, waterfall subtotals and treemap parent labels were lost on every round trip. | New model `ChartStatisticalLayout` (`ChartDefinition.Statistical`). `ChartExLayoutProperties` reads and writes `cx:layoutPr` (`binning` with `binSize`/`binCount`, `underflow`, `overflow`, `intervalClosed`; `visibility`; `statistics/@quartileMethod`; `subtotals/idx`; `parentLabelLayout`). The classic chart (XLSX fallback, DOCX, PPTX) carries the same `layoutPr` inside our `c:extLst` extension; ODS carries `pulseoffice:` attributes on `chart:chart` (`bin-count`, `bin-width`, `underflow`, `overflow`, `interval-closed`, `quartile-method`, `mean-marker`, `mean-line`, `inner-points`, `outliers`, `subtotals`, `parent-labels`). |
| Medium | The renderer ignored those properties: bins were always square root of n, quartiles always inclusive, subtotal bars were drawn as steps, whiskers reached the outliers. | `IChartStatistics` gained `Histogram(values, layout)`, `Summary`/`Outliers` by quartile method (exclusive is the n+1 rule) and `Waterfall(values, subtotals)`; `StatisticalPlot` uses them, draws mean markers, a mean line, inner points, whiskers to the furthest non-outlier, and subtotal bars from zero in the series colour. |
| Medium | Treemap and sunburst drew one level: category levels were not grouped into parents. | `IChartHierarchy.Roots` builds the tree from `ChartData.CategoryLevels` (blank or repeated parent labels continue a group); the treemap nests children in their parent with an overlapping, banner or no label; the sunburst draws one ring per level. Chartex parent `cx:lvl` levels are read back as category levels. |
| Medium | Box and whisker drew one box per series; Excel draws one per category per series. | `IChartHierarchy.Buckets` gathers points by category label; boxes sit side by side per category with the category label under them. |
| Medium | The chart style id (`c:style`) and the `cs:chartStyle`/`cs:colorStyle` parts were dropped. | `ChartStyleReference` (`ChartDefinition.Style`): `c:style` read and written by the classic builder and reader; `ChartStyleParts` reads and writes both parts (validated root and namespace, 1 MB cap) for XLSX, DOCX and PPTX charts; ODS keeps them as `pulseoffice:chart-style-id`, `color-style` and `chart-style`. |
| Minor | The categorical Pareto plot had no rendering test. | `Round334ParetoRenderingTests` renders it and checks the bars run tallest first. |

## Tests

`Round334ChartLayoutTests` (statistics and hierarchy), `Round334ChartExLayoutTests` (XLSX with and without the classic
fallback, bin size in the chartex part, style id and colour style part, chartex parent levels),
`Round334StatisticalLayoutOdsTests`, `Round334ParetoRenderingTests`.

## Still open

- No editor for the statistical layout (bin width or count, overflow and underflow, quartile method, mean and outlier
  visibility, subtotals, parent labels): they only come from files.
- Waterfall subtotal indices do not follow row insertion or deletion in the source.
- Style parts of a chart read only from a chartex part (an Excel file without our classic fallback) are not kept.
- The chartex category dimension written from a literal (inline data) carries only the leaf level.
- Treemap label placement is simple (no wrapping); the overlapping parent label sits in the middle of the parent.
- The exclusive quartile clamps to the extreme values for very small samples, where Excel's QUARTILE.EXC errors; how
  Excel draws such boxes has not been checked.
