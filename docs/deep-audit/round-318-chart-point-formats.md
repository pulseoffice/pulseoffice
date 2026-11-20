# Round 318: chart point formats and per-type round trips

Area: Ledger charts, data loss first; per-point formats from the queue (317), then the chart types round 317 did not
audit (stock, radar, combo with secondary axis, area, bubble). No build or test was run in this round (audit-cycle
rule); the tests run at the join.

## Defects

| # | Severity | Defect | Status |
|---|---|---|---|
| 1 | Serious | Per-point formats (one slice's colour or explosion, one bar's colour) were not in the model: XLSX `c:dPt` and ODS `chart:data-point` styles were dropped on read without a warning | Fixed |
| 2 | Medium | XLSX: line radar series colours were written on the outline but read only from the fill, so they were lost | Fixed |
| 3 | Medium | XLSX: marker-only scatter series keep their colour in the marker, which the reader ignored, so they were lost | Fixed |
| 4 | Medium | XLSX: a combo with a secondary-axis group, from an application that gives the line series a lower `c:idx`, was reordered by index, so a column series was drawn as the line | Fixed |
| 5 | Medium | XLSX: one `c:dPt` on any series made the reader drop the explicit colours of every series | Fixed (now dropped only for pie series, whose points carry the colours) |
| 6 | Minor | ODS: a combo whose line series is not the last one (another application's file) is read with the last series as the line, because the model keeps line series last | Open |
| 7 | Minor | Point formats keep their index when rows or columns are inserted or deleted in the chart source; they do not follow the moved point | Open |
| 8 | Minor | Point colours are drawn for pie, doughnut and column/bar (clustered, stacked, combo); line, area, radar and scatter points are written and read back but not yet drawn in their own colour | Open |
| 9 | Minor | Stock series carry no colour in XLSX (the writer clears their shape properties), so an explicit stock series colour is lost there | Open |

## What changed

- Model: `ChartPointFormat` (index, colour, explosion percent) and `ChartPointFormats` (sorted by index, value equality,
  explosion capped at 400) in `ChartSeriesFormat.Points`; `ChartDefinition.PointFormat` and `WithPointFormat`.
- XLSX: `OoxmlPointFormats` writes and reads `c:dPt` (`c:explosion`, `c:spPr` fill, or the outline for line, scatter
  and radar series), merging into the default pie points; default palette point colours read back as automatic.
- ODS: `OdfChartPointFormats` writes `chart:data-point` elements (with `chart:repeated` for the gaps) whose styles
  carry `draw:fill-color` and `chart:pie-offset`, and reads them back, expanding `chart:repeated`. Both are native in
  both formats, so no loss is declared.
- Editor: the shared chart series-format editor gains a data-point number, a point colour (shared `ColorFieldControl`)
  and a point explosion; texts in the five languages.
- Rendering: pie and doughnut slices use the point colour and explosion; bars use the point colour.

## Per-type audit

Stock, radar, filled radar, bubble, area and combo types already read back as themselves in both formats
(`ChartFamilyTests`, `Round168ChartModelTests`); the defects found are the colour and ordering ones above. Not covered
here: rendering of every type against Excel, chart styles, moving and resizing charts (queued).

Tests: `tests/PulseOffice.Formats.OpenXml.Tests/Round318ChartPointFormatTests.cs`.
