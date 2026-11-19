# Round 269: Folio table and TOC leftovers from round 263 and its join (2026-11-19)

Area: the Folio table and TOC items queued by round 263 and by the join of rounds 262-265. No build or test ran in
the round (audit cycle rule); the coordinator builds and tests at the join.

## Defects found and fixed

| Severity | Defect | Fix |
|---|---|---|
| Medium | A shared edge between two cells was drawn by both cells at identical bounds after border conflict resolution | The table emission keeps a set of drawn cell edges (bounds rounded to a thousandth of a point, colour, style) and emits each edge once. `IBordiPropriDellaCellaSiDisegnanoLatoPerLato` now asserts a single dashed edge. |
| Medium | Border conflicts were resolved only with the neighbour in the first row (or column) of a merged cell | `CellBorderConflicts` resolves the right edge against every row the cell spans and the bottom edge against every column; the merged cell keeps the shared winner, or its own line when segments differ. |
| Medium | A table style shading only `band1Vert` (or `band2Vert`) also shaded rows | `TableProperties.BandAxes` and `FirstBandAxes` (`TableBandAxes`: rows, columns); DOCX reads which of the horizontal and vertical bands are shaded and writes only those, ODT carries them as `pt:band-axes` and `pt:first-band-axes`; `TableAppearance` shades only along those axes. |
| Medium | The table dialog had no width control; a percent table could not be made fixed or automatic, and widening its last column was undone by the percent scaling | Table tab: width mode (fixed, percent of the text width, automatic) with a width in cm or a percent, applied by `ITableGeometryEditing.SetTableWidth` in the dialog's undo step. `ResizeColumn` scales the percent by the change of the column total, so the table widens as dragged. |
| Medium | `AutoWidth` tables scaled their stored columns instead of fitting their contents | The flow builder measures each column's longest word and unwrapped text (`ITableMeasure`, injected) for automatic tables; `TableItem` fits them as Word does: unwrapped widths when they fit (the stored widths act as preferred minimums), the minimums when even those overflow, otherwise the room above the minimums shared in proportion to each column's range. |
| Minor | The TOC styles tab listed every visible paragraph style under its stored name | `IFolioIndexStyleCandidates` lists the styles used in the document or already assigned, sorted and shown under their localized names (`IFolioStyleNames`). |

Tests: `Round269CellBorderSpanTests`, `Round269TableWidthEditingTests` (Documents), `Round269TableBandAxesTests`
(OpenXml), `Round269TableAutoFitTests` (Layout), `Round269IndexStyleCandidatesTests` (Documents.UI), and the updated
`TableAppearanceLayoutTests.IBordiPropriDellaCellaSiDisegnanoLatoPerLato`.

## Research and decisions

- Word's `nil` border is read as no line, so it loses to the neighbour's line in a conflict; distinguishing `nil` from
  `none` would need a third state on `BorderLine` in both codecs and stays queued until it can be compared with Word.
- A cell border against a table border: DOCX reading already merges the cell's own sides over the inherited table
  sides, so the conflict runs between two concrete lines.

## Still open (queued)

- The drag delta on a percent-scaled table is applied to stored widths.
- No table style property editor exists; band shadings come from files and presets.
- Automatic width ignores spanning cells, nested tables and `tcW`; not compared with Word.
- `nil` borders and merged cells whose segments resolve differently; not compared with Word.
- The TOC styles tab cannot assign a style not yet used.
