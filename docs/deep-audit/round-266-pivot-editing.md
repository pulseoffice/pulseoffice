# Round 266: Ledger pivot editing interface and XLSX grouping (2026-11-19)

Area: the interface to edit an existing Ledger pivot (carried over by rounds 261 and 264), then XLSX pivot grouping
instead of the declared XLSX030 loss.

## Defects found

| Severity | Defect | Status |
|---|---|---|
| Serious | No way to change an existing pivot: show values as, base field, number format, grouping, sorting, filters, Top N, layout, totals and repeated labels were modelled and saved but could only come from files | Fixed |
| Medium | XLSX grouping (dates and number ranges) was not written: every grouped field was saved ungrouped behind XLSX030 | Fixed |
| Medium | Table options in the creation dialog did not exist, so a new pivot always started compact with both grand totals | Fixed |
| Medium | Sort by value and Top N point at a value field by position: reordering or removing value fields in the dialog would have silently retargeted them | Fixed |
| Minor | Item lists and group items show invariant keys (`2024-01`, `2024-T1`, `TRUE`) instead of localized labels | Queued |
| Minor | Hidden items of a grouped field cannot be expressed in XLSX (Excel hides group items, our keys are dates with year) | Declared (XLSX030 still counts such fields) |

## What was built

- `ledger.pivot.edit` (Data › Pivot table, ribbon Insert › Tables, cell context menu; enabled with the cursor inside a
  pivot's output). It opens the creation dialog seeded from the pivot (`PivotLayoutModel(columns, definition)`): the
  fields keep every setting, calculated fields are kept after the placed value fields, the destination is fixed. The
  confirmation applies the edited definition through `IPivotCoordinator.Apply` inside one workbook checkpoint: one
  undo step; a cancelled or unchanged edit adds none. Protected destination sheets and missing sources are refused
  with a status message.
- `PivotFieldSettingsPanel` (shared by creation and editing): field picker; for value fields show values as, base
  field (enabled for the modes that need one) and number format; for row and column fields date grouping, number range
  size, sort (labels A–Z / Z–A, or by any value field either way), subtotals, Top/Bottom N by a value field; for every
  axis field the items shown. Table options: layout (compact, outline, tabular), grand totals for rows and columns,
  repeat item labels. `PivotChoice` carries `PivotTableOptions`, applied on create and edit.
- `IPivotPreviewer.Items` lists a field's items through the engine with its grouping, so items follow the grouping.
- Texts in the five languages (56 keys).

## XLSX grouping

`XlsxPivotGroupWriter` plans, per grouped row or column field whose source values are all numbers (all valid date
serials for date groupings) and that hides no items:

- numeric ranges: `fieldGroup base` with `rangePr startNum/endNum/groupInterval` and `groupItems` (`<start`, the bins,
  `>end`) on the base cache field;
- dates: the chosen level on the base cache field and derived cache fields (`databaseField="0"`, `fieldGroup base`)
  for the coarser levels that make our keys unique: Days for groupings finer than a day, then Years; months, quarters
  and days are therefore written with their years, as Excel does when grouping months across years. `par` links each
  level to the next coarser one; the axis lists the coarsest level first; the base field's shared items become date
  items (`containsDate`, `minDate`/`maxDate`).

The reader already merged such levels back into the finest grouping (round 264), so the round trip is exact.
`CheckFidelity` now counts in XLSX030 only grouped fields that cannot be written (text values, filter area, hidden
items). Not yet opened in Excel.
