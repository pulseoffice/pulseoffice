# Round 253: small leftovers, sorted validation lists, shown-value duplicates

Queue leftovers across modules, then a further pass over Ledger data validation and remove duplicates.

| Severity | Defect | Fix |
|---|---|---|
| Medium | Cutting the last outline block left a trailing line break; the remaining text parsed as one extra empty slide, so the cut blocks were dropped from the identity bookkeeping and the paste recreated the slide. | `IOutlineBlockTransfer.RemovalStart` widens the cut of a final block to the line break before it; the outline view moves the selection start before the text box deletes it. |
| Medium | ODS `table:display-list="sorted"` was read as a plain dropdown and written back `unsorted`; the dropdown never sorted. | `DataValidation.SortDropdown`, read and written in ODS; `IValidationChoiceOrder` sorts the choices (numbers first, then text ignoring case) in the cell dropdown and in the dialog preview; the validation dialog gains "Sort the entries in ascending order". XLSX has no such setting (no attribute in `dataValidation` nor in the x14 extension): typed entries are written already sorted and the remaining loss for cell ranges is declared as XLSX244. |
| Medium | Remove duplicates compared stored values, while Excel compares what the cells show (its documentation: a date shown in two formats is two values; numbers rounded by the format to the same text are one). | `DuplicateRemover` keys rows by the presented text (`ICellPresenter`), text cells by their text. |
| Minor | `VersionComparisonView`, `ReviewPanel`, `ChoiceSlot`, `TextReviewPanel`, `StylePreviewPainter` and `TableCellTextStyles` applied Avalonia's static `TextDecorations` collections (bound to the first UI thread that touched them). | Shared `ITextDecorationFactory` in `PulseOffice.UI.Controls.Text`, also used by `ShapeRenderer` and the cell text composer; `Round253TextDecorationTests` forbids the static collections in `src`. |
| Minor | `SlideDragSession.PlatformSlop` took an unused visual. | Parameter removed. |

Research for the sorted list: ECMA-376 `CT_DataValidation` and the x14 `dataValidation` extension carry no ordering;
Excel always lists a range source in cell order.

## Still open
- The last-block cut relies on the text box copying the selection before raising `CuttingToClipboardEvent` and
  deleting the (possibly widened) selection afterwards; not seen in the running app.
- Sorted lists from a cell range appear in cell order in Excel (XLSX244); not opened in Excel or the reference suite.
- Mixed text "1" and number 1 shown as "1" are now duplicates; Excel's behaviour for that case was not checked live.
