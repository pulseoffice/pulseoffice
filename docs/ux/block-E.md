# Block E — Ledger: data, sheets, analysis and tools

Code in `src/PulseOffice.Sheets.UI/DataDialogs/` unless stated otherwise. Headless tests in
`tests/PulseOffice.Desktop.Tests/LedgerDataDialogsTests.cs`, `tests/PulseOffice.UI.Controls.Tests/{SortLevelsTests,SteppedDialogTests}.cs`,
`tests/PulseOffice.Sheets.UI.Tests/DataDialogModelsTests.cs`, engine in `PasteSpecialTests`, formats in the ODS/XLSX metadata round-trip tests.

## Surfaces redone (before → after)
- **Paste special**: two drop-downs → parts as icons in two columns (All, Formulas, Values, Formats, Comments, Validation),
  operation as icons (disabled when not applicable), skip blanks, transpose, "Paste link", paper preview of the
  result (`PasteSketcher`). Engine: `PasteParts.Comments/Validation/Everything`, `PasteRequest.Link`; the snapshot carries comments,
  clipped validations and source sheet. New command `ledger.clipboard.pasteLink`.
- **Quick paste in the ribbon**: "Paste options" menu (`home/clipboard/paste`) with values, formulas, formats, transposed,
  link, text; icons in `UI.Icons/DataDialogIcons`.
- **Sort…** (missing in Ledger) → shared component `UI.Controls/Sorting/SortLevelsView`: levels with add/copy/delete/up/down
  (`PanelActionBar`, Alt+arrows/Ins/Del), direction as icons, custom order (the language's days and months), detected headers,
  case sensitivity. Wired into Ledger (`ledger.data.sort`) and Deck (`deck.table.sort`); **Folio** (table Sort) is in block C: it must
  be moved to the same component.
- **Validation**: type drop-down → types as icons, list source to choose, preview of the drop-down; help balloon and
  error window in preview. Model `ShowInputMessage`/`ShowErrorAlert` in ODF (`table:display`) and XLSX
  (`showInputMessage`/`showErrorMessage`); without an alert the value goes in silently.
- **Series**: four drop-downs → direction, type and unit as icons, unit only for Date, preview of the first values from the engine.
- **AutoFilter menu**: list of check boxes → sort at the top, accent-insensitive search, values with counts, tri-state select
  all, number/text filters (`FilterCriteriaCodec`, `FilterOperator`), filter by colour, visible rows.
- **Advanced filter**: form → ranges with selector, criteria read from the sheet in preview, live count of the rows that
  pass, "filter in place / copy elsewhere" as icons.
- **Manage names**: drop-down + box → Name · Value · Reference · Scope table with search, inline editing of name,
  scope and reference (with selector), new and delete, inline validation, a single undo step.
- **Move/copy sheet**: drop-down → action as icons, list of positions, resulting tab order in preview,
  copy name validated inline.
- **Insert/delete cells**: drop-down → four choices as icons.
- **Pivot table**: rows of drop-downs → fields to tick or drag into Filters, Columns, Rows, Values (pointer
  dragging, menu and Alt+arrows on the items), value function from the menu, preview computed by `IPivotEngine`.
- **External data**: shared components `UI.Controls/Dialogs/StepStrip` and `SteppedDialog`; "Link to external data" in two
  steps (source and destination → table in preview with `TabularPreviewView`).

Shared components touched: `IconChoiceGroup` (`columns` parameter), `RangeFieldControl` (`TextChanged`, writable `Text`).
No new loss code: the only model additions (validation messages) cross both formats.

## Completion (third session)
New common base: `DataDialogs/LedgerDialogView` (Template Method: explanation, `TwoPaneDialogLayout`, red warning that disables
the main button, optional secondary button), `CellGridSketch` (cells on paper for previews), `ISketchCells`
(values → preview cells), `ILedgerDialogProbes` (factory of probes on the real sheet, for DI). Each dialog takes a probe
through an interface and is tested without the command. Tests in `tests/PulseOffice.Desktop.Tests/LedgerAnalysisDialogsTests.cs`.

- **Consolidate**: form → list of ranges with selector, eleven functions as icons, labels, destination; **result computed
  by the engine** in preview with the headers highlighted.
- **Multiple operations**: three fields → declared result range, mode as icons (by column, by row, both) that shows
  only the right inputs, result table computed in preview.
- **Fill sheets**: seven check boxes → range and sheets to tick, "everything / only some parts" as icons, source and
  destination tabs drawn in preview.
- **Define labels**: form that replaced the range → list of defined ranges (with removal), new range with direction as icons,
  preview with the labels marked; one undo step for all changes.
- **Random numbers**: eight always-visible fields → distributions as icons, only the relevant parameters, histogram of 400 draws
  and first values, checks (minimum/maximum, deviation, probability, trials).
- **Shuffle**: one field → before and after on paper.
- **Data form**: a single row → record navigator (first, previous, next, last, PgUp/PgDn), new record,
  restore, neighbouring rows in preview; only changed records are saved.
- **Scenarios**: rows in a form → list and detail (name, comment, cells), New/Duplicate/Delete/Up/Down bar with Ins, Del,
  Alt+arrows, scenario values in preview, "Show" as a secondary action. `ScenarioManagerField` removed.
- **Goal seek**: three fields → formula and current value, variable cell, **solution computed before applying** with
  targeted warnings (not a formula, the variable is a formula).
- **Statistics**: drop-down → procedures as icons, result table in preview.
- **Sheet protection**: a single password box → password and confirmation, nine permissions as check boxes (the model already had them and
  they cross ODS and XLSX); to remove protection only the password.
- **Tab colour**: dialog → popup with the palette (`ColorPaletteView`) anchored to the tab.
- **Rename**: dialog → inline editing on the tab (double click or command), with the error below the box; the dialog
  remains only if the strip is not visible. Shared component `UI.Controls/Tabs/TabOverlays` (+ `TabStrip.BoundsOf`): Folio and
  Deck have no renamable tabs, so it is wired only in Ledger.
- **Stream, XML source, providers**: single forms → `SteppedDialog` in two steps (source → table in preview), stream
  mode and provider type as icons.
- **Insert function**: list of fixed Italian names → translated categories with icon and count (`IFunctionCategoryLooks`).
- **Filter menu**: date columns are grouped by year, month and day with tri-state check boxes and expandable branches.
- **Review panel**: `LedgerReviewSource.Edit` edits the comment text with undo (previously it did nothing).

Shared components touched: `ChecklistControl.Changed`, `TabStrip.BoundsOf`, `TabOverlays` (new). No new loss
code: no surface added data to the model.

## What remains
- Nothing from the block's list. The shared Paragraph is not used in Ledger (see block D).

## To try live
All the surfaces above, in light and dark and with the keyboard; in particular dragging in the pivot, the
"Paste options" menu, inline rename (Enter, Esc, click outside), the colour popup on the tab, the random-number
histogram, date grouping in the filter, editing a comment from the Review panel.
