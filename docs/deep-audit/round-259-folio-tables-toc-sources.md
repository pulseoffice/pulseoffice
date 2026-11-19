# Round 259: Folio tables in depth and TOC sources (2026-11-19)

Area: nested tables, cell margins and vertical alignment, merged cells, column widths, table styles and banding,
border conflicts, ODT table round trips, TOC hyperlinks, page numbers and update; plus the queued TOC `\t` and
first-level item from round 257.

## Defects found and fixed

| Severity | Defect | Fix |
|---|---|---|
| Medium | DOCX `TOC \t` (additional styles with levels) and the first level of `\o "2-4"` had no model property and were lost; ODT `text:index-source-styles` likewise; a TOC without `\o` was read as built from the outline | `IndexDefinition.FirstLevel` and `IndexDefinition.SourceStyles` (`IndexSourceStyles`, value-equal map of style name to level). DOCX writes and reads `\o "first-last"` and `\t "style,level,…"` (comma or semicolon); a TOC with `\t` or `\f` and no `\o` is not built from the outline (a bare `TOC` still is). Our settings blob carries both. ODT writes `text:use-index-source-styles` and `text:index-source-styles` per level natively; the first level, which ODF has no attribute for, goes to our `pt:first-outline-level`. The contents source starts at the first level and adds paragraphs of the source styles; renaming or deleting a style follows into the source styles. |
| Serious | A DOCX table without `w:tblGrid`, or with zero grid widths (written by several generators), read with zero-width columns or the fixed 2 cm fallback, ignoring `w:tcW` | Columns are recovered from unspanned and spanned cell widths (dxa, or pct of a dxa table width), then from the table width left over, then 2 cm. |
| Medium | ODT columns without `style:column-width` among measured ones stayed zero width (only the all-missing case was handled) | The missing columns share the table width left over, by `rel-column-width` when all have it. The writer now also writes `style:rel-column-width`, which other applications use for proportional tables. |
| Medium | Direct DOCX `w:tblCellMar` with only left and right margins (Word's usual form) set the vertical padding to 0, overriding the table style's top and bottom margins | A side-only margin leaves the vertical padding unset, so the style applies. |
| Minor | A DOCX table style whose top border is none but which draws other outer borders read as unbordered | The first drawn outer border is used. |
| Medium | Word TOC hyperlinks point at hidden `_Toc…` bookmarks, which the reader dropped unless a field referred to them: every imported TOC link went nowhere until the TOC was updated | Bookmarks targeted by any internal `w:hyperlink/@w:anchor` are kept. |

Tests: `Round259TocSourceTests` (OpenXml and Documents), `Round259OdtTocSourceTests`, `Round259TableGridTests`,
`Round259OdtColumnTests`, `Round259TableStyleCodecTests`, `Round259TocAnchorTests`.

## Checked, no defect

- Vertical merges (`vMerge` restart/continue with matching `gridSpan`), horizontal spans and covered cells are read and
  written consistently in DOCX and ODT; continuation cells carry the origin's borders and shading.
- Cell vertical alignment and text direction cross both formats; layout shifts middle and bottom content.
- Nested tables: DOCX cells always end with a paragraph after a nested table, as Word requires; ODT reads cell content
  through the block reader, nested tables included.
- TOC entries link to `__RefHeading__` bookmarks created at the heading; update rebuilds the entries with current pages
  and is one undo step.

## Still open (queued)

- Percent and auto table widths (`tblW` pct/auto, ODT `style:rel-width`) have no model property: tables are fixed-width
  in twips after reading.
- DOCX table styles that shade only `band1Horz`/`band1Vert` lose banding: the model shades only the second band.
- Border conflict resolution between adjacent cells: both edges are drawn centred on the shared line, so the wider one
  covers; same-width conflicts are decided by drawing order, not by Word's weight and colour rules.
- On TOC update in an imported Word document, a heading's existing `_Toc` bookmark is not reused; a new
  `__RefHeading__` bookmark is added beside it.
- The TOC dialog does not expose the first level or the additional styles (kept through edits of the other options).
- `\t` is written with commas; Word in locales whose list separator is the semicolon has not been checked in Word.
- ODT table columns defined in common (not automatic) styles are not looked up.
