# Deep audit round 33: data access (closing R1-R4 of round 32)

Branch: worktree `agent-a9b9803013cecc7f2`, uncommitted. Scope: the residual points R1-R4 of
[round-32-ledger-drawing.md](round-32-ledger-drawing.md).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Data/AdoDatabaseProvider.cs | 122/122 | D1 |
| src/PulseOffice.Data/Connections.cs | 96/96 | ok |
| src/PulseOffice.Data.Sqlite/SqliteDatabaseProvider.cs | 27/27 | D1 |
| src/PulseOffice.Data.PostgreSql/PostgreSqlDatabaseProvider.cs | 26/26 | D1 |
| src/PulseOffice.Data.MySql/MySqlDatabaseProvider.cs | 25/25 | D1 |
| src/PulseOffice.Data.SqlServer/SqlServerDatabaseProvider.cs | 27/27 | D1 |
| src/PulseOffice.Data.Odbc/OdbcDatabaseProvider.cs | 46/46 | D1 |
| src/PulseOffice.Sheets.Queries/PivotSourceRefresher.cs | 123/123 | D2 |
| src/PulseOffice.Sheets.Queries/QueryRefresher.cs | 1-180 | D2, D3 |
| src/PulseOffice.Sheets.UI/QueryRefreshRunner.cs | 66/66 | D2 |
| src/PulseOffice.Sheets.UI/LedgerPivotCommands.cs | 1-90 | D2 |
| src/PulseOffice.Sheets.UI/LedgerExternalDataCommands.cs | 107/107 | D2 |
| src/PulseOffice.Sheets.UI/LedgerDocumentExtraCommands.cs | 40-130 | D2 |
| src/PulseOffice.Document.Data/DataTableLoader.cs | 1-70 | ok (runs through the read-only providers) |
| src/PulseOffice.Documents.UI/Commands/FolioMailMergeCommands.cs | 15-110 | D2 (the mail-merge source SQL also comes from the file) |
| src/PulseOffice.Agent/Tools/CommandTools.cs | 1-135 | D2 |
| src/PulseOffice.Desktop/Composition/DataAccessComposition.cs | 55-90 | wiring |
| src/PulseOffice.Sheets/DatabaseRanges.cs | 69/69 | D4 |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTables.cs | 203/203 | D4 |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTableWriter.cs | 149/149 | D4, D6 |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTableFilters.cs | 44/44 | D4 |
| src/PulseOffice.Formats.OpenXml/XlsxQueryTableMapper.cs | 1-219 | D5 |
| src/PulseOffice.Formats.Odf/OdfDatabaseRangeMapper.cs | 72-220 | D4 |
| src/PulseOffice.Formats.OpenXml/ShapeStyles/DrawingDimensionLines.cs | 1-150 | D7 |
| src/PulseOffice.Formats.Odf/OdfDimensionLines.cs | 1-60 | D7 |
| src/PulseOffice.Drawing/Shapes/DimensionLineGeometry.cs | 1-45, 105-115 | D7 |

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| D1 | grave (security, R1) | AdoDatabaseProvider.ExecuteAsync and the five drivers | Document queries ran on a writable connection in autocommit: the lexical `ReadOnlySqlPolicy` cannot see a database-defined function or procedure that writes, nor a statement form it does not know. | Every `ExecuteAsync` (query refresh, pivot refresh, data tables, mail merge, table listing) now opens a read-only session through the template hooks `ReadOnlyConnectionString` and `BeginReadOnlyAsync`, runs the command inside that transaction and **always rolls back**. SQLite: `Mode=ReadOnly` (unless in-memory) + `PRAGMA query_only = ON` + deferred transaction. PostgreSQL: `SET TRANSACTION READ ONLY` (a writing function fails). MySQL/MariaDB: `START TRANSACTION READ ONLY` through `BeginTransactionAsync(isReadOnly: true)`. SQL Server: there is no read-only transaction; `ApplicationIntent=ReadOnly` routes to readable secondaries where they exist and the rollback undoes every transactional write, but effects outside the transaction (extended procedures, linked servers, `xp_cmdshell`, sequences) are not prevented: the guarantee is "rolled back", not "refused". ODBC: `System.Data.Odbc` does not expose `SQL_ATTR_ACCESS_MODE`; the query runs in a transaction that is always rolled back, and a driver without transactions refuses the query (fails closed). |
| D2 | grave (security, R2) | query refresh, pivot refresh, query links dialog, Folio mail merge | A file could name a configured connection and carry any SQL: refreshing ran it without the user ever seeing it. | Shared gate `IExternalQueryGate` (PulseOffice.Data) consulted by `QueryRefresher` and `PivotSourceRefresher` before any query runs, and by Folio's mail merge for a source read from the file. The desktop implementation `ExternalQueryConsent` (PulseOffice.Data.UI) shows `ExternalQueryConsentView`, a modal with veil through `IDialogService`, listing each connection by its configured name and the SQL text (selectable, monospace), in five languages; consent is remembered per document object and per (connection, SQL) pair by `ExternalQueryConsentLedger` for the session, so a changed SQL asks again and a reopened file asks again. Queries the user has just chosen in the import dialog are granted directly. A declined refresh keeps cells, bindings and pivot caches untouched (`QRY006`, `PVT015`) and says so in the status bar. Commands that run document queries carry `CommandDefinition.RunsDocumentQueries`; the MCP agent needs the `files` level for them and asks a `DocumentQueries` confirmation (`agent.confirm.documentqueries`). |
| D3 | minor | QueryRefresher.Apply | A failed fetch writes the binding's `LastError` outside the undo history (kept for real failures); a declined refresh must not touch the document. | Declined entries leave the binding as it was. |
| D4 | medium (silent loss, R3) | XlsxTableCatalog.Publish, XlsxTableFilters, XlsxTableWriter, OdfDatabaseRangeMapper | An XLSX table with a totals row became a database range without totals; on save the totals row was written as a data row (no `totalsRowCount`), and a table whose autofilter excluded the totals row could not be written back as a table at all. | `DatabaseRange.HasTotalsRow` and `RangeWithoutTotals`. XLSX reads `totalsRowCount`, writes `totalsRowCount="1"`, the autofilter over the rows without totals, and for each column a `totalsRowLabel` (text cell) or `totalsRowFunction="custom"` with `totalsRowFormula` (formula cell); the totals cells themselves stay ordinary cells. ODS writes and reads `calcext:contains-footer="true"` on `table:database-range` (the attribute the reference suite uses, proposed as OFFICE-4169). |
| D5 | medium (R3) | XlsxQueryTableMapper | Query tables were always named `ExternalData_n`; a user database range or defined name with that name produced a table and a defined name with the same name (a file other readers repair). | Query-table names skip every name already used by a database range or defined name; defined name and `queryTable/@name` share the same map. |
| D6 | minor | XlsxTablePlanner.Fits | A totals table needed one more row than a plain one but the minimum did not count it. | The minimum counts header and totals rows. |
| D7 | minor (R4) | DrawingDimensionLines.Restore | The OOXML reader accepted any unit string, the ODF reader only known units. | Both ask `IDimensionLineGeometry.IsKnownUnit`, the table the label formatter uses; an unknown unit becomes automatic. |

## Tests

- PulseOffice.Data.Tests: `ReadOnlyExecutionTests` (writing statements with `RETURNING`, including a trigger that would write
  an audit row, fail and leave the database untouched; reading still works), `ExternalQueryConsentLedgerTests`.
- PulseOffice.Sheets.Queries.Tests: declined query leaves sheet and binding untouched; pivot requests carry the connection
  and SQL; declined pivot keeps its cache.
- PulseOffice.Agent.Tests: document-query commands need `files` and a `DocumentQueries` confirmation.
- PulseOffice.Formats.OpenXml.Tests: `XlsxTableTotalsTests` (totals written as totals, XLSX/ODS round trips both ways,
  table without autofilter, `ExternalData_1` collision), dimension unit cases in `DimensionLineAndSceneTests`.

## Try live

- Open an ODS/XLSX with a query or an external pivot, refresh: the modal names the connection and shows the SQL; decline
  keeps everything; accept, then refresh again: no second question in the same session.
- Agent: `run_command ledger.data.pivotRefresh` asks the agent confirmation, then the consent modal.
- An Excel table with a totals row: save to XLSX and reopen in Excel (no repair, totals row still a totals row); save to ODS
  and open in the reference suite (totals row recognised).
