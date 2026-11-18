# Round 207: Ledger formula engine, third pass

Scope: the formula items round 202 left open: structured table references, XLOOKUP with an array lookup value and
several return columns, A1# on a single-value formula, refreshing the circular-reference notice.

## Defects fixed

- **Medium: structured table references were flattened on XLSX read.** `Table[Column]`, `[#This Row]`, `[@Column]`,
  `[#Headers]`, `[#Totals]`, `[#All]`, `[#Data]` and column spans became absolute A1 ranges, so they did not follow
  renamed columns or tables. They are now a `TableReferenceNode` in the formula AST (lexer token, `TableReferenceSyntax`
  for parsing and the canonical file form), evaluated through `ITableReferenceResolution` against the workbook's
  database ranges (column names from the header text, `ColumnN` when there is no header), registered as dependencies
  on the exact resolved range and re-registered when a table is defined, redefined or removed
  (`DatabaseRangeCollection.Changed`). Editing a header cell renames the column in every formula reading the table
  (`TableColumnFollower`); `TableRenaming` renames a table and its references. Rows inserted into a table are read by
  the same formula. XLSX writes the references back (the dialect leaves bracketed table specifiers untouched) and
  flattens only those to tables it cannot write as Excel tables; ODS has no structured references, so it writes the
  resolved A1 form in `table:formula` and keeps the structured formula in the `urn:pulseoffice:spreadsheet`
  `table-formula` attribute, read back first. The table structure itself already crosses ODS (database range with
  header and footer), so no loss is declared. Dependency collection moved out of `CalculationEngine` into
  `FormulaDependencies`.
- **Minor: the circular-reference notice refreshed only on selection and view changes.** The engine raises
  `Recalculated` after each outermost recalculation; `LedgerStatusSegments` subscribes only while shown and posts the
  refresh to the UI thread, coalescing bursts, so no long-lived subscription holds the document.

## Reviewed without defects

- XLOOKUP with an array lookup value and a multi-column return range: the other applications spill a lifted XLOOKUP
  in one direction only and keep the first column of each element's result (documented in public references on the
  array-of-arrays limitation); the engine matches.
- A1# on an anchor holding a single-value formula: the spilled range operator gives #REF! when the cell holds no
  spilled range; the engine matches.

Tests: `Round207TableReferenceTests` and `Round207FormulaEngineTests` (`PulseOffice.Sheets.Engine.Tests`),
`Round207TableReferenceFormatTests` (`PulseOffice.Formats.OpenXml.Tests`). Not built or run in this round; the
coordinator builds and tests after the join.

## Open items

Listed under «Ledger» in docs/audit-queue.md.
