# Deep audit round 124: exchange grids, RTF and plain-text tables

Date: 2026-11-08. Scope: the three limits left by round 121 in cross-module exchange (decision 10), then the RTF and
plain-text representations of the exchange package for sheet ranges and tables.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Exchange/ExchangeModel.cs | all | cell model extended (see Decisions) |
| src/PulseOffice.Exchange/CellGridConversions.cs | all | merges, borders, padding, fonts, alignment in HTML and tables; table back to grid |
| src/PulseOffice.Exchange/ExchangeContentGuard.cs | all | merges and new cell members admitted with bounds |
| src/PulseOffice.Exchange/ExchangeSerializer.cs | all | no defect (records serialise the new members) |
| src/PulseOffice.Exchange/ExchangeScalarConverter.cs | 1-40 | not touched |
| src/PulseOffice.Shell/Exchange/HtmlTableGrids.cs | all | rewritten: spreadsheet locale, spans, borders, alignment, caption |
| src/PulseOffice.Sheets.UI/Exchange/SheetRangeCapture.cs | all | merges and full style captured; general numbers truncated |
| src/PulseOffice.Sheets.UI/Exchange/LedgerExchange.cs | all | grid paste moved to `ExchangeGridSnapshots`; plain text from displayed values |
| src/PulseOffice.Documents.UI/Exchange/FolioExchange.cs | all | no defect |
| src/PulseOffice.Presentations.UI/Exchange/DeckExchange.cs | all | a copied Deck table had no plain text and no HTML |
| src/PulseOffice.Presentations.UI/Exchange/DeckSelectionPackages.cs | all | no defect |
| src/PulseOffice.Document/Embedding/DataTableObject.cs | all | no defect |
| src/PulseOffice.Formats.Interchange/Text/Html/HtmlDocumentReader.cs | cell part (Cell, CellParagraph, CellRun, CellBorders) | padding ignored, cell font ignored |
| src/PulseOffice.Formats.Interchange/Text/Html/HtmlDocumentWriter.cs | table part | padding not written |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfDocumentWriter.cs | 1-260 and helpers | fixed borders on every cell, no padding, quadratic origin search |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfDocumentReader.cs | table part (control words, OpenCell, Cell, Row, CloseTable, PendingCell) | cell borders and padding ignored |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfState.cs | all | definition moved to RtfCellDefinitions.cs |
| src/PulseOffice.Sheets.Engine/Operations/SheetClipboard.cs | TabularText (434-560) and Copy merges | raw invariant values (no longer used for the clipboard text) |
| src/PulseOffice.Sheets/Styling/CellStyle.cs | all | no defect |

## Decisions

- `ExchangeCell` gains `Strikethrough`, `FontFamily`, `FontSizePoints`, `Horizontal` (General, Left, Center, Right,
  Justify), `Vertical` (Default, Top, Middle, Bottom), `Wrap` and `Borders` (four `ExchangeBorderLine` with width in
  points, `#RRGGBB` colour and Solid/Dashed/Dotted/Double style). `AlignRight` is gone: numbers in General alignment are
  right-aligned by the writers that need it. `CellRangeContent.Merges` lists `ExchangeMerge` regions relative to the
  grid. Font family and size are carried only when they differ from the sheet default, like the colour.
- The guard refuses merges outside the grid, with empty spans or overlapping each other; it drops non-finite font sizes
  and border widths, caps borders at 12 pt and fonts at 409 pt, drops families longer than 256 characters or with
  control characters, and drops border lines with an invalid colour.
- Ledger maps cell style to and from the exchange cell in one injectable service, `ExchangeCellStyles`; a grid from
  another process becomes a clipboard snapshot in `ExchangeGridSnapshots` (merges included), so the mapping is
  symmetric and testable.
- Deck: a grid becomes a `DataTableObject` with its merges (header row convention, row -1) and, when any cell is
  formatted, `OwnLook` with one `TableCellFormat` per cell (fill, text colour, bold, size, horizontal alignment, four
  borders, vertical alignment). `ICellGridConversions.Grid(DataTableObject, CultureInfo)` turns a table back into a
  grid, which gives a copied Deck table its plain text and HTML.
- HTML from a grid writes `colspan`/`rowspan` (covered cells skipped), per-edge borders, `padding:1.5pt 2.5pt`,
  alignment, vertical alignment, strike, font family (quotes and markup characters stripped) and size.
- RTF cell padding follows Word: `\clpadl` is the top padding and `\clpadt` the left one, in both directions; values are
  read only in twips (`\clpadf*3` or no unit word). Without padding words the horizontal padding is `\trgaph`.
- The plain text of a Ledger range is the displayed text of the captured grid (formats, dates and the interface locale
  applied), tab separated and quoted as before, instead of the raw invariant values.

## Defects

Medium
- A Ledger range pasted into Folio or Deck lost merges and borders; Ledger to Ledger across processes lost merges,
  borders, alignment, wrap, font family, size and strike (CellGridConversions.cs Html/Table, SheetRangeCapture.cs:97,
  LedgerExchange.cs FromGrid before). Fixed.
- `HtmlTableGrids` parsed numbers with the current culture, then the invariant one (HtmlTableGrids.cs:31 before), so
  Deck tables pasted from HTML read numbers against decision 17. Fixed: spreadsheet locale only.
- `HtmlTableGrids` dropped spans, borders and alignment, and an empty cell lost its fill and borders. Fixed.
- The RTF writer drew a 0.5 pt solid border on every edge of every cell, whatever the table had
  (RtfDocumentWriter.cs:214 before); the RTF reader ignored `\clbrdr*`, `\brdr*` and `\clpad*`, so tables pasted from
  RTF arrived without borders. Fixed both ways, with styles, widths, colours and padding.
- The Folio HTML writer did not write cell padding and the reader ignored `padding`, giving every bordered cell a fixed
  padding (HtmlDocumentReader.cs CellBorders before). Fixed, longhands included, padding without borders kept.
- The plain text of a Ledger range wrote raw invariant values: dates as serial numbers, no currency or percent, a
  decimal point in every locale (LedgerExchange.cs:125 before, TabularText.Write). Fixed.
- A copied Deck table produced neither plain text nor HTML for other applications (DeckExchange.cs Text). Fixed.

Minor
- The captured grid rendered General numbers for 12 characters, truncating `1234567.891234` to `1234567.8912` in Folio
  and Deck tables and in the plain text (SheetRangeCapture.cs:22 before). Fixed: 15 characters, the precision limit.
- Folio's HTML reader ignored `font-family` and `font-size` on a table cell. Fixed.
- `HtmlTableGrids` dropped the table caption, which Ledger keeps as a first row. Fixed: first row merged across.
- The RTF writer looked up the origin of each merged cell by scanning backwards over all earlier cells, quadratic on
  large tables (RtfDocumentWriter.cs:244 before). Fixed: one pass map.

## Remaining limits

- A Deck table cell has no italic, underline, strike or font family (`TableCellLook`), so those stay in the grid but
  are not shown in a Deck table; wrap has no meaning in Folio and Deck tables.
- Paragraph borders (`\brdrt` and friends outside table definitions) are not read or written in RTF; this round covers
  cell borders only.
- Ledger's text paste (`TabularText.Read`) recognises plain numbers only (no thousands separators, dates or percent);
  displayed text with a format becomes text when pasted back into Ledger as unformatted text. Native, grid and HTML
  paths are not affected.
- No new declared-loss code was needed: every change concerns clipboard representations, not file formats.

## Tests

- `tests/PulseOffice.Exchange.Tests/Round124ExchangeGridTests.cs` (16 tests; 11 failed before the fixes, the JSON round
  trip passed and now guards the new members). `DeepAuditRoundEightyEightExchangeMatrixTests` now gives the populated
  sample valid border colours, which the guard requires.
- `tests/PulseOffice.Shell.Tests/Round124HtmlTableGridsTests.cs` (3 tests, all failed before).
- `tests/PulseOffice.Formats.Interchange.Tests/Text/Round124HtmlCellTests.cs` (4 tests, all failed before).
- `tests/PulseOffice.Formats.Interchange.Tests/Text/Round124RtfTableTests.cs` (5 tests, 4 failed before; the merge
  round trip passed and guards the new origin map).
- `tests/PulseOffice.Sheets.UI.Tests/Round124ExchangeGridTests.cs` (4 tests; the precision test failed before; the
  other three are written against the new `ExchangeCellStyles` and `ExchangeGridSnapshots` services, whose behaviour
  did not exist before).

Runs: Exchange.Tests 80/80, Formats.Interchange.Tests 181/181, Shell.Tests 225/225, Sheets.UI.Tests 489/489,
Presentations.UI.Tests 110/110, Documents.UI.Tests 370/370, Robustness.Tests 70/70, Architecture.Tests 43/43, Desktop.Tests (Exchange and Composition filter) 142/142.

## Live checks

- Copy a Ledger range with a merged title, dashed borders, centred text, Georgia 14 and wrap; paste into Folio (table
  with spans, borders and padding), into Deck (table with spans and the cell looks) and into a second PulseOffice
  instance's Ledger (everything back).
- Copy a Ledger range with dates and currency and paste into a plain text editor: displayed values, interface locale.
- Copy a Deck table and paste into a text editor and a mail composer: tab text and an HTML table.
- Copy a bordered table from Word or the reference suite as RTF into Folio: borders and padding kept; copy a Folio
  borderless table into Word: no borders.
- With the interface in Italian on an English system, paste an HTML table with `1,5` into Deck: number 1.5.
