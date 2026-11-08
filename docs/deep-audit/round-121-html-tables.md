# Deep audit round 121: HTML tables and the HTML clipboard path

Date: 2026-11-08. Scope: the three limits left by round 119 in the HTML table reading, the symmetric writing, and the
HTML clipboard path of Folio, Deck and Ledger.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Formats.Interchange/Text/Html/HtmlTableReader.cs | 311/311 | nested tables, mixed formatting, stray end tag fixed |
| src/PulseOffice.Formats.Interchange/Text/Html/HtmlCellBorders.cs | 47/47 | rewritten: longhands, style, rgb() values |
| src/PulseOffice.Formats.Interchange/Text/Html/CssDeclarations.cs | 124/124 | no defect |
| src/PulseOffice.Formats.Interchange/Text/Html/HtmlDocumentReader.cs | 831/831 | border style carried; nested tables and runs already kept |
| src/PulseOffice.Formats.Interchange/Text/Html/HtmlDocumentWriter.cs | 643/643 | cell borders not written; global border on every cell |
| src/PulseOffice.Formats.Interchange/Text/Html/HtmlTokenizer.cs | 147/147 | no defect |
| src/PulseOffice.Formats.Interchange/HtmlFormat.cs | 190/190 | fragment did not write cell borders |
| src/PulseOffice.Formats.Interchange/Text/Assembly/TextDocumentAssembler.cs | table part (BeginTable … Close) | nested tables supported by depth |
| src/PulseOffice.Exchange/ExchangeModel.cs | 154/154 | no defect |
| src/PulseOffice.Exchange/CellGridConversions.cs | 115/115 | limit noted (no merges or borders in the grid) |
| src/PulseOffice.Exchange/ObjectExchange.cs | 150/150 | no defect |
| src/PulseOffice.Exchange/Clipboard.cs | 25/25 | no defect |
| src/PulseOffice.UI.Controls/Services/SystemClipboard.cs | 127/127 | no defect |
| src/PulseOffice.Shell/Exchange/HtmlTableGrids.cs | 47/47 | limit noted (number parsing by current culture) |
| src/PulseOffice.Documents.UI/Exchange/FolioExchange.cs | 101/101 | no defect |
| src/PulseOffice.Documents.UI/Editing/FolioRichTransfer.cs | 70/70 | no defect |
| src/PulseOffice.Presentations.UI/Exchange/DeckExchange.cs | 202/202 | no defect |
| src/PulseOffice.Presentations.UI/DeckRichTextTransfer.cs | 74/74 | text in sections and tables dropped |
| src/PulseOffice.Sheets.UI/Exchange/LedgerExchange.cs | 372/372 | border style ignored on paste |

## Decisions

- Folio keeps nested tables: `HtmlDocumentReader` already opened a table inside a cell (the assembler tracks table
  depth); tests now hold it, together with mixed bold/plain runs in a cell.
- Ledger and Deck read one grid through `HtmlTableReader`. A sheet range cannot hold a table inside a cell, so a
  nested table becomes text of the outer cell: each nested row on its own line, cells separated by a tab.
- A sheet cell has no runs, so a cell takes bold, italic, underline or colour only when that formatting covers all
  of its text; formatting that merely opens the cell no longer spreads to the whole cell.

## Defects

Medium
- Nested tables in `HtmlTableReader` glued their cells together ("ab" for cells a and b),
  HtmlTableReader.cs:88 (before). Fixed: tabs and line breaks.
- Inline formatting that only opened a cell was applied to the whole cell (`<b>Total</b> 5` pasted bold),
  HtmlTableReader.cs:165 (before). Fixed: formatting common to all written text.
- `border-width`, `border-style`, `border-color` and the side longhands were not read; `rgb(255, 0, 0)` with spaces
  in a border was split and lost; border style was never read, HtmlCellBorders.cs:37 (before). Fixed.
- The Folio HTML writer did not write cell borders (lost on copy to other apps and on reopening) and its page style
  drew a grey border on every cell of borderless tables, HtmlDocumentWriter.cs:206 and :264. Fixed: per-edge
  `border-<side>`, no global border.
- Ledger's HTML fragment did not write cell borders, HtmlFormat.cs:155. Fixed; Ledger's paste maps dashed, dotted
  and double back to the sheet border styles (LedgerExchange.cs Edge).
- Deck's formatted-text paste dropped the text of HTML sections and tables, DeckRichTextTransfer.cs:26. Fixed.

Minor
- A stray `</table>` before the table drove the depth negative and hid the table, HtmlTableReader.cs:77 (before).
  Fixed.

## Remaining limits

- `CellGridConversions.Html` (Ledger range pasted into Folio as a table) carries neither merges nor borders, because
  `ExchangeCell` has none.
- `HtmlTableGrids` parses numbers with the current culture rather than the spreadsheet locale.
- Cell padding is not written by the Folio HTML writer (the reader gives a default padding).

## Tests

`tests/PulseOffice.Formats.Interchange.Tests/Text/Round121HtmlTableTests.cs` (14 tests: ten failed before the
fixes; the nested-table and mixed-run Folio tests and the writer nested-table test passed already and now guard the
behaviour; the stray end tag test was added with the fix after checking the old code decremented unconditionally),
`tests/PulseOffice.Presentations.UI.Tests/DeckRichTextTransferRound121Tests.cs` (failed before the fix).
Runs: Formats.Interchange.Tests 172/172, Robustness.Tests 70/70, Architecture.Tests 43/43, Sheets.UI.Tests 482/482,
Presentations.UI.Tests 108/108.

## Live checks

- Copy a web page table with nested tables and mixed bold text, paste in Folio: nested table inside the cell, bold
  only on the bold words; paste in Ledger: nested rows as lines in one cell, no bold on the whole cell.
- Copy an HTML table with `border-style:dashed`/`border-color` from a browser into Folio and Ledger.
- Copy a bordered Folio table and a bordered Ledger range into a browser editor or mail: borders visible; a
  borderless Folio table shows no grid.
