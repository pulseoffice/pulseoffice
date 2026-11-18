# Round 190: queued chart and table items

Not built nor run in this round (the coordinator builds and tests after the join).

## Charts

1. **Reversed category axis on statistical, financial and radar plots.** `ChartPlotBase.Across` builds the horizontal
   category scale with the axis' `Reversed` flag; histogram, Pareto, box, point, waterfall, stock and parallel
   coordinate plots use it, and the statistical and financial plots declare `DrawsReversedCategories` so the drawn
   category labels follow. Radar spokes are a `RadarSpokes` record that turns counter-clockwise when reversed.
   Test: `Round190ReversedCategoryTests` (UI.Controls).
2. **Tests for the chart loss declarations** DOCX241/242, PPTX241/242, ODT287 and ODP287, each with a chart that
   triggers it and one that does not: `Round190ChartLossTests` (Formats.OpenXml).
3. **Custom error values of Folio and Deck charts.** `ChartInlineErrorValues` (Charts) resolves a custom error value
   reference against the chart's own data table (row 1 headers, column A categories, series from column B; forms
   `Sheet1!$C$2:$C$4`, `local-table.$C$2:.$C$4`) and replaces the cached values. References outside the table keep
   their cache. It runs in `ChartRenderer` for every inline chart that is not a pivot or sheet chart, and in
   `InlineChartData.Settle`, so the model written after an edit holds the current values.
   Test: `Round190InlineErrorValueTests` (Charts).
4. **Pivot charts.**
   - Chart source references accept several areas, comma-joined in the model (`ChartSourceReferences.Areas`,
     `Write(areas)`); the series reader, the reference rewrites (structure, move, rename), the XLSX union formula
     `(Sheet!$A$3:$A$4,Sheet!$A$6)` (`OoxmlRangeFormulas`) and the ODF range address list (`SeriesSourceAddresses`)
     handle them.
   - `PivotChartLinks.Derive` writes categories and values over the runs of leaf rows only, so parent and subtotal
     rows of several row fields stay out; the series name references the header cell of every column level and the
     reader joins the non-empty texts.
   - `IPivotCoordinator.FollowCharts` derives every linked chart again from the pivot definition without rendering;
     `SpreadsheetModuleFormats` calls it after reading a workbook with pivots.
   - XLSX `c:pivotSource/c:name` is written as `[]Sheet!Pivot`, the form with the empty workbook prefix; the reader
     strips any `[...]` prefix.
   Test: `Round190PivotChartTests` (Sheets.Pivot).
5. The unused `chart.scale` text key is removed from the five language files.

## Tables

6. **Every table style option in the shared gallery.** `TableBanding` (Document.Tables) holds banded rows, banded
   columns, first column, last column and total row. `TableStyleGalleryView` offers all of them next to the heading
   row and previews them; `TableStyleChoice` carries the record. Deck maps it onto `DataTableStyle`
   (`ToBanding`, `WithBanding`); Ledger applies it to the selection (band columns, bold first/last column and total
   row, a rule over the total row); Folio tables carry `TableBlock.Banding` (undoable `SetBanding`, clone, snapshot,
   clipboard, split) and `TableAppearance` draws banded columns, bold emphasis where the style has a bold heading,
   and a rule over the total row of tables without inner lines. Folio no longer creates "No Bands" style copies;
   an older table using one opens the gallery with banded rows off. ODT writes and reads the standard
   `table:use-banding-rows-styles`, `use-banding-columns-styles`, `use-first-column-styles`, `use-last-column-styles`
   and `use-last-row-styles` (banded rows default to on when the attribute is absent); DOCX writes `w:tblLook` from the
   flags (`DocxTableLooks`) and the table styles it writes gain bold `firstCol`, `lastCol` and `lastRow` conditions and
   a `band2Vert` shading. New texts `table.style.firstColumn`, `lastColumn`, `lastRow`, `bandedColumns` in the five
   languages. Tests: `Round190TableStyleGalleryTests` (UI.Controls), `Round190TableBandingTests` (Formats.OpenXml).
7. **DOCX `tblLook` flags are read** (`noHBand`, `noVBand`, `firstColumn`, `lastColumn`, `lastRow`, from the
   attributes or the hexadecimal `w:val`), so a Word table that turns banding off over a banded style shows no bands.
   Test: `Round190TableBandingTests`.

## For the join run

- Word tables commonly carry `firstColumn` on: with a style whose heading is bold, their first column is now drawn
  bold. Fixture-based table tests that check cell boldness may need their expectations reviewed.

## Open

- Multi-level category labels for pivot charts are not modelled (leaf labels only); the `[]` c:name form and the
  multi-area references are not yet checked in other applications.
- Custom error values of inline charts are refreshed only when their reference lies inside the series columns of the
  chart's own table.
- The table insert dialog still offers only heading row and banded rows; Deck's own style options dialog duplicates
  the four options now in the shared gallery.
