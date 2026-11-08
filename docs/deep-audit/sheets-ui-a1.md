# Deep audit — sheets-ui, group A1

## Coverage

All assigned files read in full (lines at the initial state), all in `src/PulseOffice.Sheets.UI/`.

| File | Lines |
|---|---|
| LedgerAnalysisCommands.cs | 471 |
| LedgerCalculationCommands.cs | 164 |
| LedgerCellFormatCommands.cs | 205 |
| LedgerCellShiftCommands.cs | 92 |
| LedgerChangeCommands.cs | 92 |
| LedgerChartCommands.cs | 249 |
| LedgerCommands.cs | 24 |
| LedgerCommentCommands.cs | 133 |
| LedgerCompareCommands.cs | 117 |
| LedgerConditionCommands.cs | 129 |
| LedgerConditionalRuleCommands.cs | 157 |
| LedgerDataCommands.cs | 167 |
| LedgerDataDialogCommands.cs | 234 |
| LedgerDatabaseRangeCommands.cs | 126 |
| LedgerDocumentExtraCommands.cs | 156 |
| LedgerDrawCommands.cs | 75 |
| LedgerEditDataCommands.cs | 148 |
| LedgerExternalDataCommands.cs | 102 |
| LedgerFillCommands.cs | 205 |
| LedgerFilterCommands.cs | 157 |
| LedgerFinalCommands.cs | 455 |
| LedgerFormCommands.cs | 81 |
| LedgerFormDialogCommands.cs | 100 |
| LedgerFormatDialogCommands.cs | 30 |
| LedgerFunctionCommands.cs | 64 |
| LedgerGalleryCommands.cs | 218 |
| LedgerImageCommands.cs | 336 |
| LedgerInsertCommands.cs | 147 |
| LedgerMacroCommands.cs | 39 |
| LedgerMergeCommands.cs | 61 |
| LedgerMiscCommands.cs | 238 |
| **Total (31 files)** | **4972** |

## Defects

### Severe (4)
- `LedgerMiscCommands.cs:84`: «Date field» wrote `=OGGI()` into the model, which instead uses the canonical English form. The cell gave `#NAME?` and the file contained a non-existent function. Now `=TODAY()`. **Fixed.**
- `LedgerMiscCommands.cs:187,207`: undoing the insertion of a field, a mark or a symbol restored only the cell's value and lost the formula that was there before. **Fixed**: the formula or the value is restored.
- `LedgerEditDataCommands.cs:64`: «Clear filter» unhid the rows in the view but left the filters in the model. The filters stayed saved in the file, came back on reopening and the command stayed enabled. The command was moved to `LedgerFilterCommands`, where the memento and the reapplication already are: it now empties the model's filters, with undo and redo. **Fixed.**
- `LedgerFinalCommands.cs:363`: «Delete pivot table» always deleted the last pivot, not the one under the cursor, and undo put it back at the end. It now deletes the pivot containing the active cell (the last one only if the cursor is in none) and undo reinserts it in its place. **Fixed.**

### Medium (2)
- `LedgerFilterCommands.cs:66`: the column filter menu applied filter and sort even on a protected sheet without the `AllowFilter`/`AllowSort` permissions, while the autofilter command checked them. **Fixed.**
- `LedgerInsertCommands.cs:85,118`: for date, time, sheet name and hyperlink, undo and redo rewrote the cell without recalculating, and dependent cells kept the old values. **Fixed**: they now all go through a single `Record` that recalculates.

### Minor (5)
- `LedgerMiscCommands.cs:118,183`: dead variables (`_ = sheet`, `_ = engine`). **Removed.**
- `LedgerMiscCommands.cs:182`: a mark added to a cell with a text formula was appended to the formula's result. It now starts from empty, as already for non-text values. **Fixed.**
- `LedgerConditionalRuleCommands.cs:26`, `LedgerDataDialogCommands.cs:28`: optional dependencies with `?? new …()`, i.e. instances created outside DI. **Remaining**: they are registered in the container, and removing them would touch the composition, which belongs to other agents.
- `LedgerFinalCommands.cs:114`: «Thousands separator» compares with built-in format 4 (`#,##0.00`) and otherwise goes back to 0, so it does not recognize a custom format with thousands. **Remaining.**
- `LedgerDocumentExtraCommands.cs:63`: `ChooseAsync(..., default)`: the links loop ignores the command's cancellation token. **Remaining.**

Verified not to be a defect: undo and redo without `RequestRefresh` (charts, conditional rules, objects). `LedgerView` redraws everything at every `History.Changed`.

## Tests
`tests/PulseOffice.Sheets.UI.Tests/LedgerCommandAuditTests.cs` (6 tests).

## To try live
- Insert › Field › Date: the cell shows the date, not `#NAME?`; ⌘Z restores the previous formula.
- Data › Clear filter with an active autofilter: the arrows stay and the filters disappear; saving and reopening they do not come back; ⌘Z restores them.
- Two pivots in the same sheet, cursor on the first, «Delete pivot»: the first one disappears.
- Protected sheet without filter permission: the filter arrow menu refuses the operation with the protection message.
