# Round 257: Folio tables and tables of contents

## Scope
Floating tables, row splitting, repeated header rows, table look, DOCX `w:tblpPr`, `w:cantSplit`, `w:tblHeader`;
TOC field switches and the index model.

## Defects
- Medium, fixed: a DOCX floating table positioned by `w:tblpXSpec` / `w:tblpYSpec` (centered, right, bottom,
  inside, outside) lost its alignment on reading and was placed at offset 0; the alignment was never written. Now read
  into `ObjectPlacement.HorizontalAlign` / `VerticalAlign` and written back (`DocxFloatingTables`). ODT carries it
  through the frame position as for any anchored object.
- Minor, fixed: `TOC \o` without a range was read as three levels; Word uses all nine. Ranges above nine are clamped.
- Minor, open: the TOC `\t` switch (additional styles with levels) and the first level of `\o "2-4"` have no model
  property and are dropped on reading; ODT `text:index-source-styles` likewise.
- Minor, open: `\o` absent with only `\t` or `\u` is still read as built from the outline.

## Checked without defects
`w:cantSplit` and `w:tblHeader` read and write; header rows feed `tblLook` first row.

## Tests
`DocxFloatingTableTests.A_floating_table_aligned_by_tblpXSpec_and_tblpYSpec_keeps_its_alignment_through_DOCX`,
`Round257TocSwitchTests`.
