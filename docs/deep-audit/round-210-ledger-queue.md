# Round 210: Ledger audit queue (data tools and structured table references)

Scope: the open Ledger items of rounds 206 and 207. Not built or run in this round; the coordinator builds and tests
after the join.

## Fixed

1. **Whole-column scenarios left late cells behind.** Capture keeps only the used part of the range, so a cell filled
   beyond it after capture kept its value when the scenario was shown. `ScenarioManager.Apply` now also visits the
   part of the scenario range inside the current used area, clearing cells the scenario does not hold; the previous
   values (returned for undo) include them. Test: `Round210ScenarioTests`.
2. **Showing a scenario on a protected sheet relied on the effect guard.** The scenarios command now checks
   `CanWrite` on the scenario range before applying and reports `status.protection.locked` with the unprotect remedy.
   (Committed together with item 1.)
3. **XLSX relative-period dynamic filters were dropped or frozen.** Today, yesterday, tomorrow, this/last/next week,
   month, quarter and year, and year to date are now `ColumnFilter.Period` (`RelativeDatePeriod`), evaluated by
   `SheetFilterService` against its `Clock` (`RelativeDatePeriods` computes the bounds; weeks start on Sunday, as in
   Excel), whether or not the file carries `val`/`maxVal`. XLSX writes the `dynamicFilter` type with today's bounds as
   `val`/`maxVal`; ODS, which has no relative filter, writes two bound conditions with the
   `urn:pulseoffice:spreadsheet` `relative-period` attribute, read back first, so other readers see the bounds of the
   save date. The round 205 test that expected stored bounds now expects the period. Test:
   `Round210RelativeDateFilterTests`.
4. **Remove duplicates had no case option.** The dialog has a "Match case" check (five languages) that sets
   `DuplicateRemovalRequest.CaseSensitive`. Test: `Round210DuplicateTests`.
5. **No command renamed a table.** `ledger.database.rename` (Data › ranges menu, Insert › Tables on the ribbon, icon,
   five languages) asks for the table, preset from the selection, and the new name; the name is checked with
   `IDefinedNameRules` and against existing ranges; the rename is one undo step that renames back. Test:
   `Round210RenameTableTests`.
6. **`[#This Row]` outside the data rows gave #REF!.** It now gives #VALUE!. Test: `Round210TableReferenceTests`.
7. **Table references outside cells were not followed.** `TableReferenceOwners` rewrites conditional-format formulas,
   validation formulas and defined-name expressions on table renames and header renames. ODS writes the resolved form
   for other readers and keeps the structured form in the same `table-formula` extension attribute (plus
   `table-second-formula` for a validation's second formula) on named ranges and expressions, conditions and content
   validations. Tests: `Round210TableReferenceTests`, `Round210TableReferenceOdsTests`.
8. **The formula bar showed `[[#This Row],[Column]]`.** `FormulaTextTranslator.ToLocal` prints a this-row reference to
   one column in the short form `Table[@Column]` (`Table[@[Unit Price]]` when the name needs brackets). Test:
   `Round210FormulaDisplayTests`.
9. **Header renames were not followed in manual calculation.** The engine follows column renames before checking the
   calculation mode, so formula text is rewritten at once and only the values wait for the recalculation.
   `WorkbookEvaluationContext` moved to its own file.

## Open

Listed under «Ledger» in docs/audit-queue.md (round 210).
