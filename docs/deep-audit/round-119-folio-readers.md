# Round 119: Folio HTML and RTF readers

Continuation of round 115 (Folio RTF/HTML/EPUB/text interchange). Area: `src/PulseOffice.Formats.Interchange/Text`.

## Coverage

| File | Lines read / total | Outcome |
|---|---|---|
| Text/Rtf/RtfDocumentReader.cs | 820-986 / 986 (the rest in round 115), plus 440-470 and 695-730 again | no new defect; `\outlinelevel` clamped, picture cap checked |
| Text/Html/HtmlDocumentReader.cs | 560-699 / 699 (the rest in round 115), plus 225-256 and 505-560 again | two minor fixed |
| Text/Html/HtmlTableReader.cs | 305 / 305 | medium fixed |
| Text/Html/HtmlCellBorders.cs | 46 / 46 | minor fixed |
| Text/Rtf/RtfPicture.cs | 68 / 68 | minor fixed (open defect of round 115) |
| Text/Html/HtmlDocumentWriter.cs | 140-190, 276-320, 360-380, 460-510 | two open defects of round 115 fixed |
| Sheets.UI/Exchange/LedgerExchange.cs | 225-270 (consumer of `HtmlTable`) | confirmed the effect of the table defect |

## Defects

Medium
- `HtmlTableReader.cs` `Build` (was line 234): a `<tr>` whose cells are all covered by row spans from above was
  dropped as empty, so every later row moved up and the spans covered the wrong rows; trailing positions covered from
  above were not padded with placeholders; a `rowspan` larger than the remaining rows was kept, and
  `LedgerExchange.FromTable` (line 262) built merges beyond the pasted block. Now covered positions are padded, only
  truly empty rows are dropped and row spans are cut to the rows below.
- `HtmlDocumentWriter.cs` `Paragraph`: list-header paragraphs (`ListHeader`) were written as ordinary `<li>` and
  numbered. Now `data-list-header="true"` with `list-style-type:none;counter-increment:list-item 0`; the HTML reader
  reads the attribute back (`ParagraphShape.ListHeader`, applied by `TextDocumentAssembler`).

Minor
- `HtmlDocumentWriter.cs` heading ids (`h1`, `h2`, ...) could equal a bookmark name, giving two elements with the
  same id and EPUB navigation pointing at the bookmark. Ids now skip every bookmark name of body, tables and notes.
- `RtfPicture.cs:65`: `(measured ?? declared) * 15` overflowed in `int` before widening; a huge `\picw` gave the
  minimum size. Now computed in `long`.
- `HtmlCellBorders.cs:23`: `<table border>` (attribute without value, bordered in HTML) was read as borderless, and
  the number was parsed with the current culture. Now an empty value counts as bordered; invariant culture.
- `HtmlDocumentReader.cs` `Character`: `data-field="9999"` gave an undefined `TextFieldKind` (enum parse accepts
  numbers). Now only defined names are accepted.
- `HtmlDocumentReader.cs` `FontTagSize`: `<font size="+1">` was read as size 1 (7.5 pt). Relative sizes are now
  taken from base size 3.

Remaining (not fixed): nested tables in the HTML table reader are flattened into the outer cell text; inline
formatting inside a cell is kept only when it starts the cell; `border-width`, `border-style` and `border-color`
longhands are not read.

## Tests

`tests/PulseOffice.Formats.Interchange.Tests/Text/Round119ReaderTests.cs`: nine tests, all failing before the fixes.
Runs: Formats.Interchange.Tests 147/147, Robustness.Tests 70/70, Architecture.Tests 43/43.

## Live checks

Paste from a browser into Ledger a table with a row made only of spanned cells and with an oversized `rowspan`;
export a Folio document with a list header to HTML and open it in a browser (no number on the header); export to
EPUB a document with a bookmark named `h1` and follow the table of contents.
