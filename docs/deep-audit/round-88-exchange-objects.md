# Deep audit round 88: exchange matrix across modules and object kinds

Remaining point of round 47 ([round-47-exchange.md](round-47-exchange.md)): copy and paste of every object kind between
the three modules, through every representation, and version tolerance of the native JSON.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Exchange/ExchangeModel.cs | 154/154 | ok |
| src/PulseOffice.Exchange/ExchangeSerializer.cs | 135/135 | D1, D2, D3 (fixed) |
| src/PulseOffice.Exchange/ObjectExchange.cs | 150/150 | ok |
| src/PulseOffice.Exchange/ExchangeContentGuard.cs | 60/60 | ok |
| src/PulseOffice.Documents.UI/Exchange/FolioExchange.cs | 94/94 | ok |
| src/PulseOffice.Presentations.UI/Exchange/DeckExchange.cs | 198/198 | ok |
| src/PulseOffice.Document/Embedding/DataTableObject.cs | 60/120 | D1 source (`object?` cells) |
| tests/PulseOffice.Desktop.Tests/ExchangeTests.cs | 665/665 | harness extracted to `ExchangeSuite` |
| src/PulseOffice.Sheets.UI/Exchange/LedgerExchange.cs | 140/349 | ok (receive paths) |
| src/PulseOffice.Presentations.UI/DeckTableCommands.cs | 30/300 | D1 consequence (sort via `Convert.ToString`) |
| src/PulseOffice.Formats.Odf/Shapes/DeckObjectCodecs.cs | 40/~800 | D1 consequence (cell text via `ToString`) |

## Matrix

`tests/PulseOffice.Desktop.Tests/ExchangeMatrixTests.cs` (harness shared with `ExchangeTests` in `ExchangeSuite.cs`):
15 object kinds (chart, shape with lists/number formats/line breaks/picture bullets, linked image, nested group with
links, connector with a dangling endpoint, freeform, fontwork, dimension line, 3D scene, form control, data table,
formula, packaged object, signature line, vector graphic) x 3 source modules x 3 destinations: native paste keeps the
object type; the same content re-read from native JSON pastes as objects; every foreign representation of the capture
(ODF, OOXML, HTML, RTF, text, PNG) is pasted alone in every mode each module offers, with no exception, PNG always
accepted as an image and non-empty text always offering a mode. Ledger cells with formula, style, merge, validation and
comment, Folio formatted text and a Deck text box go through the same matrix. Older-build packages (optional members
missing) still paste; an unknown `$type` falls back to the foreign representations.

`tests/PulseOffice.Exchange.Tests/DeepAuditRoundEightyEightExchangeMatrixTests.cs`: every concrete `EmbeddedObject`
discovered by reflection, with every member populated (`PopulatedSamples`), round-trips byte-identically; removing each
member yields null exactly for required members and a package otherwise; unknown members are ignored.

Not covered: Folio fields, field ranges, tracked changes, content controls, footnotes and bookmarks, and Deck animations,
actions and sounds, travel only as Folio/Deck native fragments (same module) or as ODF/OOXML/HTML/RTF; the matrix pastes
those representations without crash but does not assert each construct's survival.

## Defects

### D1 (medium) - data table cells arrived as JsonElement
`DataSnapshot.Rows` is `object?`; the native JSON read numbers, booleans and dates back as `JsonElement`, so after a
cross-module paste numbers sorted as text in comma-decimal cultures, showed "2.5" and dates became strings. Fix:
`ExchangeScalarConverter` writes strings, numbers and booleans as JSON scalars and dates, offsets, durations and bytes
tagged, reads them back as CLR values, and rejects malformed tags.

### D2 (medium) - numeric enum values admitted
`JsonStringEnumConverter` accepted integers, so a hostile package could carry `Shape: 999`. Fix: `allowIntegerValues: false`.

### D3 (minor) - stale type catalog
`LoadedExchangeTypeCatalog` cached derived types forever; an assembly loaded after the first lookup was never
serializable. Fix: the cache is keyed on the number of loaded assemblies. No dedicated test (needs a fresh process).

## Tests run
Exchange.Tests 65/65; Desktop.Tests filtered on Exchange 114/114.
