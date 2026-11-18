# Round 175: Ledger pivot tables

Area: pivot creation and dialog, fields and areas, aggregation and "show as", grouping, sorting and filters, totals,
refresh after source edits, pivots on other sheets, ODS and XLSX round trip, undo, protected sheets.

## Defects

1. **Medium: a refresh that grows the pivot overwrote cells next to it.** `PivotCoordinator.Apply` checked the
   destination for existing content only when the pivot was new; on refresh (rows added to the source, a new item) the
   larger output was written over whatever lay below or beside the previous output, silently. The check now runs on
   every apply and exempts only the cells the pivot itself owned (its previous output range on the same sheet); a
   growth into data fails with PVT005 and leaves the sheet and the old output untouched.
2. **Medium: re-applying a pivot with a different destination sheet cleared the wrong sheet.** The previous output
   range was cleared on the new destination sheet, erasing unrelated cells there and leaving the old output behind on
   the old sheet. The previous output is now cleared on the sheet that holds it.

Test: `tests/PulseOffice.Sheets.Pivot.Tests/Round175PivotRefreshTests.cs`.

## Reviewed without defects

Engine bucketing, subtotals and grand totals, "show as" passes, value sorting, ODS display-mode, grouping, sort,
subtotal and hidden-member mapping, XLSX grand totals, sort, subtotals and hidden items.

## Open

Not built or run in this round (coordinator builds and tests at the join). Mixed numeric and text item sorting uses a
comparison that is not transitive for every mix of values; not changed this round.
