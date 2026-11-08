# Round 128: Deck tables, second pass

Scope: the leftovers of round 127 (rich text in Deck table cells, plain own look through ODP, RTF paragraph padding and
shading, uncapped optimal widths) and a full reading of the table editor.

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Data.UI/TableEditorView.cs | all (531 lines before, 562 after) | brushes resolved while detached; header merges ignored; quadratic merge lookups |
| src/PulseOffice.Document/Embedding/DataTableObject.cs | all | decision on rich cells (below) |
| src/PulseOffice.Document/Embedding/TableSizing.cs | all | optimal and minimal widths without cap, whole multi-line text measured |
| src/PulseOffice.Formats.Odf/Shapes/OdfTableCellLooks.cs | all | a plain own look was read as no look |
| src/PulseOffice.Formats.Odf/Shapes/DeckObjectCodecs.cs | DeckTableCodec read and write | no new defect |
| src/PulseOffice.Formats.OpenXml/Presentation/Reading/PptxTableStyles.cs | HasLook, Look, Direct | first run's format taken silently |
| src/PulseOffice.Formats.OpenXml/Presentation/Reading/PptxReadLosses.cs | all | new loss |
| src/PulseOffice.Formats.Odf/OdpReader.cs | read losses | new loss |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfDocumentReader.cs | paragraph words, cells, paragraph shape | padding dropped without a line, `\cbpat` ignored |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfDocumentWriter.cs | cell box, paragraph box, paragraph controls | padding written only with a line, no shading |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfParagraphBorders.cs | all | `HasSpace` added |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfState.cs | all | `Shading` was declared and never used |

## Decision: Deck table cells stay one look per cell

`DataTableObject` is one object shared by Ledger, Folio and Deck. Its cells are typed values (number, date, boolean,
text) that a query can refresh, and its looks are per cell (`TableCellFormat`). Rich text runs would turn the value into
text, break refresh, sorting and the number formats, and every consumer (renderer, exchange grid, Ledger and Folio data
tables, ODS and XLSX paths) would have to carry runs it cannot use. Folio already has its own rich tables for running
text. Making Deck cells rich would therefore need a different object kind, not a patch on this one, and the benefit
(formatting part of a cell's text) is small for slide tables. So the model is kept, and the loss is now declared instead
of silent:

- PPTX225 (info, on read): a table cell has runs with different formatting; it takes the first run's format.
- ODF281 (info, on read): a table cell formats part of its text with a text span style; that formatting is lost.

Both have `diagnostic.pptx225` and `diagnostic.odf281` in the five localization files. Files written by Deck never raise
them (each cell has one look).

## Changes

- ODP: a cell style that carries graphic, paragraph or text properties is an own look even when it is plain, so a table
  with its own look that is plain in every cell keeps `OwnLook` (and no grid) after an ODP round trip. A style with only
  `style:table-cell-properties` (vertical alignment of a generic table) is still not a look.
- RTF: paragraph inner spacing is read and written on every side, also without a line (`\brdrX\brdrnone\brspN`);
  paragraph shading `\cbpatN` is read into `ParagraphProperties.Background` and written back; `\pard` clears it; cell
  shading `\clcbpat` stays on the cell.
- Optimal and minimal column widths measure the widest line of a cell (not the whole multi-line text) and are capped at
  40 characters, so one very long text no longer squeezes the other columns of Ledger, Folio and Deck data tables.
- Table editor: brushes and the grid are rebuilt when the view is attached to the window and when the theme variant
  changes (before, theme brushes were looked up while detached, so cells used white and grey fallbacks, wrong in the
  dark theme); header merges span the header fields and hide the covered ones (before, every header field was shown
  and editable under a merge); merge origins and covered cells are indexed once per rebuild instead of scanning all
  merges for every cell; header field text is captured by column, so a hidden header field no longer shifts the others.

## Defects

Grave
- Table editor in the dark theme: cell backgrounds and header text used the white and grey fallbacks because theme
  resources were looked up before the view was in a window (TableEditorView.cs:113 and 122-126 before). Fixed.

Medium
- Mixed run formatting in PPTX and ODP table cells was dropped silently (PptxTableStyles.cs:198, DeckObjectCodecs.cs:574).
  Declared (PPTX225, ODF281).
- A table with a plain own look came back from ODP with the generic look and grid (OdfTableCellLooks.cs:56 before). Fixed.
- RTF paragraph padding without a line was lost both ways, and paragraph shading `\cbpat` was ignored
  (RtfDocumentReader.cs:835-838 and 965, RtfDocumentWriter.cs:276-282 and 465 before). Fixed.
- Header merges were not shown in the table editor, and the covered header fields stayed editable
  (TableEditorView.cs:160-165 before). Fixed.
- Optimal widths had no cap and measured multi-line texts as one line (TableSizing.cs:47 and 60 before). Fixed.

Minor
- The table editor looked up merges by scanning the whole list for every cell on every rebuild (TableEditorView.cs:349
  and 351 before). Fixed.

## Remaining limits

- The table editor builds one text box per cell with no virtualisation: a very large imported table (up to the 100,000
  cell import limit) is slow to open in it.
- The editor's text boxes are single-line: a cell with several paragraphs shows them on one line in the editor.
- The editor has no header selection, so a header merge read from a file cannot be split there.
- The editor's banding ignores the table's banded-rows setting.

## Tests

- tests/PulseOffice.Formats.OpenXml.Tests/Round128DeckTableTests.cs: plain own look through ODP, PPTX and both; generic
  table with a centred cell stays generic; PPTX225 present and absent; ODF281 present and absent.
- tests/PulseOffice.Formats.Interchange.Tests/Text/Round128RtfParagraphBoxTests.cs: padding without a line, `\cbpat`
  read, shading round trip, cell shading not taken as paragraph shading.
- tests/PulseOffice.Presentations.Tests/Round128TableWidthTests.cs: cap and widest line.
- tests/PulseOffice.Presentations.UI.Tests/Round128TableEditorTests.cs: theme surface after attaching, header merge.

## To try live

- Open the table editor of a Deck table in the dark theme: cells are dark, header letters readable; switch theme while
  it is open.
- Open a PPTX whose table has a cell with a bold word inside plain text: the info about PPTX225 appears.
- Save a Deck table with its own plain look as ODP and reopen it: no grid appears.
- Copy a Folio paragraph with shading and a top border with padding into Word, and back.
- Insert a data table with one very long text in a column: the other columns stay readable.
