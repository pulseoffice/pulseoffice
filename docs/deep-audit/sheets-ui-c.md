# Deep audit — sheets-ui, group C

All files were read in full (lines counted before the fixes).

## Coverage

| File (`src/PulseOffice.Sheets.UI/`) | Lines |
|---|---|
| Analysis/PivotFormulaField.cs | 64 |
| Analysis/SolverConstraintRows.cs | 66 |
| Analysis/SolverConstraintsField.cs | 87 |
| CellFormat/CellAlignmentDialogView.cs | 29 |
| CellFormat/CellAlignmentPage.cs | 203 |
| CellFormat/CellCharacterFormat.cs | 74 |
| CellFormat/CellStylePainter.cs | 64 |
| CellFormat/NewCellStyleView.cs | 92 |
| CellFormat/NumberFormatPage.cs | 405 |
| CellFormat/SparklineDialogView.cs | 184 |
| Conditional/ConditionalRuleCatalog.cs | 167 |
| Conditional/ConditionalRuleCompletion.cs | 56 |
| Conditional/ConditionalRuleEditorView.cs | 413 |
| Conditional/ConditionalRulePreview.cs | 247 |
| Conditional/ConditionalRulesManagerView.cs | 254 |
| DataDialogs/AdvancedFilterView.cs | 196 |
| DataDialogs/CellGridSketch.cs | 77 |
| DataDialogs/ConsolidateView.cs | 149 |
| DataDialogs/CustomSortOrders.cs | 33 |
| DataDialogs/DataFormView.cs | 195 |
| DataDialogs/FillSheetsView.cs | 117 |
| DataDialogs/FilterMenuModel.cs | 265 |
| DataDialogs/GoalSeekView.cs | 126 |
| DataDialogs/LabelsView.cs | 172 |
| DataDialogs/LedgerDialogKit.cs | 55 |
| DataDialogs/LedgerDialogProbes.cs | 52 |
| DataDialogs/LedgerDialogView.cs | 108 |
| DataDialogs/LedgerSortServices.cs | 39 |
| DataDialogs/MultipleOperationsView.cs | 145 |
| DataDialogs/NameManagerView.cs | 301 |
| DataDialogs/PasteSketchView.cs | 136 |
| DataDialogs/PasteSpecialModel.cs | 122 |
| DataDialogs/PasteSpecialView.cs | 122 |
| DataDialogs/RandomView.cs | 192 |
| DataDialogs/ScenarioView.cs | 232 |
| DataDialogs/SeriesView.cs | 229 |
| DataDialogs/SheetMoveView.cs | 156 |
| DataDialogs/SheetProtectionView.cs | 94 |
| DataDialogs/ShuffleView.cs | 90 |
| DataDialogs/SketchCells.cs | 79 |
| DataDialogs/StatisticsView.cs | 129 |
| DataDialogs/ValidationMessagePanes.cs | 234 |
| Dialogs/CsvImportPrompt.cs | 132 |
| Dialogs/CsvUnattendedOptions.cs | 69 |
| Dialogs/ScenarioPlan.cs | 69 |
| Dialogs/SheetReferencePickTarget.cs | 100 |
| Dialogs/SheetReferenceSource.cs | 84 |
| Dialogs/StandardFilterField.cs | 88 |
| Dialogs/TextToColumnsField.cs | 256 |
| Dialogs/ValidationCriteriaField.cs | 263 |
| Dialogs/ValidationDraft.cs | 167 |
| Exchange/LedgerExchange.cs | 341 |
| Exchange/SheetRangeCapture.cs | 113 |
| ExternalData/ExternalDataSources.cs | 93 |
| ExternalData/ExternalLinkAutoRefresh.cs | 158 |
| Fonts/LedgerFontCollector.cs | 10 |
| PageLayout/SheetPageSetupView.cs | 342 |
| ShapeText/ShapeTextCellStyle.cs | 103 |
| **Total: 58 files** | **8638** |

## Defects found

### Severe
1. **DataDialogs/MultipleOperationsView.cs:143** — `EngineMultipleOperationsPreviewer.Cell` built the input cell
   with the fixed sheet index `0`. On any sheet other than the first, both the preview and the command
   (`LedgerDataDialogCommands.MultipleAsync` uses `previewer.Request`) wrote the trial values into the same-named cell of the
   **first** sheet (then restored) and computed wrong results. **Fixed** (`reference.SheetIndex`), test
   `MultipleOperationsPreviewerTests`.

### Medium
2. **Conditional/ConditionalRuleCompletion.cs:38** and **ConditionalRuleEditorView.cs:62,139,407** — the operands of rules
   of type «Text» (contains, begins with…) were interpreted as numbers: «007» became «7», «1e3» «1000», «1.000» in
   Italian «1000»; the saved rule searched for a text different from the one written. **Fixed**: the text stays as is in
   conversion and display; test `IlTestoCercatoNonSiTrattaComeNumero`.
3. **Dialogs/CsvImportPrompt.cs:49-50** — an encoding or a language detected outside the fixed list (e.g. ISO-8859-1,
   pt-BR) silently fell back to the first entry (UTF-8, English) and the import used that. **Fixed**: the detected
   choice is added to the list and is the default; test `CsvImportChoicesTests`.

### Minor
4. **CellFormat/CellAlignmentPage.cs:155-156**, **CellFormat/CellStylePainter.cs:35-36** — underline and strikethrough together: the
   second `SetTextDecorations` replaced the first, the preview showed only the strikethrough; overline did not appear.
   **Fixed** with a single collection of decorations.

## Remaining (not fixed, outside the area or to be decided)
- **ShapeText/ShapeTextCellStyle.cs:76-78,95-97** — if the shape's base style is bold/italic/underlined, the
  command that removes it on a portion has no effect: the *run* model (`PulseOffice.Drawing`) is boolean and cannot
  say «not bold» over a bold base style. A nullable field is needed in the Drawing model: outside the area.
- **Exchange/LedgerExchange.cs:96-116** — copying a huge selection (whole columns or sheet) builds grid,
  snapshot, HTML, ODS and XLSX cell by cell over the whole area (also `SheetClipboard.Copy` in the engine): to verify
  live whether the caller limits the selection to the used area.
- **DataDialogs/RandomView.cs:134-158** — an invalid number in a hidden field (e.g. «Trials» with the uniform
  distribution) blocks confirmation with a message about a field that is not visible.
- **PageLayout/SheetPageSetupView.cs:329** — `Check` is `private static` but creates a control (it is not a pure computation);
  **ShapeText/ShapeTextCellStyle.cs:63** parameterless constructor that instantiates `ShapeTextEditing` (DI bypassed).
- **Conditional/ConditionalRulesManagerView.cs:74,81-88** — `async` event handlers: an exception from `EditAsync` is not
  caught.

## To try live
- Data ▸ Multiple operations on the **second** sheet: correct results, first sheet untouched.
- Conditional formatting ▸ Text contains «007»: the reopened rule still shows «007» and colours the right cells.
- Opening a CSV in Latin-1 or with an unlisted language: the window proposes the detected encoding and the preview is readable.
- Format cells ▸ Alignment and cell styles with underline + strikethrough: preview with both lines.
- Copying whole columns / Ctrl+A in Ledger: time and memory.
