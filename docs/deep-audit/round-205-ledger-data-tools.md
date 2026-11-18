# Round 205: Ledger data tools

Area: sort, AutoFilter and advanced filter, remove duplicates, text to columns, goal seek, data tables, subtotals,
and how filter and sort state cross XLSX (`autoFilter`, `sortState`, `filterColumn`) and ODS (`table:database-range`,
`table:filter`, `table:sort`).

## Defects fixed

1. **Medium: sorting scrambled merged cells.** `SheetSorter` moved cell contents line by line while merged regions
   stayed in place, so a merged label took another row's value and the merged-away cell's value was hidden. Sorting is
   now refused when a merged region overlaps the data lines (`ISheetSort.BreaksMerges`); the sort gate reports it with
   the new `status.sort.merged` text in the five languages. Test: `Round205SortTests`.
2. **Minor: logicals sorted among numbers.** TRUE and FALSE compared numerically with numbers (TRUE equal to 1). The
   ascending order is now numbers, text, logicals (FALSE before TRUE), errors, blanks. Test: `Round205SortTests`.
3. **Medium: XLSX dynamic filters were dropped on read.** A `dynamicFilter` (above or below average, date periods such
   as this month or last year) left the column unfiltered, so reapplying the filter showed every row. They are now
   read as conditions on the bounds the file stores (`val`, `maxVal`): above average becomes `>val`, below average
   `<val`, a period `>=val` and `<maxVal`. The filter becomes static, as the bounds were at save time. Month and quarter
   filters, which carry no bounds, remain open. Test: `Round205FilterTests`.
4. **Medium: goal seek failed on large targets.** Convergence used an absolute tolerance of 1e-9, below the floating
   point resolution of targets above about 1e7, so the search could run out of iterations and step away from the
   solution. The tolerance is now relative to the target's size (`GoalSeekRequest.Reaches`). Test:
   `Round205GoalSeekTests`.

## Reviewed without defects

XLSX and ODS sort state codecs (keys, descending, case, orientation, custom lists), XLSX data table reading and writing
(one- and two-dimensional, row and column input) against `MULTIPLE.OPERATIONS`, the ODS filter conditions for colour,
top-N, value sets and blanks, duplicate detection keys.

## Open items

Listed in `docs/audit-queue.md`, Ledger section, round 205.

Follow-up after the cycle 12 join: `BreaksMerges` looked only at the occupied part of the range, which is cut to the
last used column, so a merged region over empty cells inside the selected columns went unseen. It now checks the
selected columns (rows, for a left-to-right sort) across the occupied data lines.
