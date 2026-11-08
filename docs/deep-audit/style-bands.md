# Ledger style bands (2026-11-04)

Implements the proposal in [ledger-ui-round2.md](ledger-ui-round2.md): formatting whole rows over formatted columns (or
whole columns over formatted rows) no longer creates explicit cells at the crossings.

## Structure

- `StyleBands` (`src/PulseOffice.Sheets/StyleBands.cs`), owned by `SheetLayout.Styles`: three layers resolved in order
  **block > row > column > default** (explicit cell styles still win over all of them in `Worksheet.StyleIdAt`).
  Rows and columns are the existing `RunLengthMap<int>`; `SheetLayout.RowStyles`/`ColumnStyles` now delegate to it.
- `StyleBlocks` (`src/PulseOffice.Sheets/StyleBlocks.cs`): the derived crossing layer, a two-dimensional run-length
  structure: sorted, non-overlapping column strips, each with a `RunLengthMap<int>` of rows. Lookup is two binary
  searches; adjacent strips with equal row runs are merged, so "rows 2..end × columns B..D" is one block.
  The last block written wins.
- `StyleBandsMemento` (Memento): `Save()`/`Restore()` copy the three layers; undo and redo of a band change restore the
  before/after memento inside the same history transaction as the cell changes, so the command is one step.
- `IStyleBandFormatter` / `StyleBandFormatter` (`src/PulseOffice.Sheets/Styling/StyleBandFormatter.cs`, registered in
  `LedgerCoreComposition`): applies a style change to whole rows/columns. It cuts the selection at row runs, column runs,
  block edges and selection edges, computes the target style of every crossing piece from the style it shows *before*
  the change, applies the row/column band changes, then writes a block only where the target differs from what the
  bands alone now resolve (and removes the block where it matches). Whole sheet: only rows that already have a row style
  are restyled, as before, plus existing blocks.
- `SelectionWorkArea.Plan(selection, used)` no longer plans the crossings as cell areas: only the used area becomes
  cells (unchanged behaviour inside the data).
- Structure: `SheetLayout.Insert/DeleteRows/Columns` shift blocks too; `NeighbourFormatPart` copies the line style and the
  blocks of the neighbouring row/column (`StyleBands.CopyRow/CopyColumn`). Snapshots, sheet duplication and
  `ShapeSignature` include blocks through `SheetLayout.CopyTo`.
- Protection: `SheetEditGuard` also checks locked styles of blocks in the range.
- Copy/paste, painting and editing read `StyleIdAt`, which resolves blocks; nothing else changed. Sorting moves explicit
  cells only, so bands stay where they are, as row bands already did.

## Formats

- ODS writer: rows touched by blocks are significant; the row shape includes the block runs, so equal rows are still
  written once with `table:number-rows-repeated`; inside the row, empty gap and padding cells are split at the block
  runs into `table:table-cell table:style-name … table:number-columns-repeated` (a block with the default style writes
  `Default`).
- ODS reader: row styles (`default-cell-style-name` or the trailing repeated cell) are set as one range, without the old
  65,536-repeat cap that dropped them; an empty, purely styled cell in a styled row (or in a row repeated more than
  4,096 times) becomes a block instead of explicit cells, which also removes the old 4,096-row truncation of such cells.
- XLSX writer: `<row s customFormat>` and `<col style>` as before; OOXML has no crossing layer, so empty `<c r s>` are
  written only at the block crossings.
- XLSX reader: an empty `<c>` without formula or named style in a `customFormat` row becomes a block (merged into runs),
  not an explicit cell.
- ODS ↔ XLSX round trips keep the resolved style of every cell; no new loss, no new `CheckFidelity` code.

## Measures (bold on rows 2..1,048,576 with columns B, C and H formatted, data in A1:E5)

| | cells created | time | allocated |
|---|---|---|---|
| before | 3,145,736 | 3,801 ms | 1,221,444 KiB |
| after | 19 (the used area A2:E5) | 13 ms | 26 KiB |

## Tests

- `SheetFormattingTests.BoldOnWholeRowsOverThreeFormattedColumnsCreatesNoCells` (measure, undo, redo).
- `StyleBandsTests` (resolution, coalescing, insert/delete, neighbour copy, memento, formatter cases).
- `SelectionWorkAreaTests` rewritten for the new plan.
- `StyleBandsRoundTripTests` (ODS with 1,048,575 repeated rows, XLSX, ODS→XLSX→ODS, cleared crossing).

## Known limits

- `RowAutoHeight` measures row bands but not block fonts (a larger font only at a crossing does not grow the row).
- XLSX still writes one `<row>` per styled row, as OOXML requires.

## To test live

- Format columns B:C red, select rows 2..end and press bold: immediate; B and C keep red and become bold; undo in one step.
- Insert a row inside the band: it takes the formatting; save as ODS and XLSX and reopen both.
