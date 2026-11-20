# Round 279: Folio table templates and region text formats (2026-11-20)

Area: the round 275 leftovers on Folio table styles.

| Severity | Defect | Fix |
|---|---|---|
| Medium | ODT table styles carried total-row, column and corner formats only in our extension; other ODF applications saw no table template | `OdtTableTemplates` writes a native `table:table-template` per table style in `office:styles` (first-row, last-row, first-column, last-column, body, even/odd rows and columns), each part naming a `table-cell` style with shading, borders and text properties. Tables name their template with `table:template-name` and say `use-first-row-styles` and `use-banding-rows-styles` explicitly (the ODF default for banding is false, so it was wrong in other readers). A template with no table style of ours (a file from another application) is read back into a table style; a table with only `table:template-name` gets that style. |
| Medium | Region formats held only shading, text colour and bold: italics, fonts, sizes and lines of Word's conditional formats, and of the header and band formats, were dropped | `TableRegionFormat` gains `Italic`, `Font`, `FontSize` and `Borders`, merged field by field (borders side by side). New regions `HeaderRow`, `FirstBand`, `SecondBand` carry the extra formats of the header and bands (their shading, colour and weight stay in the dedicated fields). `TableAppearance` layers them (bands under columns under header and total under corners), overlays region lines on the style lines, and `CellAppearance` carries italic, font and size, applied by the layout and baked into ODT cells like colour and bold (and stripped on reading). DOCX writes and reads `w:rFonts`, `w:i`, `w:sz` and `w:tcBorders` in every `w:tblStylePr`; ODT writes them as attributes and a cell-properties child of `pt:table-region` and in the native template. The style editor has italic, font, size and line fields for every region, the header and both bands. |
| Minor | The quick table looks set no total-row or column formats | `TableFormatting.EnsurePreset` gives the total row bold text and a double line above in the look's border colour, and bold first and last columns. |

Research: ODF 1.3 part 3, `table:table-template` and its region elements, which name cell styles; the table
attributes `table:template-name` and `table:use-*-styles` (banding defaults to false). ODF 1.3 has no corner-cell
elements in the template, so corners stay in our `pt:table-region` extension (round-tripping, ignored elsewhere).
ECMA-376 17.7.6: `tblStylePr` carries `rPr` and `tcPr` (with `tcBorders`), which now map in full for the modelled fields.

Tests: `Round279TableRegionTextTests`, `Round279QuickLookRegionTests` (Documents), `Round279TableRegionTextStyleTests`
(DOCX and ODT round trips and a template-only ODT), `Round279TableStyleTextFormTests` (editor).

Open (queued): corner cells only in our extension in ODT; the template's paragraph styles
(`table:paragraph-style-name`) are not written; a region line set to zero width cannot override an inherited line; a
cleared font field cannot override an inherited font; the native templates and new editor fields not seen in another
ODF application or live.
