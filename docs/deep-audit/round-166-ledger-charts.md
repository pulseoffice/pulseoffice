# Round 166: Ledger charts and the print minors of round 161

Area: Ledger charts (decision 25) through the ODS and XLSX round trip, the shared chart editor and chart data extraction
with hidden rows, columns and empty cells; plus the open minors of round 161 (page arithmetic in header codes, manual
breaks under fit-to-pages, ODS row wrappers, `&N` in a whole-workbook print). Nothing was built or run here: the
coordinator builds and runs the tests after the join.

## Defects fixed

- Medium: the ODS reader flattened `table:table-row-group` and `table:table-header-rows` but not the
  `table:table-rows` wrapper (nor `table:table-columns`), so rows written inside it by other producers were lost or
  shifted. `TableRows.Flattened` now takes the set of wrapper names. Test: `Round166OdsTableRowsTests`.
- Minor: `&P+n` and `&P-n` printed the offset as literal text. `HeaderFooterCodes` reads the offset into the field token
  (`HeaderFooterToken.Offset`), `HeaderFooterFields` adds it, and ODS maps it natively to `text:page-adjust` on
  `text:page-number` in both directions; XLSX keeps the code string as written. Tests: `Round166PageOffsetTests`,
  `Round166OdsPageAdjustTests`.
- Minor: under fit-to-pages, manual row and column breaks still split the pages; the other spreadsheet applications
  ignore them there. `SheetPrintDocument` drops the breaks when `PageSetup.FitsToPages`. Test: `Round166FitBreaksTests`.
- Medium: with "visible cells only", hiding a series column (or row) made the following series take its explicit name
  and colour, because both were looked up by the visible position. The extractor now indexes them by the series'
  position in the range. Test: `Round166HiddenSeriesTests`.

## Verified, no defect

- `&N` in a whole-workbook print: `JobPageNumbering` gives every part the job total, also across restarted sections.
- The chart editor applies its result as one `DelegateAction` history step; the insertion is one step too.
- XLSX combo charts read bar series before line series, consistent with the model's "last N series are lines".

## Open

- Serious (model gap, needs a block): the chart model has no axis scale (minimum, maximum, units, logarithmic,
  reversed, crossing), axis number formats, secondary axis, trendlines, error bars, per-series line and marker
  formatting or data-label options beyond on/off. Reading an XLSX or ODS that carries them drops them silently and a
  save loses them; nothing declares the loss. Rule 7 asks for the native mapping first, so this is a model extension
  (an axis record per dimension, per-series trendline, error-bar and marker records) with both codecs and the editor,
  not a patch.
- Minor: an ODS combo chart whose line series are not the trailing ones is read as "last N series are lines", which
  changes the type of the wrong series; a per-series type list in the model would remove the restriction.
- Minor: in a column-and-line chart with a hidden series under "visible cells only", the line/column split counts the
  visible series, so a column series can be drawn as a line.
- Minor: confirming the chart editor without changes still pushes a history step.
