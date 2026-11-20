# Round 333: chart best fit labels and Ledger sparklines

Area: best fit data labels of the shared chart renderer, and a fresh audit of Ledger sparklines (kinds, groups,
axis bounds, markers, colours, empty and hidden cells, date axis) with their XLSX and ODS round trips.
No builds or tests were run (audit cycle rule); the coordinator runs them at the join.

## Defects found

### Serious
1. **Sparklines whose data lies on another sheet lost their sheet.** The XLSX reader dropped the sheet name of
   `xm:f`, the ODS reader dropped the sheet of `calcext:data-range`, and the model had no place for it: the sparkline
   was redrawn from the same range of its own sheet (wrong data) and written back that way. Fixed:
   `Sparkline.SourceSheet` (null for the host sheet), read and written in both formats, read by `SparklineData` through
   the workbook, followed by sheet renaming (one undoable plan with the other rename effects), left alone by row and
   column edits on the host sheet and used by range moves.

### Medium
2. **Best fit data labels covered the plot marks.** The greedy pass avoided only labels drawn before. Fixed:
   `ChartLabelLayout.Marks` builds an obstacle for every mark (a small square around each point, the span of each
   bar) and `BestFit` avoids them too; a bar label still ignores its own bar, so inside placements remain possible.
3. **Line sparklines were scaled from zero.** Automatic bounds always included zero, flattening series such as
   100–110 into a straight line; Excel and the reference suite scale from the lowest value. Fixed.
4. **Many group options were dropped by both formats and ignored by the drawing**: all markers (`markers`), axis
   (`displayXAxis`, `colorAxis`), first/last/high/low colours, empty cells (`displayEmptyCellsAs`, always written as
   gap and read as nothing), hidden data (`displayHidden`), separate and custom minimum and maximum
   (`minAxisType`/`maxAxisType` custom with `manualMin`/`manualMax`; only "group for both" survived), right to left,
   and the date axis (`dateAxis` with its `xm:f`). Fixed in the model (`SparklineGroup`, `SparklineStyle`), in
   `XlsxSparklineMapper` (theme colours now resolved through the workbook palette), in `OdfSparklineMapper` (the
   native `calcext` attributes: `display-empty-cells-as`, `markers`, `display-x-axis`, `display-hidden`,
   `right-to-left`, `min-axis-type`/`max-axis-type` with `custom`, `manual-min`/`manual-max`, `date-axis`,
   `color-axis`, `color-first`, `color-last`, `color-high`, `color-low`), and in drawing: gaps break the line unless
   "connect", zero reads empty cells as 0, hidden rows and columns are left out unless shown, custom bounds clip,
   points are spaced by date on a date axis, right to left mirrors, the axis is drawn when the range crosses zero, and
   each special point uses its own colour.
5. **The insert dialog offered no empty-cell, hidden-data, right-to-left, all-markers or axis choice.** Added (texts in
   the five languages).

Research for the date range in ODS: the `calcext` form carries only the `date-axis` flag, not the dates' range, so
the range is written as our own `urn:pulseoffice:spreadsheet` `sparkline-date-range` attribute; other readers see a
date axis flag without dates and fall back to even spacing.

## Tests
- `Round327BestFitLabelTests`: a label moves off another point's marker; a free label keeps its place beside its own.
- `Round333SparklineTests` (XLSX and ODS): every group option, a foreign source sheet with a space and a date range
  cross the format.
- `Round333SparklineDataTests`: line bounds from the data, empty cells as gap/zero/span, hidden columns, custom
  bounds, date positions and mirroring, foreign source read and renamed with undo, row insertion on the host.

## Still open (queued)
See docs/audit-queue.md, items marked (333).
