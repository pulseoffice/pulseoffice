# Round 96: advanced filters of named database ranges in XLSX

Date: 2026-11-07. Trigger: a corpus file (corpus) has an advanced filter
(`table:condition-source-range-address` plus `filter-or`) on the named database range `myData`; round 90 declared it
lost in XLSX (XLSX233).

## What Excel can hold

Excel stores an in-place advanced filter per sheet: the sheet-local `_xlnm._FilterDatabase` (the filtered range, hidden),
the sheet-local `_xlnm.Criteria` (the criteria range) and the hidden rows. One per sheet, and not next to an AutoFilter,
which uses the same `_FilterDatabase`. Criteria ranges natively carry or-rows and and-columns, so a filter that already
has its own criteria range loses nothing. "Unique records only" has no place in the file. A condition tree without a
criteria range of its own would need cells written into the user's sheet to become a criteria range: rejected, it stays
declared.

## Change

- `XlsxRangeFilters` (src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxRangeFilters.cs) chooses one criteria owner per
  sheet: the sheet's own advanced filter as before, otherwise, on a sheet with no AutoFilter and no sheet filter, the
  first named database range (by position, then name) with a header, no totals row, no AutoFilter buttons or column
  filters, and a criteria range on an existing sheet. Its range and criteria range go into the two sheet-local names;
  the database range is still written as its table, with its own name.
- Reading: when `_FilterDatabase` matches a database range on that sheet exactly (header, no filter yet), the criteria
  go to that range; otherwise to the sheet, as before. Tables are published before defined names are read.
- `Lost` counts a criteria-owning database range only when it asks for unique records; other database-range advanced
  filters (second on the same sheet, beside an AutoFilter or sheet filter, condition trees) stay declared.
- XLSX233 text narrowed in `XlsxFormat.cs` and in the five language files (`diagnostic.xlsx233`): "a second advanced
  filter on the same sheet or one next to an AutoFilter" instead of "advanced filters on database ranges", and "that
  part of the filter is lost" because unique records lose only the flag.
- `tools/corpus-audit/FeatureRules.cs`: XLSX `_xlnm.Criteria` defined names count as filters, so the audit sees advanced
  filters written through names (sheet ones included, round 90).

## Verification

- `Round90FilterParityTests`: four new tests (named range crosses XLSX and ODS→XLSX; second one on the same sheet
  declared; unique records keep the criteria and declare the flag; beside an AutoFilter declared). Whole
  `PulseOffice.Formats.OpenXml.Tests`: 2415 passed.
- Corpus: a corpus file → `.xlsx` OK with only XLSX058, XLSX190; XLSX233 no longer raised.
