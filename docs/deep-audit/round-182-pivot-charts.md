# Round 182: pivot charts in Ledger

Closes the first open item of round 177: an XLSX chart linked to a pivot table was read as an ordinary chart over the
pivot's drawn output, the link was lost silently, and a refresh that grew the pivot did not grow the chart.

## Model

- `ChartPivotSource(PivotName)` with `FormatId` and `ShowFieldButtons`, held by `ChartDefinition.PivotSource`
  (`IsPivotChart`). The link is by pivot name, as in OOXML.

## Following the pivot

- `IPivotChartLinks` / `PivotChartLinks` (Sheets.Pivot) derives a linked chart's series from the pivot output:
  categories are the leaf row items (label column of the compact or tabular layout), each leaf column item is a series
  named by its header cell; subtotal and grand-total rows and columns are left out. The result is written as per-series
  sources (`SeriesSources`) plus the covering compact range.
- `PivotCoordinator.Apply` calls `Follow` after every successful render (create, refresh, relayout, calculated field), so
  the charts change inside the same checkpointed undo step as the pivot. A rename keeps the link (charts are matched by
  the pivot's previous name and take the new one).
- `PivotCoordinator.Remove` calls `Detach` before clearing the output: the chart becomes a static chart over its last
  values (`InlineData`, no sheet source), which is what the reference spreadsheet applications do when a pivot is
  deleted. `ChartDataExtractor` now returns the inline data of a chart that has no sheet source.
- Registered in `LedgerDataComposition`.

## Formats

- XLSX: writes `c:pivotSource` (`c:name` = `Sheet!PivotName`, `c:fmtId`) before `c:chart`, an empty `c:pivotFmts` in
  `c:chart`, and, when field buttons are shown, the `c14:pivotOptions` chart-space extension; reads the same back
  (name after the last `!`). Static charts are written with literal values (`numLit`/`strLit`) and read back as
  `InlineData` when no series has a formula.
- ODS: writes `pulseoffice:pivot-source`, `pulseoffice:pivot-format` and `pulseoffice:pivot-field-buttons` on
  `chart:chart`, plus the vendor `loext:data-pilot-source` attribute that the reference suite uses for the same link;
  reads either. Static charts are written with a local table only and read back as `InlineData`.
- Declared: an application that knows neither attribute sees an ordinary chart over the pivot output (the series still
  reference the output cells), so nothing is lost silently for data; only the link degrades. The written
  `loext:data-pilot-source` and the XLSX link were not opened in the other applications in this round.

## Command

- `ledger.pivot.chart` ("Pivot Chart", five languages, icon): Data › Pivot Table menu (prefix `ledger.pivot.`) and the
  Insert › Charts ribbon group. Enabled when the workbook has a pivot; needs the cursor in a pivot's output. Inserts a
  column chart to the right of the pivot and re-applies the pivot in one checkpointed step, so the chart is derived
  immediately and undone in one ⌘Z.

## Tests

- `Round182PivotChartTests` (Sheets.Pivot.Tests): growth on refresh without grand total, rename keeps the link, delete
  leaves a static chart over the last values.
- `Round182PivotChartFormatTests` (Formats.OpenXml.Tests): link through XLSX and ODS, static chart values through both.

## Open

- With several row fields the categories span from the first to the last leaf row, so interleaved parent and subtotal
  rows fall inside the series; multi-level categories are not modelled.
- Only the last column-header level names a series when there are several column fields.
- The XLSX `c:name` is written without the `[file]` prefix that Excel writes; not yet verified in Excel.
- Charts are re-derived only when the pivot is applied, not on load: a file whose cached chart references are stale
  keeps them until the next refresh.
- Not built or run in this round (the coordinator builds and tests at the join).
