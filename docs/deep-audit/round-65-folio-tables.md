# Deep audit round 65: Folio tables

## Coverage

| File | Lines read / total | Outcome |
|---|---|---|
| src/PulseOffice.Documents/Blocks/TableBlock.cs | 137/137 | sound |
| src/PulseOffice.Documents/Editing/TableEditing.cs | 252/252 | sound: insert/delete rows and columns keep spans consistent, heirs carry merged content |
| src/PulseOffice.Documents/Editing/Tables/TableCellEditing.cs | 166/166 | sound for selections built by `TableSelections` (they enclose spans) |
| src/PulseOffice.Documents/Editing/Tables/TableGrid.cs | 72/72 | sound |
| src/PulseOffice.Documents/Editing/Tables/TableSorting.cs | 61/61 | sound: refuses ranges with vertical spans |
| src/PulseOffice.Documents/Editing/Tables/TableSelections.cs | 19/19 | sound |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxBlockReader.cs | 280-489 (table reading) | two defects, fixed |
| src/PulseOffice.Formats.Odf/Odt/OdtBodyReading.cs | 737-906 (table reader) | three defects, fixed |

Not reached in this round: layout (`PulseOffice.Documents.Layout` table splitting), DOCX/ODT table writers,
`TableConversion`, `TableFormatting`, `TableFormulaEditing`, `TablePasting`, the Folio table UI.

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | serious | DocxBlockReader `OriginAbove` | a `w:vMerge` continuation with no cell above (first row after a horizontal merge, or an origin with another `gridSpan`) became covered cells with no origin: its text was dropped silently and the grid had orphan covered cells | the reader keeps, per column, the origin that reaches the previous row; a continuation extends it only when it starts in the same column with the same span, otherwise it is read as a fresh cell, as Word does |
| 2 | serious | DocxBlockReader `OriginAbove` | the upward search skipped a horizontally covered cell and extended an origin two or more rows up, producing overlapping spans | same fix: only the origin reaching the previous row counts |
| 3 | serious | OdtTableReader | short rows were padded with covered cells that no cell owns (ragged grid, cells not editable) | new `ITableGridRepair` (`TableGridRepair`, Documents/Blocks) pads, clamps and uncovers |
| 4 | serious | OdtTableReader | `number-rows-spanned` / `number-columns-spanned` were not clamped to the table nor to cells that are not covered: spans past the last row or overlapping other cells | `TableGridRepair` clamps each span to free covered cells |
| 5 | medium | OdtTableReader | a `table:covered-table-cell` with content and no owner lost its text | `TableGridRepair` turns it into a normal cell keeping its content |

Tests: `tests/PulseOffice.Formats.OpenXml.Tests/DocxVerticalMergeTests.cs` (4, three failed before the fix),
`tests/PulseOffice.Formats.Odf.Tests/OdtTableGridRepairTests.cs` (3, all failed before the fix).
