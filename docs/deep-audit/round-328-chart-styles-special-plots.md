# Round 328: Ledger chart styles and special plots

Area: chart styles and colour palettes against Excel's style gallery, and stock, statistical, hierarchy and meter plots
with their XLSX and ODS mappings (queue item 317, 319, 324). No builds or tests were run in this round (audit cycle
rule); the coordinator runs them at the join.

## Defects found

Serious
1. **Excel's new chart types were dropped silently on open.** Treemap, sunburst, histogram, Pareto, box and whisker,
   waterfall, funnel and map charts live in `cx:` chartex parts referenced from an `mc:AlternateContent` anchor in the
   drawing. The XLSX chart reader only looked at direct `xdr:graphicFrame` anchors with `c:chart`, so these charts
   (and their fallback shape) vanished. Fixed: `ChartExReader` reads the chartex part (data blocks, `cx:f` formulas,
   cached levels, title, legend, series layout id) and `XlsxChartMapper` turns it into a chart with the right type
   and ranges (compact range when it fits, per-series sources otherwise, inline data when there are no formulas).
   Layout ids: treemap, sunburst, boxWhisker, waterfall, funnel map to the same types; clusteredColumn to Histogram,
   with a paretoLine sibling to Pareto; regionMap to a column chart (no map plot in the model).

Medium
2. **Theme colours in chart series and points were ignored.** Excel 2013 and later write series and point fills as
   `a:schemeClr` (accent1..6, often with `lumMod`/`lumOff`); only `a:srgbClr` was read, so every Excel chart opened in
   our own palette. Fixed: `ChartFillColour` resolves `srgbClr`, `schemeClr` (with tx/bg aliases), `sysClr` and
   `scrgbClr`, applying `lumMod`, `lumOff`, `shade` and `tint`; the scheme comes from the chart's theme override, else
   the nearest theme part up the package (workbook, document or presentation), else the default Office theme
   (`ChartThemeColours`). Gradient fills take their first stop.
3. **Series without a fill ignored the chart style.** Charts written by Excel 2007 or other producers rely on
   `c:style` (and `c14:style`, 100 + style, inside `mc:AlternateContent`) for automatic colours. Fixed:
   `ChartStyleColours` gives style columns 2 (colourful: accents with Office's variation sequence after six series),
   1 (greys) and 3..8 (shades of one accent).
4. **Pareto of categories drew a histogram of the values.** Excel's Pareto over text categories sorts the categories by
   value; `StatisticalPlot` binned the values instead. Fixed: categorical data draws one bar per category, sorted, with
   the cumulative line; numeric (ordinal) data still bins.

Minor / open (queued)
- XLSX still writes the special types as column charts with our type extension (XLSX087). Research: the native form is
  a chartex part (`cx:chartSpace`, relationship type chartEx, `mc:AlternateContent` with `Requires="cx1"` in the
  drawing, axes `cx:axis` with `cx:catScaling`/`cx:valScaling`, `cx:layoutPr` for binning, subtotals and statistics).
  Writing it was not attempted without a way to open the result in Excel in this round: a malformed chartex part makes
  Excel repair the whole workbook, which is worse than the declared loss.
- Chartex layout properties are not read: histogram binning (`cx:binning`), box plot quartile method, mean markers and
  outliers, waterfall subtotals (`cx:subtotals`), treemap parent label layout. The plots use their automatic defaults.
- Treemap and sunburst draw one level; the category levels (multi-column categories) are not grouped into parents.
- Box and whisker draws one box per series; Excel draws one per category per series when categories are given.
- Chart colour style parts (`cs:colorStyle`, `cs:chartStyle`) are not read or written; their effect survives because
  Excel also writes explicit fills, and we always write explicit `srgbClr` fills. The style id (`c:style`) is not kept
  in the model, so a round trip writes the resolved colours instead of the style.
- Meter plots (gauge, bullet) have no XLSX or ODS native form in either application; they remain column charts with
  our type extension (already declared XLSX087).
- The categorical Pareto has no automated rendering test (the plot is drawn on the headless session); covered only
  by reading.
