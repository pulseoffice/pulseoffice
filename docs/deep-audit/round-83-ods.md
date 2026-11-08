# Deep audit round 83: ODS reader and writer

Continues round 82 ([round-82-ledger-formats.md](round-82-ledger-formats.md)): ODS reader and writer, sheet part
readers, sheet writing, external pivot sources, table totals, database-range filters, shape text lists, number schemes.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Formats.Odf/OdsReader.cs | 241/241 | no defect of its own |
| src/PulseOffice.Formats.Odf/Spreadsheet/SheetPartReaders.cs | 660/660 | D1 (duplicate sheet rename can collide); dead `Strip` helper in `PrintRangePartReader` |
| src/PulseOffice.Formats.Odf/OdsWriter.cs | 620/620 | D2 (workbook named expressions written in the prelude) |
| src/PulseOffice.Formats.Odf/Spreadsheet/SheetWriting.cs | 373/373 | D3 (merged regions enumerated cell by cell when saving) |
| src/PulseOffice.Formats.Odf/OdfPivotSourceCodec.cs | 170/170 | element and attribute names match ODF 1.3 (`table:database-source-*`, `table:source-service`); cache in our own `urn:pulseoffice:pivot` namespace; no defect |
| src/PulseOffice.Formats.Odf/OdfPivotMapper.cs | 305/305 | child order of `data-pilot-table`, `data-pilot-field`, `data-pilot-level` matches the schema; no defect |
| src/PulseOffice.Formats.Odf/OdfDatabaseRangeMapper.cs | 239/239 | `table:filter-set-item` (ODF 1.3), totals row in `calcext:contains-footer`; minor: a value filter allowing nothing writes an empty condition read back as an empty criterion |
| src/PulseOffice.Formats.Odf/Shapes/OdfShapeTextLists.cs | 121/121 | list styles keyed by marker and level geometry, bullet pictures inlined as `office:binary-data`; no defect |
| src/PulseOffice.Formats.Odf/OdsFormat.cs | 255/255 | CheckFidelity declarations checked against the writers (ODF020-ODF029, ODF040-044, ODF086/087, ODF170, ODF271); no stale or missing declaration found for the audited features |

## Defects

- **D1 (medium) — `SheetPartReaders.cs`, `OdsTableReader.Begin`**: a duplicate sheet name was renamed to
  `name_{count+1}` without checking that name was free. A file with sheets `A_3`, `A`, `A` renamed the third to `A_3`,
  `Workbook.AddSheet` threw and the whole file failed to open (ODF003). An empty `table:name` also reached `AddSheet`.
  Fixed: the rename goes through `Workbook.UniqueSheetName(name + "_")`, an empty name takes the canonical prefix.
  Test: `OdsRound83AuditTests.Duplicate_sheet_names_are_renamed_without_colliding_with_a_later_sheet`.
- **D2 (medium, ODF validity) — `OdsWriter.cs`, `WriteBody`**: workbook-level `table:named-expressions` were written in
  the `office:spreadsheet` prelude, before the tables. ODF 1.3 places them in the epilogue (`table-functions`: named
  expressions, database ranges, data pilots, consolidation, DDE links), after the last `table:table`. Strict validators
  reject the file. Fixed: emitted after the tables, before database ranges and data pilots.
  Test: `OdsRound83AuditTests.Workbook_named_expressions_follow_the_tables_as_the_schema_requires`.
- **D3 (serious, performance) — `SheetWriting.cs`, `OdsRowIndex`; `OdsWriter.cs`, `BuildRow`**: the row index took every
  cell of every merged region, and each one became its own `table:covered-table-cell`. A merge across all columns of
  2,000 rows wrote 32 million covered cells (5.9 MB package); a merge of whole columns or of the whole sheet (possible
  from a hostile ODS, where spans are clamped to the sheet) never finished saving. Fixed at the root: the index takes
  only the first and last column of each merged row, and a gap that falls inside a merge is written as one
  `table:covered-table-cell` with `table:number-columns-repeated` (merges cannot overlap, so a gap between two indexed
  columns is either wholly inside one merge or wholly outside). Content inside covered cells is still written.
  Test: `OdsRound83AuditTests.A_wide_merge_is_saved_as_repeated_covered_cells` (package below 2 MB, merge and covered
  content read back).
- **Minor — `SheetPartReaders.cs`, `PrintRangePartReader`**: unused private `Strip`, removed.

## Remaining (not fixed, minor)

- `OdsRowIndex` still adds every cell of a validation range up to the last used row: a validation over all 16,384
  columns on a sheet with 1,000 used rows indexes 16 million cells. Bounded, but slow; same treatment as merges would need
  validation names in repeated cells.
- Rows of a whole-column merge are still written one per row (no `number-rows-repeated` for rows in the index): a
  1,048,576-row merge saves, but with a million short rows.
- `OdfDatabaseRangeMapper`: a value filter that allows no value at all writes an empty condition read back as an empty
  criterion.
