# Round 195: chart items of the audit queue

Not built nor run in this round (the coordinator builds and tests after the join).

## 1. Multi-level category labels

- **Model.** `ChartData.CategoryLevels` holds the outer label levels, outermost first, each aligned with the leaf
  `Categories`; an empty label continues the group above it, a non-empty one starts a new group (the convention of a
  category range of several columns). `WithCategoryLevels` drops levels whose length does not match the categories.
  `ChartDefinition.CategoryLevels` carries levels the chart cannot read from cells (pivot charts).
  `ChartCategoryGroups` (Charts) turns the levels into groups per level, breaking an inner group wherever an outer one
  starts, and compacts full label paths into group-start labels.
- **Sheet charts.** `SeriesSourceReader` reads a category reference of several columns (series in columns) or rows
  (series in rows) as levels: the last column or row is the leaf, the others are outer levels; otherwise it applies
  `ChartDefinition.CategoryLevels`. The compact layout applies the definition's levels too.
- **Pivot charts.** `PivotChartLinks.Derive` compacts the row paths of the leaf rows into `rowFields - 1` outer levels;
  every layout puts parent items on their own rows, so they cannot be read from the leaf rows' cells. `Detach` keeps
  them in the inline data.
- **XLSX.** `OoxmlCategoryLevels` writes `c:multiLvlStrRef` (formula plus `c:multiLvlStrCache`: first `c:lvl` the
  leaves, then the outer levels from the innermost, outer levels sparse at group starts) whenever the data has levels
  and the categories have a formula. Literal categories (Folio and Deck charts, detached Ledger charts) have no
  multi-level literal in ECMA-376, so they keep `c:strLit` and carry the levels in its `c:extLst` (our
  `urn:pulseoffice:chart` `categoryLevels`); other readers see the leaves. The reader takes the leaves from the first
  level of a multi-level cache (before, every level's points overwrote one another) and the outer levels from the
  others or from our extension. A sheet chart read with a `multiLvlStrRef` over several columns keeps its per-series
  sources, so the levels come back from the cells.
- **ODS.** A category range of several columns is the native ODF form (`chart:categories` over the label columns) and
  is written and read as it is. The inline local table (ODT, ODP, static Ledger charts) appends the level columns after
  the series and hidden columns and names them with `pulseoffice:category-level-column` and
  `pulseoffice:category-level-count` on `table:table`; the reader also accepts a native categories range of several
  local-table columns.
- **Drawing.** `ChartAxisPainter.Groups` draws each outer level as a row below the leaf labels (a column to their left
  for bar charts) with separators at group boundaries; `ChartRenderer` reserves the rows or columns and moves the axis
  titles out of the way.

Tests: `Round195CategoryLevelTests` (Charts, Sheets.Engine), `Round195PivotChartLevelTests` (Sheets.Pivot),
`Round195ChartCategoryLevelTests` (Formats.OpenXml).

## 2. Custom error values outside the series columns

- `ChartData.HiddenColumns` (`ChartHiddenColumn`: sheet column index and values per category row) keeps columns of
  the chart's data table that are not plotted. `ChartInlineErrorValues.Refresh` first keeps every column a custom error
  reference points to beyond the series columns, filled from its cached values, then resolves references against the
  series and hidden columns. `Remap` rewrites error references when a reader moves table columns.
- The ODF local table writes hidden columns at their own column; the reader now takes the series columns from each
  `chart:series` values address instead of assuming every column after A is a series, keeps the remaining non-empty
  columns as hidden columns, renumbers them after the series and remaps the error references. So a local table where
  error columns sit between series (the reference suite's layout) no longer turns them into extra series.
- The XLSX, DOCX and PPTX readers of inline charts run `Refresh` after reading, so hidden columns exist before the
  first save. OOXML needs no storage of its own: the error reference and its cache carry the column.

Tests: `Round195InlineErrorColumnTests` (Charts), the ODF case in `Round195ChartCategoryLevelTests`.
`Round190InlineErrorValueTests.References_outside_the_table_keep_their_cache` became
`References_below_the_table_keep_their_cache`: a reference beside the series is now kept as a hidden column.

## Research: how pivot chart links are stored (read only)

- **XLSX, other applications.** The chart part carries `c:pivotSource` with `c:name` (`[workbook]Sheet!PivotName`,
  where the workbook part is the file name; an empty `[]` is what some writers emit for the current file) and
  `c:fmtId`; `c:chart` carries `c:pivotFmts` (one `c:pivotFmt` per formatted series or point) and the `c14:pivotOptions`
  extension with the drop-zone flags. The series formulas point at the pivot table's body cells, categories commonly as
  `c:multiLvlStrRef` when several row fields exist. The pivot table definition lists `chartFormats` linking each chart
  format id and series to a pivot area. Spreadsheet applications rebuild the chart from the pivot table on refresh and
  use the formulas and caches only until then.
- **ODS, other applications.** The reference suite marks a pivot chart with the vendor extension attribute `data-pilot-source` on
  `chart:chart` and rebuilds its data from the pivot table; the cell range addresses in the chart are informative.
  There is no standard ODF attribute for the link.
- **Our files.** XLSX writes `c:pivotSource` (`[]Sheet!Pivot`, `c:fmtId`), `c:pivotFmts` and the drop-zone extension,
  series formulas over the leaf rows (multi-area unions) and, now, `c:multiLvlStrRef` with the outer levels. We do not
  write `chartFormats` in the pivot table definition. ODS writes the vendor extension attribute `data-pilot-source` plus our `pulseoffice:pivot-*`
  attributes and the leaf ranges. Both readers rebuild the chart from the pivot definition after loading
  (`FollowCharts`).
- **Remaining risk.** Excel may rename or rebuild a link whose `c:name` lacks the file name, and may expect
  `chartFormats` entries for formatted series; a multi-area category formula inside `c:multiLvlStrRef` is legal but
  unusual. Nothing was opened in another application this round; the items stay in the audit queue.
