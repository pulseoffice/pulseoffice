# Deep audit round 73: plain-text and interchange formats

## Coverage

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Formats.Interchange/CsvFormat.cs | 260/260 | defects 1, 3 fixed |
| src/PulseOffice.Formats.Interchange/CsvValues.cs | 87/87 | defect 2 fixed |
| src/PulseOffice.Formats.Interchange/CsvSniffer.cs | 82/82 | clean |
| src/PulseOffice.Formats.Interchange/CsvOptions.cs | 27/27 | clean |
| src/PulseOffice.Formats.Interchange/CsvNumbers.cs | 52/52 | clean |
| src/PulseOffice.Foundation/Text/DelimitedText.cs | 238/238 | defect 1 fixed |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfTokenizer.cs | 126/126 | clean: word length and `\bin` capped |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfState.cs | 103/103 | clean |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfDocumentReader.cs | targeted (depth, `\bin`, destinations) | clean: group depth capped at 1024 |
| src/PulseOffice.Formats.Interchange/Text/Html/HtmlDocumentReader.cs | targeted (links, images) | clean: only `data:` images, external sources never fetched |

Not reached in this round: Markdown reader/writer, HTML table reader, RTF writer, plain-text format, Deck outline
import, the CSV import dialog.

## Defects

1. **Medium — CSV import loaded the whole file in memory twice.** `DelimitedText.Read` called `ReadToEnd`, and the
   auto-detecting reader copied the stream into a `MemoryStream`, then into a string. The scanner now reads through a
   growing look-ahead window over the `TextReader` (`LookaheadReader`); a seekable stream is sniffed on a 32 KB probe
   and then decoded as it streams, with a strict UTF-8 decoder: if a byte later in the file is not UTF-8, the reader
   rewinds and falls back to the whole-file code-page detection. Non-seekable streams keep the buffered path.
2. **Medium — date patterns rebuilt for every cell.** `CsvValueReader.Moment` allocated the pattern arrays on every
   field; they are now built once per culture, and fields without digits skip the date parse.
3. **Minor — the reader closed the caller's stream** when automatic detection was off (`StreamReader` without
   `leaveOpen`).

Formula injection on export: already declared by the `CheckFidelity` warning for text cells starting with `=`, `+`,
`-`, `@`, tab or carriage return; no change.

## Tests

`tests/PulseOffice.Formats.Interchange.Tests/CsvStreamingTests.cs`: rows read from an endless reader (hung before the
fix), late non-UTF-8 tail decoded in its code page, caller stream left open. Formats.Interchange.Tests 135/135,
Sheets.Engine delimited-text tests 55/55.
