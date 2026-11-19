# Round 264: Ledger pivot leftovers from round 261

Area: the open items of round 261, in the priority order set for this round. Not built or run here (the coordinator
builds and tests at the join).

## Defects fixed

1. **Medium: % of parent was in the model and read from files but computed as Normal.** The engine now divides each
   row by its parent row total (the grand total for the outermost level), as Excel's "% of Parent Row Total".
2. **Medium: % of parent, % running total and rank were not written to either format** (declared XLSX030 and ODF024).
   XLSX: the x14 `dataField pivotShowAs` extension (percentOfParentRow, percentOfRunningTotal, rankAscending,
   rankDescending) in the data field extension list, written and read (percentOfParent and percentOfParentCol read as
   % of parent). ODS: ODF 1.3 `data-pilot-field-reference` has no such types, so our `urn:pulseoffice:pivot` attributes
   `display` and `base-field` on `table:data-pilot-field`; other readers see a plain value. Both declarations no longer
   cover display modes.
3. **Medium: the base field was fixed to the first row field.** `PivotField.BaseField` (source name) is model data. The
   engine uses it for difference, % difference (previous item at that level, inside the same outer items) and for
   running total and % running total (restarted inside each combination of the outer items). XLSX `baseField` and ODS
   `table:field-name` (standard modes) or `pulsepivot:base-field` (extended ones) are written from it and read back.
4. **Medium: value field number formats were not modelled.** `PivotField.NumberFormat` (format code) styles the value
   cells when rendered (it replaces the automatic percent format). XLSX: `dataField numFmtId`, built-in ids for
   built-in codes, custom codes declared in the stylesheet by the pivot writer through `XlsxStyleMapper`
   (`DeclareNumberFormat`), read back from the stylesheet. ODS: ODF has no number style for data pilot fields, so our
   `pulsepivot:number-format` attribute.
5. **Minor: boolean items were keyed "VERO"/"FALSO" in every language.** Keys are now "TRUE"/"FALSE" (one
   `PivotValues.KeyOf` for the engine and the XLSX mapper), and item cells hold real booleans, so they display in the
   sheet's language.
6. **Minor: date group labels were not localized.** Keys stay invariant ("2024-T1", "2024-03", "2024-03-15") for
   sorting, files and GETPIVOTDATA; the rendered labels use the interface language: quarters through the new
   `pivot.quarter` text (five languages), months as the abbreviated month and year, days as the short date.
   `LedgerPivotLabels` builds the labels (texts and culture) for every Ledger caller.
7. **Medium (import): XLSX grouping was ignored on reading.** `cacheField/fieldGroup/rangePr` now gives numeric range
   intervals and date groupings; Excel's derived date levels (a years or quarters field over the same column) merge into
   the one field with the finest grouping, since our date keys already include the year.

Tests: `tests/PulseOffice.Sheets.Pivot.Tests/Round264PivotTests.cs`,
`tests/PulseOffice.Formats.OpenXml.Tests/Round264XlsxPivotTests.cs`,
`tests/PulseOffice.Formats.Odf.Tests/Round264OdsPivotTests.cs`.

## Not covered (queued)

- The interface to edit an existing pivot (show values as, base field, number format, grouping, sorting, filters,
  layout, totals, repeat labels) reusing the creation dialog: not started this round. Largest remaining gap.
- XLSX grouping is still not written (XLSX030 still declared for groupings): writing needs `fieldGroup` with
  `rangePr` and `groupItems` laid out as Excel expects (months across years need a derived years field), which has to
  be checked in Excel. Excel's "Months" without a years level groups across years, ours keeps the year: reading such a
  file gives month-and-year items.
- A text item "TRUE"/"FALSE" shares the key of the boolean and renders as a boolean; subtotal labels of boolean items
  show the invariant key; GETPIVOTDATA does not accept the localized date labels ("T1 2024"), only the keys.
- Base item other than "previous" (a named item) is not modelled; Excel's percentOfParentCol reads as row parent.
- Manual groups, label and value filters, slicers, compact-layout leftovers and XLSX `rowItems`/`colItems` stay as
  queued in round 261.
