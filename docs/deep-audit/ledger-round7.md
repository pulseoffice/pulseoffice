# Deep audit, round 7: Ledger — what round 6 did not read

Scope: the `Ledger*Commands` files listed as unread in `ledger-commands-round6b.md`, the 22 remaining views in
`DataDialogs/`, `Conditional/ConditionalRuleCatalog`, `ConditionalRuleEditorView`, `ConditionalRulePreview`, plus the
minor points noted in round 6.

## Coverage

Every file was read in full with the Read tool.

| File | Lines | Outcome |
|---|---|---|
| LedgerCalculationCommands.cs | 164/164 | clean |
| LedgerCellFormatCommands.cs | 209/209 | fixed (menu short date ignored the locale) |
| LedgerCellShiftCommands.cs | 92/92 | fixed (data pushed off the sheet) |
| LedgerChangeCommands.cs | 93/93 | clean |
| LedgerCommentCommands.cs | 133/133 | clean |
| LedgerDataCommands.cs | 181/181 | fixed (remove duplicates deleted whole rows) |
| LedgerDocumentExtraCommands.cs | 148/148 | note |
| LedgerDrawCommands.cs | 75/75 | clean |
| LedgerExternalDataCommands.cs | 102/102 | fixed (sheet and history read after the await) |
| LedgerFormDialogCommands.cs | 100/100 | clean |
| LedgerFormatDialogCommands.cs | 30/30 | clean |
| LedgerFunctionCommands.cs | 64/64 | clean |
| LedgerInsertCommands.cs | 150/150 | fixed (hyperlink erased the formula) |
| LedgerMacroCommands.cs | 37/37 | clean |
| LedgerMergeCommands.cs | 61/61 | clean |
| LedgerNameCommands.cs | 202/202 | note |
| LedgerObjectCommands.cs | 134/134 | clean |
| LedgerObjectFileCommands.cs | 138/138 | note |
| LedgerPaneCommands.cs | 98/98 | clean |
| LedgerPanelCommands.cs | 32/32 | clean |
| LedgerPasteCommands.cs | 85/85 | clean |
| LedgerPrintCommands.cs | 131/131 | clean (margins already checked at confirm) |
| LedgerRedactionCommands.cs | 95/95 | clean |
| LedgerScriptCommands.cs | 54/54 | clean |
| LedgerSelectionCommands.cs | 98/98 | clean |
| LedgerSelectionExtraCommands.cs | 218/218 | note |
| LedgerShellViewCommands.cs | 59/59 | clean |
| LedgerSparklineCommands.cs | 138/138 | fixed (one sparkline per row of a whole column) |
| LedgerStructureCommands.cs | 109/109 | clean (snapshots already restore other sheets' formulas) |
| LedgerTableInsertCommands.cs | 114/114 | clean |
| LedgerTextCommands.cs | 77/77 | clean |
| LedgerViewCommands.cs | 44/44 | clean |
| LedgerWebCommands.cs | 75/75 | clean |
| LedgerZoomCommands.cs | 30/30 | clean |
| DataDialogs (AdvancedFilter, CellGridSketch, Consolidate, CustomSortOrders, FillSheets, FilterMenuModel, LabelsView, LedgerDialogKit, LedgerDialogProbes, LedgerDialogView, LedgerSortServices, MultipleOperations, NameManager, PasteSketchView, PasteSpecialModel, PasteSpecialView, ScenarioView, SeriesView, ShuffleView, SketchCells, StatisticsView, ValidationMessagePanes) | all | notes |
| Conditional/ConditionalRuleCatalog.cs | 167/167 | clean |
| Conditional/ConditionalRuleEditorView.cs | 413/413 | note |
| Conditional/ConditionalRulePreview.cs | 247/247 | clean |

`LedgerDocumentOperationsCommands` does not exist (the class is `LedgerDocumentOperations`, outside this list).

## Defects

### Serious

1. **Remove duplicates deleted whole rows.** `LedgerDataCommands.RemoveDuplicates` (was line 124) called
   `ISheetStructure.DeleteRows` for every duplicate block, so with a selection of column A the data in every other
   column of those rows was deleted too. It now shifts only the selected columns up (`ISheetCellShift.Delete` with
   `CellShift.Down`) inside one workbook checkpoint. Test `RimuovereIDuplicatiNonCancellaLeColonneFuoriDallaSelezione`.
2. **Insert cells pushed data off the sheet.** `SheetCellShift.Move` skipped cells whose target fell outside the sheet
   but still moved the others over them: inserting cells (shift down/right, or whole rows/columns) with data in the
   last rows/columns silently overwrote and lost data. New `ISheetCellShift.CanInsert` checks that the spill area is
   empty; `LedgerCellShiftCommands` refuses with `status.cells.wouldSpill` (five languages).
   Test `InserireCelleCheSpingonoDatiOltreLaFineDelFoglioVieneRifiutato`.
3. **Text to columns wrote locked cells to the right.** `LedgerToolCommands.TextToColumnsAsync` checked only the
   selection; it now also checks the bounding range of the planned values. Test `TestoInColonneNonScriveNelleColonneBloccateADestra`.
4. **Data form overwrote the row below the data.** `LedgerSheetExtraCommands.FormAsync` wrote new records after the
   last row without checking it was empty (explicit selection, or a second new record after the region). It now
   refuses with `dataForm.rowOccupied`, and refuses rows beyond the sheet with `dataForm.sheetFull`.
   Test `IlModuloDatiNonScriveUnNuovoRecordSopraUnaRigaOccupata`.

### Medium

5. **Inserting a hyperlink on a formula cell replaced the formula with text.** `LedgerInsertCommands.HyperlinkAsync`
   always wrote `Value = Text(choice.Text), Formula = null`, also turning numbers into text. The value is now rewritten
   only when the user changed the text. Test `UnCollegamentoSuUnaFormulaNonNeCancellaLaFormula`.
6. **Enter in the conditional rules manager was handled after the editor closed.** The `async` key handler set
   `e.Handled` after `await`, so Enter bubbled to the dialog (which could confirm it) and a second Enter opened a
   second editor. Handlers are now synchronous, mark the key handled at once and launch one action at a time; a
   faulted action is rethrown on the dispatcher instead of being lost. Test (Desktop) `Invio_sul_gestore_delle_regole_e_gestito_subito_e_non_apre_due_editor`.
7. **Import of external data read the sheet and history after the await.** `LedgerExternalDataCommands.ImportAsync`
   took `Context.SheetIndex` and `Context.History` after the query ran: switching sheet or document meanwhile restored
   one sheet's snapshot onto another or pushed the step into the wrong document. They are captured before the await,
   and undo finds the sheet by reference.
8. **Sparklines from a whole column.** `LedgerSparklineCommands.InsertAsync` created one sparkline per row of the data
   range (a million for `A:A`). The data range is bounded by the used area and the count by the destination size.
9. **Sheet names longer than 31 characters.** `LedgerPartialCommands.Unique`, `LedgerSheetExtraCommands.Unique` added
   " (n)" without a limit, and rename did not check the length at all. Restructured: `IWorksheetTabPolicy` now owns
   `MaxNameLength`, `NameAllowed` (length, forbidden characters, leading or trailing apostrophe) and `UniqueName`
   (truncates the stem, surrogate-safe); `SheetMovePlan` uses it instead of its own copy of the rule, rename uses it
   too. Test `INomiDiFoglioGeneratiRestanoEntroTrentunoCaratteri`.
10. **Images and SVG read without a size cap.** New shared `IChosenFileReader` (`PulseOffice.UI.Controls.Objects`,
    64 MB images, 16 MB vector) used by `RasterImageFiles.PickAsync` (all three modules) and by
    `LedgerShapeCommands.InsertRasterAsync`/`InsertVectorAsync`; too large reports `status.image.tooLarge`.
    Test `ILettoreDiFileRifiutaUnFileOltreIlTetto`. The two Deck readers in `DeckSlideCommands` and
    `DeckExtraInsertCommands` still read without a cap: outside this area.
11. **Goal seek preview solved on every keystroke.** `GoalSeekView` now validates at once and runs the solve through
    `IDebounceTimerFactory` (250 ms); `Solution` flushes a pending solve.
12. **Menu short date ignored the locale.** `ledger.number.date` applied `dd/mm/yyyy`; it now uses `ISystemDateCode`
    like the ribbon.

### Minor

13. **Sort refusal said "the cell is protected".** One `ISheetSortGate` (`SheetSortGate`) for the three sort paths
    (`LedgerEditDataCommands`, `LedgerToolCommands`, `LedgerFilterCommands`), with `status.protection.sort` and
    `status.protection.sortLocked`; the AutoFilter menu sort now also checks locked cells.
    Test `IlRifiutoDellOrdinamentoDiceCheSiTrattaDellOrdinamento`.
14. **Duplicate order commands.** `ledger.object.toForeground`/`toBackground` removed from `LedgerMiscCommands` (and
    their texts); `ledger.object.toFront`/`toBack` in `LedgerShapeCommands` remain.
15. **Page margins larger than the paper.** The command already refused them at confirm (`status.page.marginsTooLarge`),
    losing the whole dialog; the margins tab now clamps each margin so at least 10 mm of page remain.

### Noted, not changed

- `LedgerObjectFileCommands.OpenAsync` indexes `Diagnostics[0]` on failure without checking the list is not empty.
- `LedgerDocumentExtraCommands.LinksAsync` passes `default` tokens to the link dialogs and labels image link steps
  with a Deck text key (`deck.action.links`).
- `LedgerNameCommands`: defining or managing names only refreshes the view; formulas using a newly defined name are
  not recalculated until the next recalculation.
- `LedgerSelectionExtraCommands.Restrict` enumerates every cell of the used area (slow on very large sheets).
- `SheetAdvancedFilterProbe` and the consolidation preview run on the full ranges at every keystroke.
- `ConditionalRuleEditorView.SwitchKind` keeps the rule per kind but not the typed operands.
- `LedgerCommandsRound6Tests.LaTabellaPivotNonCreaUnFoglioConUnNomeNonValido("a/b")` failed once in a full run of
  the project and passed in the next runs: an intermittent test, unrelated to this round.
- Undo of structure changes: checked and correct, `SheetSnapshot` already restores the other sheets' formulas.

## Files changed

- New: `src/PulseOffice.Sheets.UI/SheetSortGate.cs`, `src/PulseOffice.UI.Controls/Objects/ChosenFileReader.cs`,
  `tests/PulseOffice.Sheets.UI.Tests/LedgerRound7AuditTests.cs`
- `src/PulseOffice.Sheets.UI/`: `WorksheetTabPolicy.cs`, `DataDialogs/SheetMoveView.cs`, `DataDialogs/GoalSeekView.cs`,
  `Conditional/ConditionalRulesManagerView.cs`, `PageLayout/SheetPageSetupView.cs`, `LedgerPartialCommands.cs`,
  `LedgerSheetExtraCommands.cs`, `LedgerSheetCommands.cs`, `LedgerMiscCommands.cs`, `LedgerShapeCommands.cs`,
  `LedgerToolCommands.cs`, `LedgerFilterCommands.cs`, `LedgerEditDataCommands.cs`, `LedgerAnalysisCommands.cs`,
  `LedgerCellShiftCommands.cs`, `LedgerDataCommands.cs`, `LedgerExternalDataCommands.cs`, `LedgerInsertCommands.cs`,
  `LedgerSparklineCommands.cs`, `LedgerCellFormatCommands.cs`
- `src/PulseOffice.Sheets.Engine/Operations/SheetCellShift.cs`, `src/PulseOffice.UI.Controls/Objects/RasterImageFiles.cs`
- Outside the area, minimum needed for DI: `src/PulseOffice.Desktop/Composition/LedgerCoreComposition.cs`,
  `ObjectsAndScriptingComposition.cs`
- Texts in the five languages: `status.protection.sort`, `status.protection.sortLocked`, `status.image.tooLarge`,
  `status.cells.wouldSpill`, `dataForm.rowOccupied`, `dataForm.sheetFull`; removed the two duplicate command titles
- Tests updated for constructors: `LedgerCommandAuditTests.cs`, `LedgerCommandsAuditTests.cs`, `LedgerCommandsAuditA2Tests.cs`,
  `LedgerProtectionOutsideHistoryTests.cs`, `LedgerRound3AuditTests.cs`, `LedgerRound6bAuditTests.cs`,
  `UI.Controls.Tests/Objects/DeepAuditCompressTests.cs`, `DeepAuditImagePickTests.cs`, Desktop `LedgerDataDialogsTests.cs`,
  `LedgerAnalysisDialogsTests.cs`, `LedgerFormatSurfacesTests.cs` (new test)

## Tests

Every new test was run against the code with its fix disabled and failed (8 in Sheets.UI.Tests, 1 in Desktop.Tests).
Run: `PulseOffice.Sheets.UI.Tests` (390/390), `PulseOffice.Desktop.Tests` filtered on the three Ledger dialog classes
(31/31), `PulseOffice.UI.Controls.Tests` filtered on `Objects` (21/21), `PulseOffice.Architecture.Tests` (38/38),
`PulseOffice.Localization.Tests` (32/32), `PulseOffice.Sheets.Engine.Tests` filtered on shift/duplicate/structure (37/37).

## Live checks

- Select A1:A3 with duplicates and other data in column C, Data > Remove duplicates: column C stays as it was.
- Put a value in the last row, select A1, Insert cells, shift down: refused with the spill message.
- Protect a sheet with A1 unlocked and B1 locked, Text to columns on A1 with `x;y`: refused, B1 unchanged.
- Data form on an explicit selection whose next row has data, add a record: refused.
- Insert a hyperlink on a formula cell without changing the text: the formula stays.
- Conditional > Manage, press Enter on a rule: the editor opens once and the manager stays open.
- Insert a sparkline from `A:A` to a single cell: one sparkline, no freeze.
- Move or copy a sheet with a 31-character name: the proposed copy name stays within 31 characters; renaming a sheet
  to 32 characters is refused.
- Insert an image or SVG larger than the cap: the "larger than … MB" message.
- Goal seek: typing in the fields stays fluid; the result appears after a short pause.
- Format > Number > Date with an English (US) system locale; sort on a protected sheet without sort permission: the
  sort-specific message.
- Page setup: type a 200 mm left margin on A4: it is clamped so the page keeps at least 10 mm.
