# Round 168: chart model extension (axes, series formats, analysis)

Answers the serious defect of round 166 (`round-166-ledger-charts.md`): the chart model had no axis scale, axis number
formats, secondary axis, trendlines, error bars, per-series line and marker formatting, and data labels were only on or
off, so XLSX and ODS files carrying them lost them silently on read and save.

## Model (`PulseOffice.Charts`, `ChartFormatting.cs`)

All immutable records with defaults that leave existing charts unchanged.

- `ChartAxisScale`: minimum, maximum, major and minor unit, logarithmic with base, reversed, crossing (automatic,
  minimum, maximum, value) and number format. `ChartDefinition.CategoryAxis`, `ValueAxis`, `SecondaryValueAxis`
  default to `ChartAxisScale.Automatic`. The crossing stored on an axis is where the *other* axis crosses it, which is
  how both formats store it (OOXML `c:crosses`/`c:crossesAt` on the crossing axis, ODF `chart:axis-position` on the
  crossing axis).
- `ChartSeriesFormat` per model series (`ChartDefinition.SeriesFormats`, aligned with the model series like
  `SeriesColors`; `SeriesFormat(i)`, `WithSeriesFormat`): line width in points, dash, marker symbol and size,
  secondary axis, `ChartTrendline` (linear, exponential, logarithmic, polynomial with order, power, moving average with
  period, forward/backward extrapolation, forced intercept, equation and R² display, name), `ChartErrorBars` (fixed
  value, percentage, standard deviation with multiplier, standard error; both/plus/minus; end caps).
- `ChartDataLabelOptions` (`ChartDefinition.DataLabels`, used when `ShowDataLabels`): value, category, series name,
  percentage, number format, placement.
- Pure services for drawing: `ChartAxisScaling` (applies an axis scale to an automatic `PlotScale`; `PlotScale` gained
  logarithmic positions and fixed major steps), `ChartTrendFit` (least squares curves, moving average),
  `ChartErrorSpans`.

## XLSX (also DOCX and PPTX charts, same `ChartSpaceBuilder`/`ChartSpaceReader`)

`ChartFormattingXml.cs`: `c:scaling` (`c:logBase`, `c:orientation`, `c:max`, `c:min`), `c:numFmt` with
`sourceLinked=0`, `c:crosses`/`c:crossesAt`, `c:majorUnit`/`c:minorUnit` on value axes; secondary axes as a second
chart group per family with axis ids 333333333/444444444, a deleted secondary category axis and a right value axis
crossing at the maximum; the reader gathers series from every chart group of the found families (ordered by `c:idx`)
and marks a series secondary when its group's axis ids differ from the first group's. Per series: `a:ln` width and
`a:prstDash`, `c:marker` (symbol, size), `c:trendline` (type, order, period, forward, backward, intercept, dispRSqr,
dispEq, name), `c:errBars` (errDir y on scatter and bubble, errBarType, errValType, noEndCap, val), `c:dLbls` with
`c:numFmt`, `c:dLblPos` (only positions the series type allows) and the show flags.

Declared: `XLSX241` (trendlines, error bars and secondary axes on pie, doughnut and radar series, which DrawingML has no
place for), `XLSX242` (major and minor units on a category axis, which `c:catAx` has no element for).

## ODS (also ODP charts)

`ChartFormattingOdf.cs`: one axis style per axis (`pulseoffice-axis`, `-axis-y`, `-axis-y2`) with `chart:minimum`,
`chart:maximum`, `chart:interval-major`, `chart:interval-minor-divisor` (our `pulseoffice:interval-minor` when the
minor unit does not divide the major one), `chart:logarithmic`, `chart:reverse-direction`, `chart:axis-position`
(start, end or value) and `style:data-style-name` with a number style written by the shared `OdfNumberStyleWriter`;
secondary axis `chart:axis` named `secondary-y` and `chart:attached-axis` on its series. Per series:
`chart:regression-curve` with a style carrying `chart:regression-type`, `-max-degree`, `-period`,
`-extrapolate-forward`/`-backward`, `-force-intercept`, `-intercept-value`, `-name` (read also from the vendor extension
namespace) and `chart:equation`; `chart:error-indicator` (dimension y) with `chart:error-category`,
`-upper-indicator`, `-lower-indicator`, `-upper-limit`/`-lower-limit`, `-percentage`; `chart:symbol-type`,
`chart:symbol-name`, `chart:symbol-width`/`-height`; `svg:stroke-width`; data labels `chart:data-label-number`
(value, percentage, both), `chart:data-label-text`, `chart:data-label-series`, `chart:label-position`, label number
style.

Declared: `ODF287` for what ODF has no attribute for and is kept only in our `urn:pulseoffice:chart` extension (dashed
series lines, since dash patterns need `draw:stroke-dash` definitions the chart object does not carry; logarithm base
other than 10; error bars without end caps; a standard deviation multiplier other than one; the dot marker, written as
a circle). Other applications see solid lines, base 10, capped bars, one standard deviation and a circle.

## Rendering (`PulseOffice.UI.Controls/Charts`, shared by the three modules)

Value axis scale (bounds, major unit, logarithmic, reversed) in column, bar, line, area and combination plots and both
axes of scatter and bubble plots; secondary value axis with its own scale and labels on the right for non-stacked
column, bar, line and combination charts; axis and data label number formats; data label options (series name,
category, value, percentage); series line width and dash and marker symbols in line and combination charts; trendlines
and error bars drawn over category and scatter charts (`ChartAnalysisPainter`).

## Editor

The shared chart picker (insert and edit, Ledger, Folio and Deck) gained a value axis scale section (minimum, maximum,
major unit, logarithmic, reversed) and an analysis section (trendline type, error bar type and amount, applied to every
series). Settings go through `ChartSettingsMapper`, so an edit stays one undo step; texts in the five languages.

## Tests

`tests/PulseOffice.Formats.OpenXml.Tests/Round168ChartModelTests.cs` (axis scales, series formats with trendline and
error bars, data label options, secondary column series, plain chart unchanged; each in XLSX and ODS),
`tests/PulseOffice.Charts.Tests/Round168ChartScaleTests.cs` (scaling, logarithmic and fixed divisions, trend fits,
error spans, editor settings mapping).

## Open

- Not built nor run in this round (coordinator builds and tests after the join).
- DOCX and PPTX writers do not yet declare `XLSX241`/`XLSX242` equivalents; ODT and ODP do not declare `ODF287`.
- Rendering: markers and dashes in scatter and radar plots, secondary axes for stacked and horizontal charts, the
  crossing position and minor units are not drawn; trendline equations and R² are not drawn.
- Editor: per-series formatting (line, marker, secondary axis), category and secondary axis scales, crossing, number
  formats and data label options are kept and written but not editable yet.
- OOXML custom error values (`c:plus`/`c:minus` ranges) and X error bars on scatter charts are not modelled; they are
  read as no error bars (only the Y bars of fixed, percentage, deviation and error kinds survive).

## Status

Model, both codecs, rendering and editor done for the items above; serious defect of round 166 closed, with the open
items listed as minor.
