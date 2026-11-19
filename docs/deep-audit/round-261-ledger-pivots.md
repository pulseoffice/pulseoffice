# Round 261: Ledger pivot tables

Area: creation and layout, fields and value settings, grouping, calculated fields, sorting and filtering, refresh,
GETPIVOTDATA, and the XLSX and ODS round trips, compared with Excel. Not built or run here (the coordinator builds and
tests at the join).

## Defects fixed

1. **Medium: Index, Difference From and % Difference From were read from both formats but computed as Normal.** The
   engine now computes Index (cell x grand total / (row total x column total)) and the difference and percent
   difference from the previous item of the outermost row field (the column field when there is no row field), which
   is the base both writers already store; the first item and grand totals are blank, as in Excel.
2. **Medium: no sorting by value and no Top/Bottom N filter.** `PivotField.SortByValue` (index of a value field) and
   `PivotField.TopFilter` (count, value field, from bottom) are new model data. The engine orders items by their
   aggregate and keeps the top or bottom N; hidden items leave the subtotals and grand totals, as Excel does by
   default. ODS: `data-pilot-sort-info sort-mode="data" data-field` and `data-pilot-display-info`. XLSX: `autoSortScope`
   with a data-field reference, and a `filters/filter type="count"` with `top10`. Both read back.
3. **Medium: Outline layout rendered like Compact, and Repeat Item Labels was ignored.** Outline now puts each row
   field in its own column; with Repeat Item Labels the outer labels are written on every inner row (Outline and
   Tabular).
4. **Medium: Repeat Item Labels was declared lost in XLSX (XLSX063).** It is now native: `x14:pivotField
   fillDownLabels` in the pivot field extension list, written and read; the declaration is gone.
5. **Medium: a single selected item of an XLSX report filter (`pageField item`) was dropped on reading**, so the pivot
   showed every item. The other items now become excluded values; writing a filter field with exclusions sets
   `multipleItemSelectionAllowed`.
6. **Medium: GETPIVOTDATA did not accept Excel's data field names** ("Sum of Amount", "Average of Amount") when the
   pivot had more than one value field. The "<function> of <field>" form is now matched.
7. **Minor: numeric items were written as text** in the output (left aligned, not usable in formulas); ungrouped
   numeric items are now numbers. **Blank items** now show "(blank)" in the five languages, and GETPIVOTDATA accepts
   that label.
8. **Minor: percent display modes showed raw fractions**; they now carry the built-in 0.00% format.
9. **Minor: every XLSX data field caption was "Sum of" unless Count**, so an average appeared as "Sum of Amount" in
   Excel. Captions follow the function and read back as unnamed.
10. **Minor: XLSX `location` always said first data row 1 and column 1**; they now follow the column and row field
   counts.

Tests: `tests/PulseOffice.Sheets.Pivot.Tests/Round261PivotTests.cs`,
`tests/PulseOffice.Formats.OpenXml.Tests/Round261XlsxPivotTests.cs`,
`tests/PulseOffice.Formats.Odf.Tests/Round261OdsPivotTests.cs`.

## Reviewed without defects

Aggregations (sum to distinct count, sample spreads with one value give #DIV/0!), item order (round 177), numeric
bins with negative starts, calculated fields summing their columns, refresh ownership and overlap checks, destination
sheet handling, GETPIVOTDATA through hidden subtotals and filter fields, ODS hidden members, subtotals and grand total
flags.

## Open (queued)

- No interface to edit an existing pivot: the dialog only places fields and picks the aggregation. Show values as,
  number format, grouping, sort (label or value), label/value/top filters, layout, subtotals, grand totals, repeat
  labels and blank rows are reachable only from files or macros. Largest gap against Excel.
- Value field number formats are not modelled (XLSX `dataField numFmtId`, ODS number style of the data field).
- Grouping in XLSX (dates by years/quarters/months/days, numeric ranges) is still declared (XLSX030): needs
  `fieldGroup` with `rangePr` and `groupItems` in the cache. Manual groups are not modelled at all.
- Date group labels are not localized ("2024-T1", "2024-03"); Excel shows "Qtr1", "Jan" and can group months across
  years. Ungrouped date items are written as serial numbers without a date format.
- Boolean items use the Italian keys "VERO"/"FALSO" in every language.
- Running total and difference use only the first row field as base field; Excel lets any field and item be the base.
  % of parent, rank and % running total are not written to XLSX (x14 `pivotShowAs`) or ODS.
- Compact layout still reserves one column per row field (empty columns beside the labels); tabular layout puts each
  parent on its own row rather than beside its first child; no blank line after each item.
- Label and value filters other than Top N, and slicers, are not modelled.
- XLSX pivots are written without `rowItems`/`colItems`; Excel rebuilds them on refresh. Not opened in Excel here.
