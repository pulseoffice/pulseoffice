# Round 255: robustness and security of the readers

Area: every file reader against malformed, truncated, hostile and huge input.

## What was already in place (verified, not changed)
- `DocumentIntake` and `IncomingDocumentInspector` gate every registry read: file size (1 GB), entry count (100 000),
  total and per-entry expanded size, compression ratio, XML element depth (256), DTDs and entities refused (FMT010-015).
- `OdfPackage` bounds every entry it opens; XML part loaders prohibit or ignore DTDs with no resolver.
- RTF: group depth 1024, word and binary caps, clamped parameters. HTML: attribute, inline-depth and cell budgets,
  spans clamped. CSV: rows and columns clipped to the sheet. GIF: pixel and frame budgets. Formula parser depth
  budget; evaluator depth; ODF style parent chains bounded and cycle-checked in every codec; repeated and spanned
  counts clamped.

## Defects found
- Medium: style inheritance cycles in ODT and DOCX (`parent-style-name`, `w:basedOn`, `pt:parent-name` on page
  styles) entered the model unchanged. Resolution was bounded, but cyclic styles vanished from the Styles panel tree
  (no root) and were written back cyclic. Fixed: `StyleSheet.BreakInheritanceCycles` cuts the closing link of each
  cycle after reading, in both readers.
- Medium: SVG `<use>` references were passed to the renderer unchecked: a few kilobytes of nested uses expand to
  hundreds of millions of drawn elements (render hang and memory growth), and cyclic or self-containing uses were left
  to the renderer. Fixed in `SvgSanitizer`: cyclic, self-containing and over-deep (32) uses are removed, and when the
  whole document would expand past one million elements every use is removed.
- Minor (queued): XLSX date-group and dynamic month filters scan every row of the filter range, which for a
  whole-column range is a million lookups at load; they could be clipped to the used range.

## Tests
`StyleCycleRound255Tests` (Documents.Tests), `SvgReferenceLimitRound255Tests` (Formats.Odf.Tests): in-memory inputs.
