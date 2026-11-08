# Deep audit round 66: Folio tables, part two

Continues [round 65](round-65-folio-tables.md) on the areas it left uncovered.

## Coverage

| File | Lines read / total | Outcome |
|---|---|---|
| src/PulseOffice.Documents/Blocks/TableGridRepair.cs | 63/63 | round 65 leftover fixed (defect 1) |
| src/PulseOffice.Documents/Blocks/TableBlock.cs | 137/137 | sound |
| src/PulseOffice.Documents/Editing/Tables/TableConversion.cs | 239/239 | two defects, fixed (3, 4) |
| src/PulseOffice.Documents/Editing/Tables/TablePasting.cs | 70/70 | two defects, fixed (2, 5) |
| src/PulseOffice.Documents/Editing/Tables/TableFormatting.cs | 176/176 | sound |
| src/PulseOffice.Documents/Editing/Tables/TableFormulaEditing.cs | 100/100 | sound |
| src/PulseOffice.Documents.UI/Commands/FolioTableCommands.cs | 293/293 | sound (split table is enabled only below the first row) |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxTableCodec.cs | 146/146 | sound |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxBlockWriter.cs | 120-253 (table writer) | sound; non-repeating header rows are already a declared loss in `DocxWriter` |
| src/PulseOffice.Formats.Odf/Odt/OdtBodyWriting.cs | 598-739 (table writer, formulas) | sound |
| src/PulseOffice.Documents.Layout/Flow/Paginator.cs | 285-710 (table pagination) | one defect, fixed (6) |

Not reached in this round: `CellNumberFormatting`, `TableBorderFraming`, `TableGeometryEditing`, `DocxFloatingTables`,
`DocxTableStyleParagraphs`, `DocxTablesInParagraphs`, `TableComposer` (cell placement and vertical text) and the rest of
`Paginator.cs`.

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | medium | TableGridRepair | text inside an ODT `table:covered-table-cell` that a spanning cell owns was dropped silently (round 65 leftover) | the owner absorbs the covered cell's blocks, appended after its own; empty paragraphs add nothing |
| 2 | serious | TablePasting.PasteRows / PasteColumns | rows pasted inside a vertical merge (or columns inside a horizontal merge) were plain cells overlapped by the merged cell: two owners per grid slot | new `ITableSpanCutting` (`TableSpanCutting`): the merged cell stops before the pasted rows/columns and an empty heir with the remaining span and the same formatting continues it after them |
| 3 | serious | TableConversion.SplitTable | a covered cell of the first moved row was uncovered whenever it had no content, even when a cell of the same row owned it horizontally (overlap); covered cells with content stayed covered with no owner | the same `TableSpanCutting` cuts the merges crossing the split and places one heir per merge, with its column span |
| 4 | minor | TableConversion.SplitTable | the second table lost the table's direct properties (padding, borders) | `Direct` is copied |
| 5 | minor | TablePasting | a pasted covered cell became a cell with no paragraph (not editable); pasted rows lost "allow row to break across pages" | an empty paragraph is added; the row flag is copied |
| 6 | serious | Paginator.Carry | when a merged cell's text continued on the next page under a repeated header row, the header emission discarded the pending carry: the rest of the merged cell's text disappeared from the layout, the screen and print | carries the emission does not reach are kept for the following emissions |

## Tests

- `tests/PulseOffice.Formats.Odf.Tests/OdtTableGridRepairTests.cs`: two new tests (owned covered text appended, empty covered cells add nothing).
- `tests/PulseOffice.Documents.Tests/TablePastingSpanTests.cs`: five tests (paste rows/columns inside merges, covered cells pasted, split across a horizontal merge, split across a wide vertical merge); all failed before the fix.
- `tests/PulseOffice.Documents.Layout.Tests/RepeatedHeaderCarryTests.cs`: three cases, all failed before the fix (text cut after the first page).

Runs: Documents.Tests 344/344, Documents.Layout.Tests 229/229, Formats.Odf.Tests 799/799.
