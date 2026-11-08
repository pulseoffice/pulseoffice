# Declared losses, block B6b (Ledger): pivots on external sources

Codes: ODF080, XLSX081. Analysis: [../declared-losses-analysis.md](../declared-losses-analysis.md); previous block
[losses-b6.md](losses-b6.md).

## Files read

| File | Outcome |
|---|---|
| `src/PulseOffice.Formats.Odf/OdsReader.cs` (pivot section) | ODF080 raised when `table:data-pilot-table` has no `table:source-cell-range` |
| `src/PulseOffice.Formats.OpenXml/XlsxReader.cs` (pivot section) | XLSX081 counts every non-worksheet cache source |
| `tests/PulseOffice.Formats.Odf.Tests/OdsExternalPivotTests.cs` | Asserts the declaration; to be rewritten as a round trip |
| `src/PulseOffice.Sheets/Pivot/PivotDefinition.cs` | Sheet range or query binding as source only |
| `src/PulseOffice.Sheets.Pivot/PivotCoordinator.cs`, `PivotSource.cs`, `PivotEngine.cs` | Engine reads an `IPivotSource`: a cached-record source fits without touching the engine |
| `src/PulseOffice.Formats.Odf/OdfPivotMapper.cs` | Reads and writes only `table:source-cell-range` |
| `src/PulseOffice.Formats.OpenXml/XlsxPivotMapper.cs`, `XlsxPivotSourceResolver.cs` | Writes records from the sheet; external caches skipped |
| `src/PulseOffice.Formats.OpenXml/XlsxQueryTableMapper.cs` | Owns `connections.xml`: must share it with pivot connections |
| `src/PulseOffice.Sheets/QueryBinding.cs`, `src/PulseOffice.Data/Connections.cs`, `ConnectionDirectory.cs` | Connection directory keyed by id; credentials outside the document |
| `src/PulseOffice.Sheets.Queries/QueryRefresher.cs`, `src/PulseOffice.Sheets.UI/QueryRefreshRunner.cs` | Fetch/apply split reused for pivots |
| `src/PulseOffice.Sheets.UI/LedgerPivotCommands.cs`, `LedgerFinalCommands.cs`, `src/PulseOffice.Sheets.Pivot/PivotDataLookup.cs` | Every consumer built a `RangePivotSource` itself |
| `src/PulseOffice.Sheets/Values/CellValue.cs`, `src/PulseOffice.Formats.Odf/Tables/QualifiedRanges.cs` | Error text table reused for the XLSX cache reader; external ODF addresses built on the qualified-range writer |
| corpus: three corpus files, a corpus file | ODS: `table:database-source-query` without a cache; all XLSX cases are `worksheetSource r:id` pointing at **another workbook** through an `externalLinkPath` relationship of the cache part, with `pivotCacheRecords` present |

## Design

- **Model** (`PulseOffice.Sheets/Pivot/PivotExternalSource.cs`): `PivotDefinition.ExternalSource` with a kind (`Sql`,
  `Table`, `Query`, `Service`, `Workbook`), database/connection name, command, ODF `parse-sql-statement`, the
  source-service fields (never the password: decision 9), an optional connection-directory id and a `PivotRecordCache`
  (headers + records, value equality). For `Workbook` the external sheet and range live in `SourceSheet`/`SourceRange`.
- **Engine** (`PulseOffice.Sheets.Pivot/PivotSources.cs`): `IPivotSources` opens a `RangePivotSource` or a
  `CachedPivotSource`; the coordinator, GETPIVOTDATA (`PivotDataLookup`) and the calculated-field dialog use it. An
  external pivot without cached records keeps its last result in the cells and its definition (refresh does not clear it).
- **Refresh** (`PulseOffice.Sheets.Queries/PivotSourceRefresher.cs`): fetch/apply split as for query bindings; the
  connection is found by id, then by name in `IConnectionDirectory`; tables and queries become `SELECT * FROM` with the
  provider's quoting; SQL passes `IReadOnlySqlPolicy`. Failures keep the cache and say why (PVT010-PVT014, five
  languages). `QueryValueConverter` extracted from `QueryRefresher` and shared.
- **UI**: `ledger.data.pivotRefresh` (enabled only when the workbook has pivots) fetches external sources first, then
  recomputes everything in one undo step; the status bar reports sources read again or the first failure.
- **ODS** (`OdfPivotSourceCodec`): reads/writes the four database/service elements and external
  `table:source-cell-range` (`'url'#$Sheet.A1:B9`); the records travel in a foreign `pulsepivot:cache` element
  (`urn:pulseoffice:pivot`, values in attributes only). Foreign ODS without our cache: definition kept, cache empty.
- **XLSX** (`XlsxPivotExternalSources`): reads `cacheSource type="external"` + `connections.xml` `dbPr`
  (`commandType` 3 = table, else SQL) and `worksheetSource r:id` (`externalLinkPath`); records from
  `pivotCacheRecords` (shared items, `x` indices, dates as serials). Writes a connection with our fields in a
  `connection/extLst` marker, appended to the `connections.xml` written by query tables (queries now written first),
  or an external relationship for other workbooks; no cache gives `saveData="0"` without a records part.

## Declarations after the block

- **ODF080** narrowed: only the `table:password` of a `table:source-service` (credentials never enter the document).
- **XLSX081** narrowed: OLAP cubes, consolidation ranges, non-database connections and named ranges of another
  workbook (`column-style-autofilter.xlsx`, 17 pivots on `worksheetSource name=… r:id`).

## Tests

Sheets.Pivot 59/59 (`ExternalPivotTests`), Sheets.Queries 29/29 (`PivotSourceRefresherTests`, SQLite), Formats.Odf
770/770 (`OdsExternalPivotTests` rewritten), Formats.OpenXml 2235 + parity fact (`XlsxExternalPivotTests`,
`XlsxOdsParityTests`), Architecture 40/40, Sheets.UI pivot tests 24/24.

## Corpus

Full run: LOSS 0, DECL 170; ODF080 0 rows, XLSX081 2 rows (column-style-autofilter.xlsx).
