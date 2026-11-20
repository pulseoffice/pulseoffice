# Round 306: Deck table cell editing

Area: the Deck table queue items of round 302 and the join item of rounds 302-305.

## Defects

| Severity | Defect | Status |
|---|---|---|
| Serious | Editing a cell's text dropped all its rich runs (data loss in both formats after any edit) | Fixed: `TableCellText.Rebased` keeps the runs of the unchanged prefix and suffix; inserted text takes the format of the run before it |
| Medium | The header row of a grid table drew the plain column name, ignoring its runs | Fixed: rich header runs drawn through the rich cell path |
| Medium | Stacked cell text drew the plain value | Fixed: `TableCellText.Stacked` gives one paragraph per grapheme with its run format |
| Medium | Reading an ODP grid table lost its accent colour | Fixed: the accent is read back from our `table:template-name` (`OdfDeckGridTemplates.Accent`) |
| Minor | The cell editor used the default font family | Fixed: family, weight and slant of the cell look |
| Minor | No own-look PPTX round trip test asserting no invented rich text | Added `Round306PptxOwnLookRoundTripTests` |
| Medium | No character formatting for part of a cell's text; the editor does not share the shape text editor | Open, queued (306) |
| Medium | Paragraph alignment per cell, not per paragraph | Open, queued: needs a model field and both codecs (306) |

## Tests

`Round306TableCellTextRebaseTests`, `Round306OdpGridAccentTests`, `Round306PptxOwnLookRoundTripTests`;
`Round302TableCellWriteTests` updated to the new semantics (an edit keeps the runs).
