# Round 152: plain-text shortcuts in the codecs (2026-11-16)

Scope: the places, outside Deck notes and PPTX comments (round 151), where a codec writes a string as raw `text:p` or
`a:t` text, or reads it back with the element value or by concatenating `a:t`, losing line breaks, tabs and repeated
spaces. Ledger validation and conditional formats were left out (audited in parallel).

## Defects (all medium, fixed; each test failed on the old code before the fix)
- **Chart and axis titles, ODS/ODP/ODT** (`OdfChartDocumentBuilder`, `OdfChartDocumentReader`): written as one raw
  `text:p`, read from the first paragraph's value. A two-line title from another suite lost its second line; tabs and
  repeated spaces were collapsed by other readers. Now the shared `IOdfParagraphText` both ways. Test:
  `Round152ChartTitleTests` (Odf).
- **Chart and axis titles, XLSX/PPTX/DOCX charts** (`ChartSpaceBuilder`, `ChartSpaceReader`): one `a:p` with a raw
  newline, read by concatenating every `a:t`, so the paragraphs of an Excel title were merged. Now one `a:p` per line
  and `IDrawingPlainText.Body` on read. Test: `Round152ChartTitleTests` (OpenXml).
- **ODS tracked changes**: the previous cell text (`OdfCellValueCodec`, shared with scenarios) was written raw and read
  by concatenating paragraphs (a multi-line previous value lost its breaks); the change comment kept only its first
  paragraph. Test: `Round152TrackedChangeTextTests`.
- **ODP notes and handout master header/footer** (`OdfPrintMasters`): written raw, read by value (tabs and repeated
  spaces lost to other readers, `text:s`/`text:tab` from other suites dropped). Test: `Round152PrintMasterTextTests`.

## Seen, left as they are
- Fontwork text (ODF and OOXML) joins paragraphs with a space by design (the model is single-line); `text:s` in a
  fontwork shape from another suite is still collapsed (low, open).
- Equation shapes (`DeckObjectCodecs`, ODF and OOXML) read the formula source from text that is never multi-line.
- `OdtReader` tracked-change note and `OdtWatermarks` text still read paragraph values (low, open: single-line in
  practice).
- Inline chart data cells (`ChartDocuments` table cells) are written raw (category names; low, open).

## Not verified
Not cross-checked in the reference suite, OnlyOffice or Excel.
