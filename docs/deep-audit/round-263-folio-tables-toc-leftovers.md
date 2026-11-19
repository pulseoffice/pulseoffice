# Round 263: Folio tables and TOC leftovers from round 259 (2026-11-19)

Area: the items queued by round 259 for Folio tables and tables of contents. No build or test ran in the round (audit
cycle rule); the coordinator builds and tests at the join.

## Defects found and fixed

| Severity | Defect | Fix |
|---|---|---|
| Medium | Percent and automatic table widths (DOCX `tblW` `pct`/`auto`, ODT `style:rel-width`) had no model property: every table became fixed-width after reading, and DOCX always wrote `auto` | `TableBlock.WidthPercent` and `TableBlock.AutoWidth` (cloned, kept by copy and by table split). DOCX writes and reads `tblW w:type="pct"` (fiftieths, or the `50%` form) and `auto`; ODT writes `style:rel-width="N%"` natively beside `style:width`, reads it and `table:align="margins"` as 100%; automatic width goes to ODT as our `pt:auto-width`. Layout scales the stored columns to the percent of the available width (`TableItem.Width`, `ColumnLeft`, `ColumnWidth`). |
| Medium | DOCX table styles shading only `band1Horz` or `band1Vert` lost their banding: the model only had the second band | `TableProperties.FirstBandBackground`; DOCX reads `band1Horz` then `band1Vert` (and `band2Horz` then `band2Vert` for the second band) and writes both bands; ODT carries it as `pt:first-band-background` in the table style, like the second band. `TableAppearance` shades even body rows (or columns) with the first band. |
| Medium | Border conflicts between adjacent cells were decided by drawing order and width | `ICellBorderConflicts` (`CellBorderConflicts`, injected into `TableAppearance`): for every right/left and bottom/top pair of neighbouring cells the winner is chosen by weight (width times the style number of the specification: single 1, double 3, dotted 4, dashed 5), then style rank, then the darker colour (smaller R + B + 2G), then the leading cell; both cells get the winner. `ITableAppearance.Cells` returns the resolved grid, used by the flow builder. |
| Minor | ODT table column widths in common (non-automatic) styles were not looked up | `OdtAutomaticStyleIndex` falls back to common `table-column` and `table-row` styles. |
| Minor | TOC update in an imported Word document added a `__RefHeading__` bookmark beside the heading's `_Toc` bookmark | `ReferenceTargets` treats a `_Toc` bookmark start in the heading as its anchor (after our own `__RefHeading__` at offset 0), for `Prepare`, `Touches`, `HeadingAnchor` and `HeadingOf`. |
| Minor | The TOC dialog did not expose the first level or the additional styles | First level beside the levels, clamped to them; a Styles tab with one level field per visible paragraph style (0 leaves it out), applied through `IndexSourceStyles.Assign`. |

Tests: `Round263TableWidthTests` (OpenXml: DOCX and ODT), `Round263TablePercentWidthTests` (Layout),
`Round263TableBandTests` (OpenXml), `Round263CellBorderConflictTests` (Documents), `Round263OdtCommonColumnStyleTests`
(Odf), `Round263TocBookmarkReuseTests` and `Round263TocSourceStyleAssignTests` (Documents).

## Still open (queued)

- Percent tables keep their percent when columns are resized or the width is set in the table dialog; no percent or
  automatic width control in the dialog; `AutoWidth` tables are not auto-fitted.
- First-band shading has no control in the table style editor; `band1Vert` alone also shades rows when row banding is on.
- Word's special cases of border conflicts (nil borders, cell against table borders, merged-cell spans) are not modelled
  and the result is not compared with Word.
- The TOC styles tab lists every visible paragraph style; long with many styles, names not localized, not seen live.
- `\t` with commas under a semicolon list separator is still unchecked in Word (from 259).
