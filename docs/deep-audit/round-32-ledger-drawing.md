# Deep audit round 32: Ledger, Deck and drawing code of the declared-loss blocks

Scope: code added by blocks B6, B6b, B10 (and the B10 follow-up) for external pivot sources, Excel tables from
database ranges, linked and grouped pictures, dimension lines and preserved 3D scenes.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Queries/PivotSourceRefresher.cs | 123/123 | ok (residual R1) |
| src/PulseOffice.Sheets/Pivot/PivotExternalSource.cs | 49/49 | ok |
| src/PulseOffice.Data/ReadOnlySqlPolicy.cs | 51/51 | D1 |
| src/PulseOffice.Data/SqlWordReader.cs | 135/135 | D1 |
| src/PulseOffice.Sheets.UI/LedgerPivotCommands.cs (refresh) | 45-75 | ok: refresh only on user command, one checkpoint |
| src/PulseOffice.Formats.Odf/OdfPivotSourceCodec.cs | 170/170 | D5 |
| src/PulseOffice.Formats.OpenXml/XlsxPivotExternalSources.cs | 178/178 | D2, D3, D4 |
| src/PulseOffice.Formats.OpenXml/XlsxPivotMapper.cs (cache writing and reading) | 150-215, 430-530 | D4 |
| src/PulseOffice.Sheets.Pivot/PivotSources.cs | 45/45 | ok (ragged records are safe) |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTableWriter.cs | 149/149 | ok (residual R3) |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTableFilters.cs | 44/44 | ok |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTables.cs (Publish) | 40-60 | residual R3 |
| src/PulseOffice.Formats.OpenXml/XlsxImageMapper.cs (B6 diff) | diff | D3 shared |
| src/PulseOffice.Formats.Odf/OdfImageMapper.cs (B6 diff) | diff | ok: links are never loaded on open |
| src/PulseOffice.Formats/LinkedFiles.cs | 60/60 | ok |
| src/PulseOffice.UI.Controls/Shapes/Painting/Painters.cs (RasterPainter) | 70-110 | ok: unloaded links drawn as placeholder |
| src/PulseOffice.Presentations.UI/DeckLinkResolver.cs | 27/27 | ok: path resolution only, no read |
| src/PulseOffice.Formats.Odf/Shapes/DeckSceneCodec.cs | 105/105 | D6 |
| src/PulseOffice.Formats.OpenXml/Shapes/DeckSceneCodec.cs | 81/81 | ok (ODF side validates before writing) |
| src/PulseOffice.UI.Controls/Shapes/Painting/ScenePainter.cs | 60/60 | ok |
| src/PulseOffice.Presentations/SlideTargetResolver.cs | 28/28 | ok |
| src/PulseOffice.Drawing/Shapes/DimensionLine.cs | 74/74 | ok |
| src/PulseOffice.Drawing/Shapes/DimensionLineGeometry.cs | 140/140 | ok: zero length, clamped decimals, finite inputs |
| src/PulseOffice.Formats.Odf/OdfDimensionLines.cs (reading) | 30-70, 146-149 | ok: lengths go through Twip |
| src/PulseOffice.Formats.OpenXml/ShapeStyles/DrawingDimensionLines.cs (Restore) | 80-140 | residual R4 |

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| D1 | grave (security) | SqlWordReader / ReadOnlySqlPolicy | A quoted function name bypassed the forbidden-function list: `SELECT "pg_read_file"('/etc/passwd')`, `pg_catalog."lo_import" (...)`, `U&"pg\005fread_file"(...)`, `"load_extension"/**/(...)`. The pivot source SQL comes from an untrusted file. | A quoted identifier followed (after spaces and comments) by `(` becomes the token `SqlWordReader.QuotedCall`, which the policy forbids. |
| D2 | grave (DoS) | XlsxPivotExternalSources.Records | Each cached record allocated a `CellValue[fields]` array whatever its content: thousands of fields times many `<r/>` (a few compressed bytes each) grew memory as fields x records. | Records hold only the values they carry (ragged records were already safe in `CachedPivotSource`). |
| D3 | medium (crash on save) | XlsxPivotExternalSources.Source | `new Uri(path)` threw `UriFormatException` for a workbook path that is not a valid URI (read from ODS): the XLSX save failed. | Shared `ExternalTargetUris` (also used by linked pictures) escapes invalid targets. |
| D4 | medium (crash on save) | XlsxPivotMapper | Cache field names and string items with XML-invalid characters (database text) made the XLSX save throw. Also affected sheet-range pivots. | Encoded with `OoxmlTextEscape` (`_xHHHH_`) and decoded on every read path (field names, items, hidden-item keys). |
| D5 | minor | OdfPivotSourceCodec.ReadValue | `pulsepivot:error="9999"` produced an undefined `CellError`. | Only defined, named errors are accepted; others become `#VALUE!`. |
| D6 | medium (security) | Formats.Odf DeckSceneCodec | The namespace whitelist checked element names only: `xlink:href` (external fetches by other readers), `xml:base`, and `office:`/`script:` content in the carried graphic styles were written into the ODF. | Attributes in xlink/office/script/dom namespaces and `xml:base` are stripped from the scene; styles carrying such elements or attributes are not written. |

## Residual (not fixed, declared)

- R1: the read-only check is lexical: a user-defined function with side effects called by its bare name still runs. The root
  fix is a read-only transaction or connection per driver in `PulseOffice.Data` (shared with query refresh); out of this area.
- R2: a pivot refresh runs the file's query on a connection configured by the user, looked up by name from the file. It runs
  only on the explicit refresh command; a confirmation naming the connection would make it explicit.
- R3: XLSX tables with a totals row become database ranges without totals; on write the totals row becomes data.
  A database range named like the query-table defined names (`ExternalData_n`) would collide.
- R4: the OOXML dimension-line reader accepts any unit string, the ODF reader only known units.

## Tests

- PulseOffice.Data.Tests: 6 new bypass cases in `ReadOnlySqlPolicyTests` (failed before).
- PulseOffice.Formats.OpenXml.Tests: `XlsxExternalPivotTests` (3 new, failed before), `DimensionLineAndSceneTests` (3 new
  cases, failed before); full project 2273 passed (one timing test passed on rerun).
- PulseOffice.Formats.Odf.Tests: `OdsExternalPivotTests` (2 new, error case failed before).
- PulseOffice.Sheets.Queries.Tests: 29 passed.
- Corpus (`one`): a corpus case, pivot-table-err-in-cache.ods, PivotTable_CachedDefinitionAndDataInSync.xlsx,
  a corpus case: LOSS 0.
