# Round 317 (2026-11-20): Ledger chart creation and chart types

## Found and fixed
- **Medium: a chart made from numbers only lost its first column.** Every inserted chart assumed the first column holds
  categories, so selecting two numeric columns gave one series instead of two. `IChartSelectionShape`
  (`ChartSelectionShape`, Sheets.Engine) now detects the layout as Excel does: a header row when the first row's data
  cells are text, a category column when the first column's data cells are text, both when the top-left cell is empty
  (numeric year headers), series in columns when there are at least as many data rows as data columns, otherwise in rows.
- **Medium: a single selected cell made a one-cell chart.** The selection now grows to its current region, and the chart
  is anchored beside that region.
- **Medium: the legend followed the column count.** A pie or doughnut from a two-column range had no legend, and a
  two-series chart without a category column had none either. The legend now follows the series count and is always
  shown for pie and doughnut.
- **Medium (regression guard): scatter and bubble** keep a numeric first column as X values (`ChartSelectionLayout.For`),
  also when the type is chosen in the chart gallery after the draft.

Tests: `Round317ChartSelectionShapeTests` (Sheets.Engine.Tests, five tiny in-memory sheets).

## Open (queued)
- Per-point formats (one slice's colour, one slice pulled out: `c:dPt` with `c:spPr`/`c:explosion` in XLSX, per-point
  `chart:data-point` styles in ODS) are not in the model, so they are lost silently on read in both formats. Needs a
  per-point format list on the series model and both mappers.
- Not covered by this round: rendering of every type against Excel, stock, radar, combo with secondary axis, chart styles
  and colours, moving and resizing, and the per-type XLSX and ODS round trips beyond the pie, doughnut and scatter
  families read here.
