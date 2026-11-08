# Deep audit, round 3: Ledger UI and Deck

Completes the re-readings left open by `ledger-ui-round2.md` and `deck-round2.md`. Changes are uncommitted in the
worktree.

## Coverage

| File | Read | Outcome |
|---|---|---|
| Sheets.UI/SheetGridView.cs | 465/465 | ok; painting loop allocates nothing per cell (themed brushes resolved once per frame) |
| Sheets.UI/GridParts/GridViewport.cs | 20-165 of 409 (filter, frozen panes, geometry) | **fixed** with LedgerFilterCommands (below) |
| Sheets.UI/LedgerViewParts/LedgerCellWriter.cs | 167/167 | ok |
| Sheets.UI/LedgerViewParts/LedgerCellOperations.cs | 170/170 | ok |
| Sheets.UI/LedgerViewParts/LedgerObjectEditing.cs | 338/338 | **fixed** (medium): form-control macros ran before the change |
| Sheets.UI/Exchange/LedgerExchange.cs | 346/346 | **fixed** (minor): cutting objects on a protected sheet was a silent copy |
| Sheets.UI/Exchange/SheetRangeCapture.cs | 124/124 | ok (round 2 bound intact) |
| Sheets.UI/LedgerFilterCommands.cs | 199/199 | **fixed** (medium): undo/redo of a filter hid rows on the active sheet |
| Sheets.UI/Conditional/ConditionalRulesManagerView.cs | 254/254 | not a defect: async handlers are covered by `InterfaceFaultGuard` (dispatcher exceptions absorbed and reported) |
| Sheets.UI/LedgerTableLooks.cs | 47/47 | not a defect: a table look replaces the font weight, as the reference suite's AutoFormat does |
| Sheets.UI/SheetImageInsertion.cs | 93/93 | ok (round 1 fix intact) |
| Sheets.UI/SpreadsheetSnapshotProvider.cs | 42/42 | ok (round 1 fix intact) |
| Sheets.UI/DataDialogs/RandomView.cs | 192/192 | **fixed** (minor) |
| Sheets.UI/ShapeText/ShapeTextCellStyle.cs | 103/103 | **fixed** (minor, rule 1): parameterless constructor bypassing DI removed |
| Sheets.UI/PageLayout/SheetPageSetupView.cs | lines 200-240, 329-338 | **fixed** (minor): «Fit» with 0 × 0 pages |
| Presentations.UI/DeckStageKeys.cs | 117/117 | ok (round 1 single-step delete intact; read-only guarded by the recorder) |
| Presentations.UI/DeckColorBar.cs | 80/80 | ok (round 1 single step intact) |
| Presentations.UI/DeckObjectInsertion.cs | 134/134 | ok (Guid ids intact); minor remaining: the formula prompt is not closed when focus leaves it |
| Presentations.UI/DeckRulers.cs | 167/167 | ok (clamp fix intact) |
| Presentations.UI/DeckMediaFiles.cs | 38/38 | ok; the opener accepts only http/https |
| Presentations.UI/DeckCommands.cs | 35-80 (media playback) | **fixed** (minor): failed opening was silent |
| Presentations.UI/DeckEditCommands.cs | 120-257 of 257 | ok |

Not re-read in this round (remaining): `DeckArrangeCommands`, `DeckConvertCommands`, `DeckElementActions`,
`DeckExtraInsertCommands`, `DeckInsertCommands`, `PrintMasterView`, the first 120 lines of `DeckEditCommands`,
`LedgerCellEditing`, `LedgerShapeTextSession`.

## Defects

### Medium: undoing a filter while another sheet is active hid rows on that sheet

`LedgerFilterCommands.Reapply` / `ApplyFilters` (and `ScriptSheetEdits.Restore`) computed the rows to hide for the
filtered sheet but handed them to `IActiveSheetContext.ApplyRowFilter`, which applied them to the *active* sheet
(`GridViewport.ApplyRowFilter` used `Sheet`). ⌘Z of a filter step after switching sheet hid rows of the wrong sheet
(saved with the file) and left the filtered sheet unchanged; the macro undo skipped the refresh instead.
Fix (restructured): `ApplyRowFilter(Worksheet sheet, hiddenRows)` on the context, the target delegate, the grid and
the viewport, which keeps the filtered rows per sheet already; every caller passes its sheet.
Test: `LedgerRound3AuditTests.UndoingAFilterFromAnotherSheetHidesRowsOnTheFilteredSheetOnly` (failed before).

### Medium: form-control macros ran before the change was applied

`LedgerObjectEditing.ApplyFormControl` started `RaiseFormEventsAsync` before replacing the control and writing the
linked cell. When the macro ran synchronously it saw the old value, and if it added or removed objects the stored
index then overwrote another object. The event is now raised after the change (outside its history step).
Test: `LedgerViewPartsAuditTests.Form_control_macros_run_after_the_change_is_applied` (failed before).

### Minor

- `RandomView.cs:134-158`: an invalid number in a hidden field (Trials, Probability) blocked confirmation; a huge
  Trials overflowed the `int` cast. Hidden parameters now use their defaults; the cast is clamped. Chart colours are
  themed tokens instead of fixed white/grey. Test: `Random_numbers_ignore_invalid_values_in_hidden_parameters`
  (Desktop.Tests, failed before).
- `SheetPageSetupView.cs:214`: «Fit» with 0 pages wide and 0 tall set nothing and reopened as «Percentage»; now one
  page wide. No automatic test (view).
- `ShapeTextCellStyle.cs:63`: constructor creating its own `ShapeTextEditing`; removed, `LedgerView` shares one
  instance.
- `LedgerExchange.cs:129`: ⌘X on objects of a protected sheet copied silently; now it says why.
- `DeckCommands.cs:47-58`: a linked media whose opening failed (non-http address, launcher error) did nothing; now a
  warning `deck.media.openFailed` (five languages). No automatic test (command set with many dependencies).

## Tests run

`PulseOffice.Sheets.UI.Tests` 357/357, `PulseOffice.Presentations.UI.Tests` 60/60, `PulseOffice.Desktop.Tests`
filtered on Ledger and Deck 108/109 (1 skipped), `PulseOffice.Localization.Tests` 32/32,
`PulseOffice.Architecture.Tests` 38/38.

## To test live

- Filter a column on sheet 1, clear the filter, go to sheet 2, ⌘Z: rows of sheet 2 stay visible; back on sheet 1 the
  filter is back.
- A button or check box with a macro that reads its linked cell: the macro sees the new value.
- Random numbers: uniform distribution with Trials emptied still confirms.
- Page setup, «Fit» with 0 and 0: reopening shows «Fit», 1 page wide.
- Cut a shape on a protected sheet: status message.
- Slide show, linked media with an `ftp:` address: warning in the status bar.

## Part 2

### Coverage

| File | Lines read | Outcome |
|---|---|---|
| Presentations.UI/DeckArrangeCommands.cs | 356/356 | ok |
| Presentations.UI/DeckConvertCommands.cs | 154/154 | ok (bitmap side capped at 4096) |
| Presentations.UI/DeckElementActions.cs | 368/368 | ok (every change is one recorder step) |
| Presentations.UI/DeckExtraInsertCommands.cs | 128/128 | ok; minor remaining: photo album files are read whole with no size cap |
| Presentations.UI/DeckInsertCommands.cs | 385/385 | ok |
| Presentations.UI/DeckEditCommands.cs | 257/257 | ok |
| Presentations.UI/PrintMasterView.cs | 217/217 | ok; minor remaining: page edge colour is fixed (paper preview) |
| Presentations.UI/DeckObjectInsertion.cs | 134/134 | **fixed** (minor): formula prompt |
| Sheets.UI/LedgerViewParts/LedgerCellEditing.cs | 393/393 | **fixed** (minor): Enter in the formula bar |
| Sheets.UI/LedgerViewParts/LedgerShapeTextSession.cs | 136/136 | ok |
| Sheets.UI/GridParts/GridViewport.cs | 410/410 | **fixed** (medium): snap edges; minor remaining: `Geometry` recomputes the row header width on every access |

### Defects

- **Medium** `GridViewport.CellEdges` (Alt + drag of an object): with the rows (or columns) below the used area hidden, a
  common way to tidy a sheet, every hidden row has height zero, so the loop never passed the bottom of the view and
  walked all 1,048,576 rows on every pointer move, returning a million duplicate snap lines. The edges are now collected
  once each and the walk stops when the rest of the sheet is hidden.
  Test: `GridAndDataAuditTests.Cell_snap_edges_stop_at_the_last_visible_row_and_column_when_the_rest_is_hidden` (failed before).
- **Minor** `DeckObjectInsertion.PromptText`: the formula prompt stayed open when focus left it, and running the command
  again stacked a second prompt. It now closes when focus leaves it (not for its own context menu), and a new prompt
  replaces the open one. Test: `DeckFormulaPromptTests` (Desktop.Tests, failed before).
- **Minor** `LedgerCellEditing.OnFormulaKeyDown`: Enter in the formula bar rewrote the cell even when nothing had been
  typed; on a locked cell of a protected sheet it reported «locked» just for moving on. It now commits only an edit.
  Test: `LedgerEditorTests.Enter_in_the_formula_bar_without_changes_on_a_locked_cell_moves_on_without_an_error` (failed before).

### Unstable test

Five runs of the whole `PulseOffice.Desktop.Tests` project (263/264 each, then 265/266 with the new tests) did not
reproduce the failure. Reading the project found two waits that depended on wall-clock timing:
`FolioAccessibilityTests` waited at most 500 ms for a dialog to close (`Task.WhenAny(running, Task.Delay(500))`) and then
asserted the focus anyway; `LedgerEditorTests.Trascinare_un_file_sulla_finestra_lo_apre` spun 200 dispatcher passes
with no pause for the asynchronous storage lookup. Both now poll the dispatcher with a pause up to a 10 s bound and assert
the completion explicitly, so a slow machine waits instead of failing.

### Tests run (part 2)

`PulseOffice.Desktop.Tests` 265/266 (1 skipped), `PulseOffice.Sheets.UI.Tests` 358/358,
`PulseOffice.Presentations.UI.Tests` 60/60, `PulseOffice.Architecture.Tests` 38/38.

### To test live

- Deck, Insert formula, then click the slide: the prompt closes; running it twice shows one prompt.
- Ledger, protected sheet, click the formula bar on a locked cell and press Enter: no error, the selection moves down.
- Ledger, hide all rows below 30, Alt + drag a shape: snapping is fluid.
