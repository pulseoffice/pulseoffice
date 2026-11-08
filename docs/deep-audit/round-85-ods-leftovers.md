# Deep audit round 85: ODS leftovers of round 83

Continues [round-83-ods.md](round-83-ods.md): the three minor points left open there, and the same patterns checked in the
XLSX writer on a sheet merged across all 16,384 columns and 1,048,576 rows.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Formats.Odf/Spreadsheet/SheetWriting.cs | 382/382 | `OdsRowIndex` restructured (D1, D2) |
| src/PulseOffice.Formats.Odf/OdsWriter.cs (`WriteTable`, `RowSpans`, `Padded`, `SplitAtBlocks`, `BuildRow`) | 230-620/620 | D2, D3 |
| src/PulseOffice.Formats.Odf/OdfDatabaseRangeMapper.cs | 239/239 | D4 |
| src/PulseOffice.Formats.Odf/OdfSheetMetadataMapper.cs (validation names and reader) | 60-155 | reader already folds repeated rows and columns into ranges |
| src/PulseOffice.Sheets/CellRowIndex.cs | 80/80 | no defect |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxAutoFilterCodec.cs (filters) | 20-95 | empty `<filters/>` already read back as an empty allowed set |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/SheetSections.cs (`MergesSection`), XlsxMetadataMapper.cs (validations) | parts | ranges written as references, no per-cell work |
| src/PulseOffice.Sheets.Engine/Operations/SheetFilter.cs (`Matches`) | 200-218 | see Remaining |

## Defects

- **D1 (serious, silent loss) — `SheetWriting.cs`, `OdsRowIndex`**: validated cells were indexed only up to the last used
  row, so a validation over a whole column (or any range past the last used cell) was saved truncated: `C1:C1048576` on a
  sheet using only `A1` came back as `C1`; `A1:J10` with one value in `F4` came back as `A1:J4`. Not declared anywhere.
  Fixed at the root: the index keeps merges and validations as *bands* (row segments, each with the sorted edge columns of
  the bands that cover it, built in one sweep), and point cells apart. Rows inside bands are significant up to the band's
  end. Tests: `OdsRound85AuditTests.A_whole_column_validation_keeps_its_rows_beyond_the_last_used_row`,
  `A_validation_cell_inside_a_gap_carries_the_validation_name`.
- **D2 (medium, performance) — same place**: every validated cell was indexed (16 million for a validation across all
  columns of 1,000 rows); now only the two edge columns of each band per segment. A gap between indexed columns is wholly
  inside or outside every band, so the gap filler carries `table:content-validation-name` (and is covered when inside a
  merge); `SplitAtBlocks` keeps the validation name on the split pieces.
- **D3 (medium, performance and size) — `OdsWriter.cs`, `RowSpans`**: rows of a whole-column merge were written one per
  row. Rows that hold only band content are now coalesced with `table:number-rows-repeated` up to the next band boundary,
  row-group or header boundary, or row shape change. Test: `Rows_of_a_whole_column_merge_are_written_with_repeated_rows`.
- **D4 (minor, loss) — `OdfDatabaseRangeMapper.cs`**: a value filter admitting no value wrote an `=` condition with an empty
  value and no set items, read back as an empty criterion (the filter vanished). It is now written as two conditions on the
  field, `empty` and `!empty`, inside the filter's `table:filter-and` — no row passes in any ODF reader — and read back as
  an empty allowed set without blanks. A lone `!empty` from other files, previously lost, reads as the criterion `<>`.
  Tests: `A_value_filter_that_admits_nothing_stays_a_value_filter`, `A_value_filter_that_admits_only_blanks_survives`,
  `XlsxOdsParityTests.A_value_filter_that_admits_nothing_crosses_both_formats`.

## Measurements (serial performance collections)

| Case | Before | After |
|---|---|---|
| ODS, merge and validation over the whole sheet (16,384 × 1,048,576) | validation saved truncated to row 1 | write 346 ms, 8.3 KB, read 17 ms, both read back whole |
| ODS, validation across all columns of 1,000 rows | 16 M indexed cells | write 16 ms, 15 KB, read ~0.2 s |
| ODS, 10,000 merges (existing test) | 0.34 s | 0.34 s (a first band version without segments took 32 s; replaced) |

XLSX: merges and validations are written as references (`mergeCell`, `dataValidation sqref`), rows only for cells, so no
change was needed: whole-sheet merge plus validation writes in 4 ms (3.3 KB) and reads in 3 ms; whole-column merge and
validation: XLSX 2 ms / 3.3 KB, ODS 374 ms / 3.0 KB, both read back identical. Test runs: Formats.Odf.Tests 812/812,
OpenXml parity and performance 93/93.

## Remaining

- `SheetFilter.Matches` (engine, outside this area): an allowed-values set that is empty does not reject non-empty
  values, so a filter admitting nothing (or only blanks) still shows every non-empty row. The formats now preserve it;
  the engine should treat a non-null empty set as "no value".
