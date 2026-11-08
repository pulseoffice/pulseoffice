# Deep audit round 74: text formats, second pass

## Coverage

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfDocumentWriter.cs | 526/526 | defect 1 fixed |
| src/PulseOffice.Formats.Interchange/Text/Rtf/RtfDocumentReader.cs | 120-350 (token dispatch, text, tables) | defect 1 fixed (reader side) |
| src/PulseOffice.Formats.Interchange/Text/Html/HtmlDocumentWriter.cs | 583/583 | clean: text and attributes HTML-encoded and XML-safe, links limited to `#`, http(s), mailto, ftp, file; images inline as `data:`; font names stripped of CSS-breaking characters |
| src/PulseOffice.Formats.Interchange/Text/Plain/PlainTextFormat.cs | 123/123 | clean: 256 MB cap, CR, LF, CRLF, FF, U+2028/2029/0085 split paragraphs, control characters dropped |
| src/PulseOffice.Sheets.UI/Dialogs/CsvImportPrompt.cs | 160/160 | clean: preview decodes only the 64 KB head read by the caller, twelve lines |
| src/PulseOffice.Formats.Interchange/CsvFormat.cs (writer) | 160-260 | formula guard added |

Not present in the code base: a Markdown reader or writer, and a Deck outline import from RTF or text. Nothing to
audit there; they are features, not defects. RTF `\uN` is written as a signed 16-bit value with a `?` fallback and
`\uc1`, surrogate pairs as two escapes, as other writers do.

## Defects

1. **Medium — font and style names containing `;` broke the RTF tables.** The writer wrote the name verbatim, so the
   `;` closed the entry early and the rest leaked; the reader, for its part, treated an escaped `\'3b` (and a `\uN`
   semicolon) as the terminator, and emitted pending hex bytes after the following plain text, reordering the name.
   The writer now escapes `;` as `\'3b` in the font table and stylesheet; the reader flushes pending bytes before plain
   text and treats escaped characters as literal.
2. **Decision — CSV formula injection.** `CsvOptions.GuardFormulas` (default off, as in the other office suites and
   Excel, because the apostrophe changes the data for anyone re-reading the file) prefixes `'` to text cells starting
   with `=`, `+`, `-`, `@`, tab or carriage return; with it on, the `ODF259` warning is not raised. There is no CSV
   export dialog yet: the writer is a singleton with fixed options, so exposing the switch in five languages needs an
   export-options prompt in the save path (queued).

## Tests

`tests/PulseOffice.Formats.Interchange.Tests/Round74TextFormatsTests.cs`: semicolon font name round trip (failed
before the fix), guarded and unguarded CSV export. Formats.Interchange.Tests 138/138.
