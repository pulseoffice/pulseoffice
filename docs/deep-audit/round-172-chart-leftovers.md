# Round 172: chart leftovers of round 168

Follows `round-168-chart-model.md`, whose open items were: custom and horizontal error bars, declarations in the word
processor and presentation writers, rendering gaps and editor gaps. Not built nor run in this round (the coordinator
builds and tests after the join).

## Defects and changes

1. **Medium: custom error values read as fixed bars of one.** OOXML `c:errValType val="cust"` fell back to a fixed value
   of 1, so a file with error bars taken from cells came back with wrong bars and saved them that way. The model now has
   `ErrorBarKind.Custom` and `ChartErrorBars.Plus`/`Minus` (`ChartErrorValues`: model reference plus cached values).
   XLSX (and DOCX/PPTX charts) write and read `c:plus`/`c:minus` as `c:numRef` with formula and cache, or `c:numLit`
   when there is no reference (`OoxmlRangeFormulas`, shared with `ReferencedSeriesSource`). ODS/ODP/ODT write
   `chart:error-category="cell-range"` with `chart:error-upper-range`/`chart:error-lower-range` and keep the cached or
   literal values in `pulseoffice:error-upper-values`/`error-lower-values` of `urn:pulseoffice:chart`; a side without a
   reference is declared by `ODF287`. `ChartErrorSpans` draws each point with its own values.
2. **Medium: X error bars of scatter series were dropped.** `ChartSeriesFormat.XErrorBars`; OOXML `c:errBars` with
   `c:errDir val="x"` on scatter and bubble series, ODF `chart:error-indicator chart:dimension="x"`; drawn horizontally
   by `ChartAnalysisPainter`.
3. **Medium: editing the trendline overwrote every series' error bars.** `ChartSettingsMapper` applied trendline and
   error bars together whenever either changed, so changing the trendline copied the first series' error bars to all
   series. Each is now applied only when it changed, and custom error bars are never spread from the first series. The
   editor lists custom bars as "Values from cells" (five languages) only when the chart has them, and keeps them.
4. **Minor: DOCX, PPTX, ODT and ODP did not declare chart losses.** `DOCX241`/`DOCX242`, `PPTX241`/`PPTX242`,
   `ODT287`/`ODP287` mirror `XLSX241`/`XLSX242`/`ODF287`; the ODF message is shared (`OdfChartExtensions.Loss`).
5. **Minor: trendline equation and R² not drawn.** `ChartTrendFit.Summary` gives the equation and the coefficient of
   determination (computed on the original values) for linear, exponential, logarithmic, power and polynomial fits; the
   painter writes them at the end of the curve.
6. **Minor: scatter and radar ignored series line formats and markers.** They now use the shared `ChartSeriesStroke`.

## Tests

`tests/PulseOffice.Formats.OpenXml.Tests/Round172ChartErrorTests.cs` (custom values from cells and horizontal bars in
XLSX and ODS, literal values declared in ODS, per-point spans), `tests/PulseOffice.Charts.Tests/Round172ChartSettingsTests.cs`,
`tests/PulseOffice.Charts.Tests/Round172TrendSummaryTests.cs`.

## Open

- Custom error values are drawn from the values cached at read time; a Ledger chart does not yet refresh them from
  the referenced cells, and references in error bars do not follow row, column and sheet edits like `SeriesSources`.
- Equation coefficients are formatted with the invariant culture.
- Rendering: secondary axes on stacked and horizontal charts, the axis crossing position and minor ticks.
- Editor: per-series format (line, marker, secondary axis), category and secondary axis scales, crossing, number formats
  and data-label options are still kept and written but not editable.
- No test yet for the DOCX/PPTX/ODT/ODP declarations.
