# Round 275: Folio table style regions (2026-11-19)

Area: the Folio table style model, from the round 271 queue items.

| Severity | Defect | Fix |
|---|---|---|
| Medium | The table style model had no total-row, first-column, last-column or corner formatting: DOCX `lastRow`, `firstCol`, `lastCol`, `nwCell`, `neCell`, `swCell`, `seCell` conditional formats were dropped on reading (only a bold-only emphasis was written back) | `TableProperties` gains seven `TableRegionFormat` regions (background, text colour, bold) addressed by `TableRegion`, merged per field by `Over`. `TableAppearance` layers them as Word does (bands under columns, columns under header and total rows, corners over both) and only when the table turns the region on (`TableBanding.FirstColumn`, `LastColumn`, `LastRow`). DOCX writes and reads them as `w:tblStylePr`; ODT as `pt:table-region` children of the table style. The style editor has three new tabs. |
| Medium | A derived table style could not clear an inherited colour to none from the style editor: an emptied field fell back to the parent's colour | A cleared colour is stored as `TableRegionFormat.NoColour` (alpha 0), which overrides the parent and is drawn as no colour (`TableAppearance` maps it to none). DOCX writes it as `auto` fill or colour, ODT as `transparent`; both read it back. The border colour keeps the old behaviour (a line always has a colour). |
| Minor | Automatic width measured a nested percent table by its stored columns | `TableMeasure` measures a nested percent table by its content, as an automatic one. |
| Minor | `tcW` in percent units was ignored when a DOCX table had no grid or was automatic | `DocxBlockReader.Grid` resolves percent cell widths against the table width (a percent table against a nominal 6.5 in text width, an automatic table against its grid), and an automatic table's percent preferred width raises its column like a `dxa` one. |
| Minor | The DOCX explicit-empty-side pass (`ICellInheritedEdges`) was tested only on the model | Tested through a DOCX file. |

Research: ECMA-376 Part 1, 17.7.6 (`tblStylePr` types and their precedence); ODF 1.3, 16.18 (`table:table-template` with
`table:first-row`, `table:last-row`, `table:first-column`, `table:last-column`, `table:odd-rows`, `table:even-rows`,
`table:odd-columns`, `table:even-columns`, `table:body` naming cell styles). The native template needs a cell style per
region and the template name on every table; Folio tables do not write templates yet, so the regions use our
round-tripping extension and the native template is queued.

Tests: `Round275TableRegionAppearanceTests`, `Round275TableStyleRegionFormTests`, `Round275TableMeasureTests`,
`Round275TableRegionStyleTests` (DOCX and ODT round trips, the nil-side pass and percent cell widths from DOCX files).

Open (queued in docs/audit-queue.md): native ODT table templates for Folio; borders, italics and fonts of region
formats; quick looks without regions; the nominal width for percent cells without a grid; live checks of the editor.
