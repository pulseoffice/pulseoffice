# Round 147: external data and formula interchange

On Linux (Fedora), 2026-11-15, branch `audit-round-147` from `work`. A focused code audit of the areas the recent
rounds touched least: the external-data path (`PulseOffice.Sheets.Queries`, `PulseOffice.Document.Data`, the tabular
sources of `PulseOffice.Formats.Interchange`), the CSV and HTML spreadsheet codecs, and the OMML/MathML formula syntaxes.

## Method

1. Reading of the code above, looking for data loss, wrong results, crashes and quadratic paths.
2. A throwaway probe test that parsed 23 formulas and sent each through OMML and MathML write and read, comparing the
   MathML of the result with that of the parsed formula.
3. Each defect: a test seen red first, the fix, one commit.
4. `tools/run-tests.py` over the affected projects (Architecture, Data, Formats.Interchange, Sheets.Queries,
   Robustness); the command sweeps were not run.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Medium | A refreshed external pivot table kept a field whose column had left the query result at its old index, so it silently summarised whatever column now stood there. | `PivotSourceRefresher` drops such fields, as range sources already do on column deletion, keeps calculated fields and adds new columns as unused fields. |
| Medium | HTML tables read as external data (web queries) ignored `rowspan`: every later cell of the rows below moved one column to the left. A huge `colspan` number threw `OverflowException`. | Covered slots are tracked per column; spans are parsed with `TryParse` and clamped. |
| Minor | `DataTableLoader` (Folio data tables, mail merge) caught only `DbException`; a connection string the driver rejects (`ArgumentException`) escaped the command instead of the "load failed" report. | Every failure except cancellation is a `data.query.failed` diagnostic, as in `QueryRefresher`. |

## Checked without defects

- OMML and MathML round trips of fractions, roots, scripts, large operators with and without limits, functions,
  fences, matrices, accents, text and numbers: equal, apart from nested rows that OMML flattens (same meaning).
- CSV writer and reader: dates, times, percentages, booleans, the formula guard and the culture-dependent numbers.
- `QueryRefresher`: clipping at the sheet edges, clearing the previous result area, deleted sheets.
- HTML spreadsheet writer: spans of merges across hidden rows and columns, escaping.

## Not verified

- No live database server: the providers were exercised only through SQLite.
- Minor, left open: in the HTML spreadsheet export, a merge whose first cell lies in a hidden row or column loses
  its text; a CSV text that looks like a number reads back as a number (the reference suite behaves the same
  without "quote all text").
