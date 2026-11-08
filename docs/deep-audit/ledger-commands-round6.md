# Deep audit, round 6: Ledger commands and dialogs

Area: `src/PulseOffice.Sheets.UI` command sets (`Ledger*Commands`), data dialogs, pivot, chart, conditional,
names, print setup and the view-event entry points of `LedgerDocumentFactory`. Focus: changes that bypass the
protection guards, operations on the wrong sheet or workbook, multi-step undo, unbounded or invalid dialog values,
swallowed exceptions, cost on large sheets, untranslated text, components duplicated across modules.

## Coverage (read in full)

| File | Lines | Outcome |
|---|---|---|
| LedgerDataDialogCommands.cs | 232 | duplicated value writer replaced by `ISheetValueWrites` |
| LedgerNameCommands.cs | 202 | no defect |
| LedgerPivotCommands.cs | 125 | defect 2 fixed |
| LedgerConditionalRuleCommands.cs | 155 | observation O1 |
| LedgerConditionCommands.cs | 129 | observation O1 |
| LedgerChartCommands.cs | 249 | defect 4 fixed (entry point), observation O2 |
| LedgerPrintCommands.cs | 131 | no defect |
| LedgerDatabaseRangeCommands.cs | 126 | observation O3 |
| LedgerDataCommands.cs | 181 | observation O4 |
| LedgerSheetToolsCommands.cs | 161 | defect 3 fixed |
| LedgerFinalCommands.cs | 477 | defects 5, 6, 7 fixed |
| LedgerSheetCommands.cs (rename, 200-270) | 379 | uses the shared sheet-name rule |
| LedgerDocumentFactory.cs | ~130 | defect 4 fixed |
| LedgerTailCommands.cs (40-200) | 571 | partial; no new defect in the part read |
| LedgerSheetExtraCommands.cs (40-200) | 351 | partial; observation O5 |
| LedgerCommandSet.cs, ActiveSheetContext.cs (formatting service), LedgerProtectionGuard.cs, WorksheetTabPolicy.cs, SheetValueWrites.cs, Scripting/DocumentFacade.cs (sheet members) | — | read as context for the fixes |

Not reached in this round: the remaining command files (Analysis, Tool, Shape, Image, Spelling, Ribbon, Partial,
Misc, Gallery, Style, Fill, Filter, …) and the view classes under `DataDialogs/`, `Conditional/`, `PageLayout/`.

## Defects

1. **Not a defect (verified)** — `LedgerDataDialogCommands.CopyResults` writes cells straight into the sheet; a test
   showed the engine observes those writes (dependents and copied formulas recalculate) and the transaction makes undo
   one step. Kept as regression tests. Structural fix: `Write` duplicated `SheetValueWrites`; it now uses the shared
   `ISheetValueWrites`.
2. **Medium** — `LedgerPivotCommands.cs:85` (destination sheet) and `Scripting/DocumentFacade.cs:88,115`
   (`AddSheet`, `RenameSheet`): a sheet name with `[ ] * ? : / \` was accepted, producing a sheet that formulas cannot
   reference and that XLSX forbids. The forbidden set lived only inside `LedgerSheetCommands` (rename). It is now
   `IWorksheetTabPolicy.ForbiddenNameCharacters` / `NameAllowed`, used by rename, pivot creation and the script facade.
   New text `status.pivot.badSheetName` in the five languages.
3. **Medium** — `LedgerSheetToolsCommands.cs:113` (`Apply`): «delete all comments», «show/hide all comments»,
   «auto outline», «clear outline» and the page-break commands changed a protected sheet. The effect guard does not
   watch comments or outline groups. Now refused at the source (objects message for comments, format message
   otherwise), as `LedgerDataCommands` already did for grouping.
4. **Medium** — `LedgerDocumentFactory.cs:115-116`: double-clicking a chart (edit) and double-clicking a sheet tab
   (rename) reached the command bodies without the command's `ForbiddenState = ReadOnly`, so a read-only document
   could be changed. Both now pass the same read-only check as the filter menu (`Editable`).
5. **Medium** — `LedgerFinalCommands.cs:96-105` (paintbrush): the copied style kept the source workbook's custom
   number-format id; painting into another document applied whatever format had that id there. The format code is
   remembered and re-registered in the target workbook.
6. **Medium** — `LedgerFinalCommands.cs` (calculated pivot field): with several pivots the chosen one may live on
   another, protected sheet; the effect guard only watches the active sheet, so locked cells were rewritten. Now refused
   when the destination sheet is protected.
7. **Minor** — same command: a failed `IPivotCoordinator.Apply` still pushed an empty undo step (no `applied`
   predicate, unlike pivot create/refresh). Fixed.

## Observations (not changed)

- O1 `LedgerConditionalRuleCommands` refuses only when `ISheetEditGuard.CanFormat` fails, `LedgerConditionCommands`
  refuses on any protected sheet: two policies for the same model object.
- O2 `LedgerChartCommands.EditSelectedChart` discards the task (`_ =`): exceptions are unobserved; it runs outside
  `CommandExecutor`, so the effect guard does not see it (the body checks protection itself). The same holds for the
  rename and filter-menu entry points. Routing view events through the executor needs a lazy executor in the factory
  (DI cycle executor → registry → commands → factory).
- O2b `InsertChart` anchors at `range.End.Column + 2` without clamping to the last column.
- O3 `LedgerDatabaseRangeCommands.DefineAsync`: the range name is not validated (ODF accepts any string; XLSX drops
  database ranges with XLSX031).
- O4 `LedgerDataCommands.Sheet` and `LedgerTailCommands.CommentChangeAsync` checkpoint the whole workbook for a print
  range or a change comment: cost proportional to all cells of all sheets.
- O5 `LedgerSheetExtraCommands.FromFileAsync` builds `new Formats.Odf.OdsReader()` instead of an injected reader; its
  sheet commands ignore the command's cancellation token.

## Tests

`tests/PulseOffice.Sheets.UI.Tests/LedgerCommandsRound6Tests.cs` (13 tests; defects 2, 3, 5, 6, 7 failed before the
fix), plus two assertions added to `LedgerFacadeContractTests` (`doc.AddSheet`, `doc.RenameSheet`, failed before).
Run: `PulseOffice.Sheets.UI.Tests` (372 passed), `PulseOffice.Architecture.Tests` (38), `PulseOffice.Localization.Tests`
(32); `PulseOffice.Desktop` builds. Defect 4 has no unit test (the factory builds the Avalonia view).

## Live checks

- Read-only document: double-click a chart and a sheet tab — nothing must open.
- Pivot table with destination `a:b` — refused with the message, no new sheet.
- Protected sheet: Sheet ▸ Comments ▸ Delete all, Data ▸ Outline ▸ Clear — refused.
- Paintbrush: copy a cell with `0.000` in one workbook, paint in another that has its own custom formats.
- Two pivots, the second on a protected sheet: add a calculated field to it — refused.
