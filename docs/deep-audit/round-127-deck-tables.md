# Round 127: Deck tables

Scope: the two leftovers of round 124 (Deck table cells without italic, underline, strike and font family; RTF paragraph
borders in Folio), then the Deck table code: model, commands, table editor, rendering, ODP and PPTX read and write,
exchange.

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Document/Embedding/DataTableObject.cs | all | `TableCellLook` had no italic, underline, strike or font family |
| src/PulseOffice.Document/Embedding/TableStructure.cs | all | inserted rows and columns had no look |
| src/PulseOffice.Document/Embedding/TableSizing.cs | all | hit testing used equal widths, drawing and files another rule |
| src/PulseOffice.Presentations.UI/DeckTableCommands.cs | all | no defect of its own (structure defects are in TableStructure) |
| src/PulseOffice.Presentations.UI/DeckTableLooks.cs | all | no defect |
| src/PulseOffice.Presentations.UI/DeckTableBorders.cs | all | quadratic format lookups; seeding moved to `DeckTableLookSeed` |
| src/PulseOffice.Presentations.UI/DeckFormatCommands.cs | all | font commands did nothing on a selected table |
| src/PulseOffice.Presentations.UI/DeckRibbonCommands.cs | font, size, colour, helpers | same |
| src/PulseOffice.Presentations.UI/DeckCommandSet.cs | all | shared table-cell wrapper added |
| src/PulseOffice.Presentations.UI/ActiveDeckContext.cs | all | no defect |
| src/PulseOffice.Presentations.UI/DeckElementActions.cs | table part (EditTable, ReplaceTable) | no defect |
| src/PulseOffice.Presentations.UI/DeckInsertCommands.cs | table part (TableAsync, InsertContent) | no defect |
| src/PulseOffice.Presentations.UI/SlideView.cs | table picking and cell selection drawing | wrong cell picked (see TableSizing) |
| src/PulseOffice.Data.UI/DataTableRenderer.cs | all | new properties not drawn; quadratic merge and format lookups per frame |
| src/PulseOffice.Data.UI/TableEditModel.cs | all | no defect |
| src/PulseOffice.Data.UI/TableEditorView.cs | constructor, toolbar, merge/split, trim, capture, build | added rows and columns had no look |
| src/PulseOffice.Formats.Odf/Shapes/DeckObjectCodecs.cs | DeckTableCodec | header vertical alignment lost; quadratic writer |
| src/PulseOffice.Formats.Odf/Shapes/OdfTableCellLooks.cs | all | new properties |
| src/PulseOffice.Formats.OpenXml/PptxDeckTableMapper.cs | all | new properties; index moved to the model |
| src/PulseOffice.Formats.OpenXml/Presentation/Reading/PptxTableStyles.cs | all | run formatting of style-less tables dropped |
| src/PulseOffice.Exchange/CellGridConversions.cs | Table, Grid, Described, Styled, Formats, Look | new properties both ways |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfDocumentReader.cs | control-word switch, cells, paragraph shape | paragraph borders ignored |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfDocumentWriter.cs | cell box, paragraph, paragraph controls | paragraph borders not written |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfCellDefinitions.cs | all | line-style table shared with paragraphs |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfState.cs | all | paragraph borders state |

## Changes

- `TableCellLook` gains `Italic`, `Underline`, `Strikethrough` and `FontFamily`. They are drawn
  (`ITableCellTextStyles`: typeface style, weight and family, underline and strike decorations), written and read in ODP
  (`fo:font-style`, `style:text-underline-style`, `style:text-line-through-style`, `fo:font-family`, `style:font-name` on
  read, family quoted when it has spaces, at most 256 characters) and PPTX (`a:rPr` `i`, `u="sng"`,
  `strike="sngStrike"`, `a:latin`; theme fonts `+mn-lt` are not taken as a family), and carried by the exchange grid
  both ways.
- The font group works on a selected Deck table: bold, italic, underline, strike, font family, size, text colour, the
  three colour shortcuts and left, centre and right alignment apply to the selected cells, or to the whole table when no
  cell is selected, in one undo step; the buttons show the state of the first target cell. `IDeckTableCellEditing`
  (availability, look, apply, toggle) is shared by `DeckFormatCommands` and `DeckRibbonCommands` through
  `DeckCommandSet.InTableCells`; `IDeckTableCellFormatting` edits the looks; `IDeckTableLookSeed` turns a table with the
  generic look into explicit cell looks before the first edit (also used by `DeckTableBorders`, which no longer has its
  own copy). Number columns of a seeded generic table keep their right alignment.
- `TableCellIndex` (formats, merge origins and covered cells by position) moved from the PPTX mapper to
  `PulseOffice.Document.Embedding` and is used by the renderer, the ODP writer, the PPTX writer, borders and seeding.
- Inserted rows and columns (ribbon commands and the table editor) copy the formats of their neighbour: the row above
  (the first body row for an insertion at the top, never the header), the column on the left (the first column for an
  insertion at the left).
- Without saved column widths, drawing, hit testing, the cell selection overlay and the ODP and PPTX writers all use
  the optimal widths (`OptimalColumnSizing`).
- RTF paragraph borders: `\brdrt`, `\brdrb`, `\brdrl`, `\brdrr`, `\box` with the line style words, `\brdrw`, `\brdrcf`,
  `\brsp` (padding) and `\brdrnone` are read into `ParagraphProperties.Borders` and written back; `\pard` clears them.
  Words after `\trowd`, `\clbrdr*` or `\trbrdr*` belong to the table, as before. `RtfBorderLineStyles` is the single
  table of line style words for cells and paragraphs.

## Defects

Grave
- The font group did nothing on a selected Deck table: every font command was enabled only for a text target
  (DeckFormatCommands.cs:110, DeckRibbonCommands.cs:220 before). Fixed.
- Italic, underline, strike and font family of table cells were lost in ODP, PPTX, rendering and paste
  (DataTableObject.cs `TableCellLook`, OdfTableCellLooks.cs, PptxDeckTableMapper.cs:128, PptxTableStyles.cs:178,
  CellGridConversions.cs:139 and 188 before). Fixed.

Medium
- The vertical alignment of header cells was not read back from ODP: header formats were always Top
  (DeckObjectCodecs.cs:579 before). Every table inserted in Deck has centred header cells. Fixed.
- A PPTX table without a table style and without cell properties lost the formatting of its runs (bold, size, colour):
  the look was read only when a style or cell properties existed (PptxTableStyles.cs:68 before). Fixed: formatted runs
  count as an own look.
- Rows and columns inserted into a table with its own look had no fill, borders or text format, leaving holes in the
  grid (TableStructure.cs:31 and 79, TableEditorView.cs:405 and 415 before). Fixed.
- Clicking a cell of a table without saved widths selected the wrong column, and the selection rectangle was drawn
  elsewhere: hit testing used equal widths, drawing used the character-count widths of the renderer, and the files were
  written with a third rule (TableSizing.cs:174, DataTableRenderer.cs:53 and 158 before). Fixed: one rule.
- Quadratic work on large tables: the ODP writer looked up merges and formats by scanning all of them for every cell
  (DeckObjectCodecs.cs:250, 259, 237, 273 before), the renderer did the same on every frame and did not stop at the
  bottom of the frame for own-look tables (DataTableRenderer.cs:102, 104, 116, 173, 174, 180 before), and the border
  command searched the format list for every cell (DeckTableBorders.cs:79 and 108 before). Fixed with `TableCellIndex`
  and position maps.
- RTF paragraph borders were neither read nor written (round 124 leftover). Fixed.

## Remaining limits

- A Deck table cell still holds plain text with one look per cell, not rich text runs: formatting part of a cell's text
  is not possible, and a PPTX or ODP cell with mixed runs takes the first run's format.
- A table whose own look is plain in every cell is read back from ODP as a table with the generic look
  (`OdfTableCellLooks.Read` returns nothing for a plain look).
- RTF paragraph padding is written only for sides with a line (`\brsp` belongs to a border); paragraph shading
  (`\cbpat`) is still not read or written.
- With no saved widths, Ledger and Folio data tables now also use the optimal widths, which have no cap on very long
  texts.
- No new declared-loss code: nothing new is lost in either format.

## Tests

- tests/PulseOffice.Formats.OpenXml.Tests/Round127DeckTableTests.cs: new look properties through PPTX, ODP and both
  orders; header vertical alignment through ODP; run formatting of a style-less PPTX table.
- tests/PulseOffice.Exchange.Tests/Round127DeckTableExchangeTests.cs: grid to table and table to grid.
- tests/PulseOffice.Presentations.UI.Tests/Round127DeckTableCellTests.cs: italic on selected cells with undo, bold
  toggle on a generic table, underline/strike/colour/alignment on a whole table, typeface and decorations, editor
  rows and columns.
- tests/PulseOffice.Presentations.Tests/Round127TableStructureTests.cs: inserted rows and columns, hit testing widths.
- tests/PulseOffice.Formats.Interchange.Tests/Text/Round127RtfParagraphBorderTests.cs: paragraph borders read, box,
  pard, round trip, cell borders after a paragraph border.

## To try live

- Insert a Deck table, select a few cells, press Ctrl+I, Ctrl+U, strike, pick Georgia and 20 pt, red text, centre: the
  cells change at once; Ctrl+Z undoes each step; the ribbon buttons light up on those cells.
- Save as ODP and PPTX, reopen in PulseOffice, the reference suite and PowerPoint: the formatting is there, and the
  header row stays vertically centred.
- Add a row and a column to a styled table (ribbon and table editor): they look like their neighbours.
- Type a long text in the first column of a new table and click the second column: the right cell is selected.
- Copy a Ledger range with italic, underlined and Georgia cells into Deck, and a Deck table into Ledger.
- Paste from Word into Folio a paragraph with a top and bottom border, and copy a bordered Folio paragraph into Word.
