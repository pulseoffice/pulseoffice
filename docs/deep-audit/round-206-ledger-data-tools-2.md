# Round 206: Ledger data tools, second pass

Area: the open items of round 205 (remove duplicates, text to columns, AutoFilter sort memory, XLSX custom orders and
dynamic filters, the sort gate), then scenarios, consolidation, the solver and subtotals next to filters.

## Defects fixed

1. **Medium: remove duplicates had no dialog.** It always compared every column and treated the first row as data, so
   a header row could be removed as a duplicate of nothing and a key column could not be chosen. A dialog
   (`IDuplicateRemovalPrompt`, shared `FormPromptView` with a header toggle preset by the header detector and a
   checklist of columns) now sets `HasHeader` and `KeyColumns`; choosing no column is reported. A protected sheet that
   forbids deleting rows reports the new `status.protection.deleteRows` text instead of the row and column format
   text. The removal stays one undo step. Test: `Round206DuplicateTests`.
2. **Medium: text to columns overwrote cells to the right silently.** It now counts the non-empty cells it would
   replace outside the selection and asks first (`ICellOverwritePrompt`, a `ChoicePromptView` with a veil); declining
   changes nothing. Test: `Round206TextToColumnsTests`.
3. **Medium: a left-to-right sort remembered on an AutoFilter was dropped.** `AutoFilterState.Remember` checked the
   key offsets against the column count whatever the orientation, the ODS reader and the XLSX reader kept only
   downward sorts, and the sort dialog recalled the keys as columns. The check is now orientation aware, both readers
   keep rightward sorts (`table:orientation="column"`, `sortState/@columnSort`) and recall maps rows. Follow-up found
   in the same area: row and column insertion moved a rightward AutoFilter key as a column. Tests:
   `Round206LeftToRightSortTests`, `Round206AutoFilterSortTests`.
4. **Minor: XLSX custom order items containing a comma were split on read.** `customList` is a comma-separated list,
   so an item such as "Rome, Italy" came back as two items. The writer keeps the plain list for other applications
   and adds the exact items in a `sortState` extension (`urn:pulseoffice:sort-order`); the reader uses it only when
   joining its items gives the stored `customList`, so a list edited elsewhere wins. XLSX236 still warns that other
   applications split the item. Test: `Round206CustomOrderTests`.
5. **Medium: XLSX month and quarter dynamic filters were dropped.** They carry no stored bounds and match that month
   or quarter of any year, which no single pair of bounds and no ODF filter condition expresses. They are now read as
   the value set of the column's dates that fall in the period, blanks excluded, the same way date group items are
   read; the filter becomes static, as the bounded periods of round 205 already are, and crosses to ODS as a value
   set. Computing bounds from today was rejected because it would keep only the current year. Test:
   `Round206DynamicFilterTests`.
6. **Medium: the sort gate and the sorter disagreed.** The gate always checked merges as if the first row were a
   header and arrays as if it were not, before the dialog had asked; the sorter then refused silently. The gate now has
   a check before the dialog (protection only) and the full check with the chosen header and orientation after it,
   used by the sort dialog, the AutoFilter menu sort and the quick sort, so every refusal is reported. Test:
   `Round206SortGateTests`.
7. **Medium: a scenario over whole columns visited every cell of the range.** Capture iterated over a million rows
   per column. It now captures the part of the range inside the used area. Test: `Round206ScenarioTests`.
8. **Medium: the solver reported an unreached target value as solved.** With the goal "value of", any feasible end
   point counted as success. Success now requires the objective within a relative 1e-6 of the target; otherwise the
   reason is `target` and the command reports no solution. Test: `Round206SolverTests`.

## Reviewed without defects

Consolidation by position and by labels (case-insensitive label matching, link formulas with canonical separators,
the combiners), subtotal insertion and replacement next to an AutoFilter (the filter range follows inserted rows,
`SUBTOTAL` ignores filtered rows), the scenario dialog's name checks. Flash fill does not exist in Ledger.

## Open items

Listed in `docs/audit-queue.md`, Ledger section, round 206.
