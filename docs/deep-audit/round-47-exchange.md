# Deep audit round 47: exchange, clipboard, drag and drop

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Exchange/ExchangeSerializer.cs | 133/133 | defect D1 (fixed) |
| src/PulseOffice.Exchange/ExchangeContentGuard.cs | 60/60 | ok (negative sizes already normalised) |
| src/PulseOffice.Exchange/Clipboard.cs | 30/30 | ok |
| src/PulseOffice.Exchange/ObjectExchange.cs | 160/160 | ok |
| src/PulseOffice.Sheets.UI/Exchange/LedgerExchange.cs | 349/349 | ok: protection through `Refused`, read-only checked, paste clipped to sheet bounds |
| src/PulseOffice.Shell/Exchange/HtmlTableGrids.cs | 46/46 | ok |
| src/PulseOffice.Formats.Interchange/Text/Html/HtmlTableReader.cs | 305/305 | ok: 1M cell cap, spans clamped to 1000 |
| src/PulseOffice.Shell/ExchangeDragDrop.cs | 77/77 | ok: drops bypass the command executor, but every participant checks read-only and protection at the source |
| src/PulseOffice.Documents.UI/Exchange/FolioExchange.cs | 94/94 | ok: `FolioEditor.Paste`/`PasteText` refuse protected ranges and forms |
| src/PulseOffice.Presentations.UI/Exchange/DeckExchange.cs | 198/198 | ok |
| src/PulseOffice.Sheets.Engine/Operations/SheetClipboard.cs (paste, tabular text) | 300/560 | ok: destinations validated, text never becomes a formula |

HTML and RTF readers (`HtmlDocumentReader`, `RtfDocumentReader`): checked for fetching and caps only. No network
access, only `data:` images, with byte caps (32 MB HTML image, 64 MB RTF picture), input caps (64/256 MB) and depth caps.

## Defects

### D1 (serious) - hostile native JSON yields objects with null required members

`JsonExchangeSerializer` did not respect nullability annotations or required constructor parameters, so a native package
from the system clipboard could carry, for example, a `ShapeGroupObject` with `Children: null` or a `GroupChild` with no
`Content`. The guard admitted it and the receiving module crashed later (decomposition, rendering, writers).

Fix: the serializer now sets `RespectNullableAnnotations` and `RespectRequiredConstructorParameters` and writes nulls
explicitly (no `WhenWritingNull`), so every member a well-formed package carries is present and a null where the model
does not allow one rejects the whole package. The envelope's optional members got default values so a package without
origin or content is still read.

Tests: `tests/PulseOffice.Exchange.Tests/DeepAuditRoundFortySevenExchangeTests.cs` (null children, null child content,
omitted child content fail before the fix; intact package still read).

## Remaining / live checks

- Copy and paste of every object kind between two running instances (native JSON now carries explicit nulls; an
  instance built before this round omits them and its packages fall back to the foreign representations).
