# Round 178: chart editor and leftovers of rounds 168 and 172

Not built nor run in this round (the coordinator builds and tests after the join).

## Defects and changes

1. **Medium: custom error bar references did not follow sheet edits.** Inserting or deleting rows and columns, moving a
   range or renaming a sheet left `ChartErrorBars.Plus`/`Minus` pointing at the old cells. `ChartErrorValueReferences`
   (Sheets.Engine) maps every custom error value reference of a chart; `ChartReferenceRewrite.Rewrite`, shared by the
   structure, range move and sheet rename rewrites, now applies it after the series sources, so the error references
   follow exactly like `SeriesSources`, become `#REF!` when their cells disappear, and are undone in the same step
   (structure mementos, `ObjectReferenceChange` for renames).
2. **Medium: custom error values were never refreshed from the cells.** `ChartErrorValueRefresh` reads the referenced
   cells (bounded by the used area for whole rows or columns; text and empty cells become NaN, drawn as no bar) and
   leaves literal values and unresolvable references untouched. Ledger applies it when painting the grid, when
   rasterizing objects and when writing XLSX and ODS, so the caches written are the current values.
3. **Minor: trendline equation coefficients used the invariant culture.** `IChartTrendFit.Summary` takes an optional
   format provider; the shared `ChartSeriesStroke` passes the interface culture used for the R² line, so all three
   modules show `y = 1,5x + 0` in Italian.

## Tests

`tests/PulseOffice.Sheets.Engine.Tests/Round178ChartErrorReferenceTests.cs`,
`tests/PulseOffice.Charts.Tests/Round178TrendLocaleTests.cs`.

## Open

- Rendering: secondary axis on stacked and horizontal charts, axis crossing position and minor ticks are still not drawn.
- Shared chart editor: per-series format (line width, dash, marker, secondary axis), category and secondary axis
  scales, crossing, axis number formats and data-label options are still kept and written but not editable.
- Folio and Deck charts carry inline data, so their custom error values stay the cached ones by design.
- No test yet for the DOCX/PPTX/ODT/ODP chart loss declarations (from round 172).
