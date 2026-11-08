# Deep audit: Ledger's interface (Sheets.UI, Data.UI)

Reserved loss codes: XLSX220–224, ODF240–244 (none used).

## Resuming the interrupted work

A previous agent had left twenty uncommitted files. Examined with `git diff`, completed and committed in three groups:

- Data.UI: unique connection identifiers (`ConnectionIdentifiers`: «Sales DB» no longer overwrites a connection with the same name), save, test and removal errors shown instead of being lost in a discarded `Task`, table list cancellable when the connection changes, import confirmation only after the preview of the same query, `TableEditModel` that keeps column types and numbers and the appearance of realigned cells, merge that removes overlapping merges (not only those with the origin inside the selection).
- Grid and view: resize borders found also in frozen panes with the sheet scrolled (`GridViewport.ColumnBorderAt/RowBorderAt`), spelling underlines outside the text discarded, autocomplete on the cell being edited and not on the active one, column style preserved when entering a percentage (`CellEntryStyle`), status bar summary on non-empty cells only (`SelectionSummary`: a whole column no longer walks a million cells).
- Macros: undoing the clearing of conditional rules restores their order; renaming a sheet by case only; format of empty cells starting from the column style.

Tests: `GridAndDataAuditTests`, three new cases in `LedgerFacadeContractTests`.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Data.UI/ConnectionIdentifiers.cs | 29/29 | ok |
| Data.UI/ConnectionManagerView.cs | 337/337 | ok; the password does not stay in the field after saving, does not go into the logs |
| Data.UI/DataImportFlow.cs | 59/59 | ok |
| Data.UI/DataImportView.cs | 315/315 | **fixed** (medium): the preview was tied only to the SQL text; changing connection after the preview inserted the query with the preview of the other connection. Now the key is (connection, SQL). The query runs only on an explicit click (Load or Insert) |
| Data.UI/DataTableRenderer.cs | 277/277 | minor, not fixed: brushes and `merges.Any` per cell on every draw (preview tables ≤ 200 rows) |
| Data.UI/TableEditModel.cs | 97/97 | ok |
| Data.UI/TableEditorView.cs | 524/524 | ok |
| Data.UI/TabularPreview.cs | 206/206 | minor: `_read` in the constructor not protected from read exceptions |

Note: the fixed white background of the previews is intended, the renderer draws fixed dark text.

## Second session: GridParts and LedgerViewParts

| File | Lines read | Outcome |
|---|---|---|
| GridParts/GridNotifications.cs | 64/64 | ok |
| GridParts/Input/GridPointerInput.cs | 361/361 | ok: every gesture ends in a notification that the view checks |
| GridParts/Gestures/FillGesture.cs | 45/45 | ok; the check of locked cells is in `ApplyFill` |
| GridParts/Gestures/ObjectDragGesture.cs | 78/78 | ok |
| GridParts/Gestures/ColumnResizeGesture.cs | 47/47 | minor, not fixed: while dragging on a protected sheet the column widens visually and reverts on release |
| GridParts/Gestures/RowResizeGesture.cs | 47/47 | as above |
| LedgerViewParts/LedgerCellOperations.cs | 170/170 | ok: deletion, fill and resizing check protection and read-only |
| LedgerViewParts/LedgerObjectEditing.cs | 337/337 | **fixed** (serious): freehand drawing, point editing, adjust handles and `ReplaceShape` checked only read-only: on a protected sheet the mouse added and changed objects with undoable steps. Now they all go through `CanEditObjects` |
| LedgerViewParts/LedgerShapeTextSession.cs | 135/135 | **fixed** (serious, same root): a shape's text was written and formatted on a protected sheet |
| LedgerViewParts/LedgerCellWriter.cs | 167/167 | ok |
| LedgerViewParts/LedgerCellEditing.cs | 393/393 | minor: `BeginEdit` opens the editor in a read-only document (the write then refuses and says so) |
| GridParts/Input/GridKeyboardInput.cs | 226/226 | ok: Delete on objects, moving with the arrows and text typed on a shape go through the view's checks |
| GridParts/Input/FreeformDrawing.cs | 103/103 | ok; `Tools` unhooks the old state's event |
| GridParts/GridViewport.cs | 402/402 | **fixed** (serious): the filter wrote height 0 and then the height in pixels into the layout of the *current* sheet, with the table of hidden rows kept per view and not per sheet. Removing the filter after switching sheet put heights back on the rows of the other sheet, and every filtered row came back with an explicit height (saved as manual, rounded). Now `HideRow`/`ShowRow` per sheet is used, only on the rows hidden by the filter. **Fixed** (medium): `MergedRect` summed row by row: a whole-column merge cost a million iterations on every draw; now a difference of offsets. Minor, not fixed: `AnchorAt` ignores frozen panes (the anchor of an object dropped over the frozen pane is computed with the main scroll) |
| GridParts/Painting/GridCellPainter.cs | 231/231 | medium, not fixed: conditional rules are evaluated twice per cell (background and text) on every draw, and `Merges.At` per cell; to be revisited with a per-draw cache |
| GridParts/Painting/CellValuePainter.cs | 159/159 | **fixed** (medium): a new `SolidColorBrush` for every coloured cell on every draw; now from `IGridPaints`. Minor: substrings to measure misspelled words, string of `#` per narrow cell |
| GridParts/Painting/CellDecorationPainter.cs | 170/170 | **fixed** (medium): background brushes and pens, borders (up to six per cell), colour scales, bars and change marks recreated per cell on every draw. Now `GridPaints` (Flyweight capped at 1024 entries) in `GridRenderServices` |
| GridParts/GridRenderServices.cs | 22/22 | ok (added `Paints`) |
| GridParts/SheetGridParts.cs | 106/106 | ok |
| GridParts/Painting/GridOverlayPainter.cs | 202/202 | minor: active border and fill handle are not drawn if the active cell is out of view even when part of the area is in view |
| GridParts/Painting/GridHeaderPainter.cs | 116/116 | minor: one `FormattedText` per header on every draw (dozens per screen) |
| GridParts/Painting/GridObjectPainter.cs | 162/162 | medium, not fixed: each chart's data are extracted from the sheet on every draw (`ChartData.Extract`); minor: when dragging several objects, the preview frame of all of them is that of the grabbed object |
| GridParts/GridObjectSelection.cs | 64/64 | ok |
| GridParts/GridObjectRenderers.cs | 31/31 | ok |
| GridParts/GridTheme.cs | 45/45 | ok |
| GridParts/GridDisplayOptions.cs | 29/29 | ok |
| GridParts/IGridSurface.cs | 16/16 | ok |
| (revisited) GridParts/Input/GridPointerInput.cs | — | **fixed** (minor): a new native `Cursor` on every mouse move over the headers; now one per kind |
| GridParts/Gestures/PathEditGesture.cs | 33/33 | **fixed** (serious): on every mouse move it notified a change; the view looked for the object by record equality, which after the first replacement was no longer there: dragging a point only the first pixel counted, and every move was an undo step. Now a preview during the drag (`Preview`, painted by `GridObjectPainter`) and a single step on release |
| GridParts/Gestures/FormControlScrollGesture.cs | 38/38 | **fixed** (serious, same root): a form control's scroll bar stopped at the first step and filled the history; now a preview and one step on release (the linked cell updates on release) |
| GridParts/Gestures/ShapeAdjustGesture.cs | 32/32 | ok (already one step on release) |
| GridParts/Gestures/SelectionGesture.cs | 14/14 | ok |
| GridParts/Gestures/GridGesture.cs | 24/24 | ok |
| GridParts/Gestures/GridGestureFactory.cs | 44/44 | ok |
| LedgerViewParts/LedgerStatusSegments.cs | 44/44 | ok |
| LedgerViewParts/LedgerViewLayout.cs | 68/68 | ok |
| LedgerViewParts/SelectionSummary.cs | 39/39 | ok |
| LedgerViewParts/CellEntryStyle.cs | 25/25 | ok |
| (revisited) LedgerViewParts/LedgerObjectEditing.cs | — | **fixed**: `ApplyFreeform`, `ApplyAdjustment`, `ApplyFormControl` find the object by `Id` and start from the current state, not from the event's snapshot (which would have undone a move made in the meantime). Minor, not fixed: `RaiseFormEventsAsync` discarded with `_ =`, an exception of the event macro is lost |

Coverage of GridParts and LedgerViewParts: **complete** (38 files, 4,289 lines).

Tests: `LedgerViewPartsAuditTests` (protection, filter and subsequent edits fail before the fix).

## Files at the root of Sheets.UI (commands excluded)

| File | Lines read | Outcome |
|---|---|---|
| SheetGridView.cs | 465/465 | minor: the horizontal wheel with ⌘/Ctrl zooms out (`Delta.Y == 0` counts as «down») |
| LedgerView.cs | 558/558 | ok; `_history.Changed` and the height tracker are not unhooked, but they live as long as the document |
| ActiveSheetContext.cs | 364/364 | ok (a shape's text goes through `ISheetShapeText`, which checks protection itself) |
| FilterMenuView.cs | 305/305 | **fixed** (medium): a malformed filter colour read from an ODS (`#ZZZZZZ` passes the length check of `OdfDatabaseRangeMapper`) made `Brush.Parse` throw when opening the filter menu; now `FilterSwatchColours` with a fallback. Minor: no cap on the value checkboxes on columns with tens of thousands of distinct values |
| FunctionWizardView.cs | 338/338 | **fixed** (medium, translation): `FunctionDefinition.Description` is never set, so the description was always «Category: Statistica» with the Italian category name in all languages; now the translated label of `FunctionCategoryLooks` |
| PivotPromptView.cs | 442/442 | minor: the preview values use `CellValue.ToString()` without format or culture; the preview has fixed (light) colours also in the dark theme |
| FormatCellsView.cs | 236/236 | ok (the Protection tab has only «Locked»: the model has no «hidden formula», a core limit) |
| SheetScriptApi.cs | 196/196 | ok (metadata) |
| LedgerReviewSource.cs | 157/157 | minor: editing or deleting a comment of a protected sheet from the panel silently does nothing, without a message |
| LedgerChangeResolution.cs | 63/63 | **fixed** (serious): «Reject» from the Review panel rewrote the locked cells of a protected sheet (the panel does not go through `CommandExecutor`, so `LedgerProtectionGuard` did not see it), and «Accept/Reject» did not check protected recording (only the button was disabled). Now `ISheetEditGuard` per cell, refusal with message and remedy |
| CellNavigator.cs | 148/148 | ok; `RowEnd` walks all non-empty cells of the sheet (linear, on every End key) |
| GridGeometry.cs | 147/147 | ok (`VisibleCells` includes one row and one column beyond the view: extra drawing, harmless) |
| LedgerDocumentFactory.cs | 141/141 | **fixed** (minor, reuse rule): construction and wiring of the view duplicated between document and second view; now `CreateView`. Minor, not fixed: `RenameAsync` and `ShowMenuAsync` discarded with `_ =` |
| SheetSelection.cs | 117/117 | ok |
| LedgerFindTarget.cs | 112/112 | ok (read-only and locked cells checked before replacing) |
| LedgerNavigatorSource.cs | 117/117 | **fixed** (serious): the Navigator reordered sheets (arrows and drag) even with the workbook structure protected and in a read-only document; now `CanMove`/`CanDrop`/`Reorder` respect `Protection.Structure`, and `ModuleNavigatorSource` (Shell, outside the area, bare minimum) blocks moves and drops in read-only mode as `ModuleReviewSource` already did. Minor: a hand-built «-1:…» id would go out of range (the ids are produced by the panel itself) |
| SheetSpelling.cs | 109/109 | minor: the cell language reads only the cell's own style (`sheet[address].StyleId`), not that of the row or column |
| SpreadsheetDocumentService.cs | 108/108 | minor: the CSV preview reads 64 KB with a single `Read`, which on non-file streams may return less |
| LedgerDocumentOperations.cs | 105/105 | **fixed** (medium): `WriteTo`, `Properties`, `RenderHtml` and `Versions` ignored the document passed and used the active workbook; now `document.Model`. `BuildPrintable` stays tied to the active context (built by `ISheetPrintDocumentBuilder`) |
| LedgerStylesSource.cs | 100/100 | minor: on every refresh of the Styles panel it walks all cells of all sheets to mark the styles in use |
| SheetImageInsertion.cs | 75/75 | **fixed** (serious): `InsertObject` checked neither read-only nor protection; acquisition from the scanner (`ShellScannerCommands`, asynchronous: it inserts after the command guard has already closed the check) put images into a protected sheet or a read-only document. Now it refuses with message and remedy |
| LedgerProtectionGuard.cs | 74/74 | minor: watches only the active sheet and copies all non-empty cells on every command on a protected sheet; asynchronous commands that write after the first `await` are not seen (see above) |
| NameBoxResolver.cs | 80/80 | ok |
| SheetPrintDocumentBuilder.cs | 81/81 | ok |
| SparklinePainter.cs | 99/99 | **fixed** (medium, drawing): for every point it rebuilt the list of present values (quadratic per sparkline on every draw) and a new brush for every marker; now maximum, minimum, first and last and the brush are computed once per sparkline |
| FormEventRunner.cs | 71/71 | ok |
| QueryRefreshRunner.cs | 59/59 | medium, not fixed: the «after» capture happens at the end of the asynchronous refresh, so changes made by the user while waiting end up in the «Refresh» step and a ⌘Z of that step erases them although they have a step of their own |
| WebResourceAccess.cs | 58/58 | **fixed** (serious, security): `WEBSERVICE` read the entire response into memory (`ReadAsStringAsync`) before truncating it to 2 MB: a server could make it allocate gigabytes; now `Send` with `ResponseHeadersRead` and reading up to the cap, without `GetAwaiter().GetResult()`. Remaining: synchronous call on the UI thread (up to 10 s per cell) |
| LedgerCommandSet.cs | 69/69 | ok |
| SheetTextTransform.cs | 66/66 | ok |
| CellTextComposer.cs | 72/72 | **fixed** (medium, drawing): `FontFamily`, `Typeface` and an empty `TextDecorationCollection` for every cell on every draw; now a Flyweight of fonts (cap 256) and decorations only when needed |
| SheetTabStrip.cs | 61/61 | minor: tab colour brush and pen recreated on every draw (few tabs) |
| CellOverflow.cs | 60/60 | ok |
| WorksheetTabPolicy.cs | 56/56 | ok |
| CellTextPlacement.cs | 55/55 | ok |
| SparklineBars.cs | 54/54 | ok |
| CellFormatDialog.cs | 53/53 | ok |
| LedgerTableLooks.cs | 47/47 | ok |
| SheetChartImages.cs | 45/45 | ok |
| SpreadsheetSnapshotProvider.cs | 38/38 | **fixed** (medium): the outcome of writing the recovery snapshot was ignored, and a failed save replaced the previous good snapshot with partial data; now the error is raised and `DocumentRecovery` keeps the old one (no test: it needs a complete fake format registry). `DeckSnapshotProvider` has the same defect (outside the area) |
| SheetClipboardService.cs | 37/37 | ok |
| SheetTabSurface.cs | 30/30 | ok |
| LedgerMacroEventsSource.cs | 29/29 | ok |
| ActiveShellViewOptions.cs | 27/27 | **fixed** (medium): followed the last view created, not the active document: with two workbooks open, hiding the formula bar acted on the other one. Now reads `IDocumentWorkspace.Active` |
| ConditionalIcons.cs | 23/23 | ok |
| DrawingTools.cs | 18/18 | ok |
| CellTextMeasure.cs | 15/15 | ok |
| LedgerRemedies.cs | 12/12 | ok |
| LocalizedSeriesNames.cs | 9/9 | ok |

Coverage of the root (excluding `Ledger*Commands.cs`): **complete**, 50 files, 5,845 lines.


## Subfolders Analysis, ExternalData, PageLayout, ShapeText, Exchange

| File | Lines read | Outcome |
|---|---|---|
| Exchange/LedgerExchange.cs | 341/341 | **fixed** (medium, translation): pasting cells left in the history the name «Incolla» written in Italian inside `SheetClipboard` (core), in all languages; now an outer translated transaction. **Fixed** (minor): pasting objects into a protected sheet failed silently, now with a message. Minor, not fixed: cutting objects of a protected sheet copies them without saying so |
| (revisited) LedgerFindTarget.cs | — | **fixed** (medium, translation): «Replace all» appeared in the history as «Sostituisci» (`WorkbookSearch`, core) in every language; now an outer translated transaction. The same defect remains in `SheetSort` («Ordina», called by the commands, outside the area) |
| Exchange/SheetRangeCapture.cs | 113/113 | ok (writes ODS and XLSX of the portion on every copy: cost proportional to the area) |
| ExternalData/ExternalLinkAutoRefresh.cs | 158/158 | minor: an exception from `Read` not covered by `IsFailure` bubbles up from the timer's asynchronous handler |
| ExternalData/ExternalDataSources.cs | 93/93 | ok (16 MB cap also without `Content-Length`) |
| PageLayout/SheetPageSetupView.cs | 342/342 | minor: in «Fit» with 0 pages in width and in height nothing stays set and the window reopens in «Percentage» |
| ShapeText/ShapeTextCellStyle.cs | 103/103 | ok |
| Analysis/PivotFormulaField.cs | 64/64 | minor: a header with an apostrophe is inserted between quotes without doubling it |
| Analysis/SolverConstraintRows.cs | 66/66 | ok |
| Analysis/SolverConstraintsField.cs | 87/87 | ok |

Coverage of the five subfolders: **complete**, 10 files, 1,367 lines.

## Still to read

Nothing of the area assigned to this round: the root of Sheets.UI (excluding `Ledger*Commands.cs`, assigned to another agent) and the five subfolders are read in full. The other subfolders of Sheets.UI not yet covered by any round remain (for example `Dialogs`, `DataDialogs`, `CellFormat`, `Scripting`; `LedgerViewParts` already done).

## Remaining defects, reported to other areas

- `DeckSnapshotProvider` ignores the outcome of writing the recovery snapshot (as Ledger did before the fix).
- `SheetSort` and `SheetClipboard`/`WorkbookSearch` (core) open transactions with fixed Italian names; Ledger's callers now wrap them, but every other caller (sort commands) shows «Ordina».
- ~~`QueryRefreshRunner`: the capture after an `await` absorbs the changes made in the meantime.~~ Fixed in the following round.
- `LedgerProtectionGuard` does not see the effects of asynchronous commands after the first `await`.

## To test live

- Protected sheet: freehand drawing, point editing, yellow handles, a shape's text (double click and typing): all refused with the message.
- Point editing and a form's scroll bar: the drag follows the mouse all the way, ⌘Z undoes everything in one step.
- Autofilter active on a sheet, switch sheet, remove the filter: the rows of the other sheet stay as they were; filtered rows go back to the default height.
- Resize cursors on the headers.
- Review panel on a protected sheet: «Reject» on a locked cell does not change it and says so; with protected recording neither «Accept» nor «Reject» acts.
- Navigator with protected structure or in read-only mode: arrows and dragging of sheets move nothing.
- Scanner on a protected or read-only sheet: no image inserted, message in the status bar.
- Two workbooks open: «Formula bar» from the View menu acts on the active workbook.
- Function wizard in English, German, Spanish, French: the description gives the category in the interface language.
- Filter menu on an ODS with a malformed filter colour: it opens.
- History (Edit menu) after Paste and Replace all in English: translated names.
- Sheets with many sparklines and text cells: smooth scrolling.

## Following round: Scripting, DataDialogs, Dialogs, Conditional, CellFormat, Fonts

| File | Lines read | Outcome |
|---|---|---|
| Scripting/SheetFacade.cs | 713/713 | **fixed** (serious, protection bypassed): `AddChart`, `AddShape`, `RemoveObject` added and removed objects on a protected sheet (the command guard does not see macros); now refused as by `LedgerProtectionGuard`. Minor: `DistinctValues` does not bound the area (whole column = a million rows) |
| Scripting/ScenarioFacade.cs | 62/62 | **fixed** (medium): `Capture` and `Remove` changed the scenarios of a protected sheet |
| Scripting/ScriptProtection.cs | 51/51 | ok |
| Scripting/ScriptSheetEdits.cs | 54/54 | ok |
| Scripting/ValidationFacade.cs | 153/153 | ok |
| Scripting/FormatRuleFacade.cs | 186/186 | minor: a null `comparison` gives `NullReferenceException` instead of `ArgumentException` |
| Scripting/FilterFacade.cs | 109/109 | ok |
| Scripting/PivotFacade.cs | 169/169 | ok |
| Scripting/DocumentFacade.cs | 252/252 | minor: the properties (title, author, keywords) have no length cap |
| Scripting/PrintFacade.cs | 190/190 | **serious, not fixed** (sandbox): `page.ExportPdf(path)` writes a PDF into any folder even from a sandboxed macro (`FileSystemWrite` false), including the form control macros (`FormEventRunner`) of a received document. The facade does not know the permissions of the run: a current-permissions service set by the engine is needed (Scripting.Editor, Agent, FormEventRunner: outside the area), and the agent at the «files» level must enable it. `Printers` blocks the thread with `GetAwaiter().GetResult()` |
| Scripting/ScriptGlobals.cs | 40/40 | ok |
| Scripting/ScriptReferences.cs | 44/44 | ok |
| Scripting/LedgerScriptHost.cs | 19/19 | minor: `Copy`/`Cut`/`Paste` not awaited, the macro continues before the paste has happened and an error is lost |
| (revisited) QueryRefreshRunner.cs | — | **fixed** (medium, history): the «before» snapshot was taken before the `await` of the queries, so what the user wrote in the meantime ended up in the «Refresh» step and ⌘Z erased it. `IQueryRefresher` (Sheets.Queries, touched minimally) now separates `FetchAllAsync` (network) from `Apply` (synchronous write on the calling thread): previously the write happened on the pool thread (`ConfigureAwait(false)`) while the interface could draw; a link removed while waiting is not rewritten; protection is re-checked after the wait |
| DataDialogs/NameManagerView.cs | 301/301 | ok |
| DataDialogs/FilterMenuModel.cs | 265/265 | ok |
| DataDialogs/ValidationMessagePanes.cs | 234/234 | ok (fixed colours: they imitate the balloon and the error window, intended) |
| DataDialogs/ScenarioView.cs | 232/232 | ok |
| DataDialogs/SeriesView.cs | 229/229 | **fixed** (minor, validation): «NaN» and «∞» typed in the increment or the limit passed validation (`double.TryParse` accepts them) and reached the fill; now refused as in `LedgerDialogView.ReadNumber`. No test (views are tested in Desktop.Tests, outside the allowed projects) |
| DataDialogs/AdvancedFilterView.cs | 196/196 | ok |
| DataDialogs/DataFormView.cs | 195/195 | ok |
| DataDialogs/RandomView.cs | 192/192 | minor: the trials recompute 400 samples on every key; a huge «Trials» is rounded to `int` without a cap before the check (only the binomial checks it) |
| DataDialogs/LabelsView.cs | 172/172 | ok |
| DataDialogs/SheetMoveView.cs | 156/156 | ok |
| DataDialogs/LedgerDialogView.cs | 108/108 | ok |
| DataDialogs/LedgerDialogKit.cs | 55/55 | ok |
| DataDialogs/ConsolidateView.cs | 149/149 | ok |
| DataDialogs/MultipleOperationsView.cs | 145/145 | **fixed** (serious, wrong data): the row and column input cells were always built on sheet 0 (`new SheetCell(0, …)`), so on any other sheet multiple operations substituted the values in the cells of the first sheet and gave wrong results (preview and command) |
| DataDialogs/PasteSketchView.cs | 136/136 | ok |
| DataDialogs/LedgerDialogProbes.cs | 52/52 | ok |
| DataDialogs/GoalSeekView.cs | 126/126 | ok |
| DataDialogs/StatisticsView.cs | 129/129 | ok |
| DataDialogs/PasteSpecialModel.cs | 122/122 | ok |
| DataDialogs/PasteSpecialView.cs | 122/122 | ok |
| DataDialogs/FillSheetsView.cs | 117/117 | ok |
| DataDialogs/SheetProtectionView.cs | 94/94 | ok |
| DataDialogs/ShuffleView.cs | 90/90 | ok |
| DataDialogs/SketchCells.cs | 79/79 | ok |
| DataDialogs/CellGridSketch.cs | 77/77 | ok |
| DataDialogs/LedgerSortServices.cs | 39/39 | ok |
| DataDialogs/CustomSortOrders.cs | 33/33 | ok |

Coverage of DataDialogs: **complete**, 27 files, 3,845 lines.
| Dialogs/ValidationCriteriaField.cs | 263/263 | ok |
| Dialogs/TextToColumnsField.cs | 256/256 | ok |
| Dialogs/ValidationDraft.cs | 167/167 | ok |
| Dialogs/CsvImportPrompt.cs | 132/132 | ok |
| Dialogs/SheetReferencePickTarget.cs | 100/100 | ok |
| Dialogs/StandardFilterField.cs | 88/88 | ok |
| Dialogs/SheetReferenceSource.cs | 84/84 | ok |
| Dialogs/ScenarioPlan.cs | 69/69 | ok |
| Dialogs/CsvUnattendedOptions.cs | 69/69 | minor: a separator equal to the text delimiter (`"`) is accepted without a warning |

Coverage of Dialogs: **complete**, 9 files, 1,228 lines.
| Conditional/ConditionalRuleEditorView.cs | 413/413 | minor: «Confirm» does not check the cap of 100 for top/bottom in percent, which `ConditionalRuleCompletion` then rejects |
| Conditional/ConditionalRulesManagerView.cs | 254/254 | minor: the `async` event handlers (actions, double click, keys) let an editor exception bubble up with no safety net |
| Conditional/ConditionalRulePreview.cs | 247/247 | ok |
| Conditional/ConditionalRuleCatalog.cs | 167/167 | ok |
| Conditional/ConditionalRuleCompletion.cs | 56/56 | minor: `AllowThousands` makes «1.5» read as 15 in Italian and «1,5» as 15 in English |

Coverage of Conditional: **complete**, 5 files, 1,137 lines.
| CellFormat/NumberFormatPage.cs | 405/405 | ok |
| CellFormat/CellAlignmentPage.cs | 203/203 | ok |
| CellFormat/SparklineDialogView.cs | 184/184 | minor (performance): `Points()` re-reads the data range on every draw of the preview and of the three thumbnails; with a whole column that is a million cells per frame. «Confirm» does not validate data and destination |
| CellFormat/NewCellStyleView.cs | 92/92 | ok |
| CellFormat/CellCharacterFormat.cs | 74/74 | ok |
| CellFormat/CellStylePainter.cs | 64/64 | ok |
| CellFormat/CellAlignmentDialogView.cs | 29/29 | ok |
| Fonts/LedgerFontCollector.cs | 10/10 | ok |

Coverage of CellFormat and Fonts: **complete**, 8 files, 1,061 lines.

### Summary of the round

Coverage: Scripting (13 files, 2,042 lines), DataDialogs (27, 3,845), Dialogs (9, 1,228), Conditional (5, 1,137), CellFormat (7, 1,051), Fonts (1, 10): **62 files, 9,313 lines, all read**.

Defects by severity:
- **Serious, fixed**: macros that add and remove objects on protected sheets (`SheetFacade.cs`, `AddChart`/`AddShape`/`RemoveObject`); multiple operations with the input cells always on sheet 0 (`MultipleOperationsView.cs:143`).
- **Serious, not fixed**: `PrintFacade.ExportPdf` writes files from sandboxed macros (`PrintFacade.cs:136`); the fix touches the macro engine, the agent and `FormEventRunner` (outside the area).
- **Medium, fixed**: scenarios created and removed by macros on protected sheets (`ScenarioFacade.cs`); `QueryRefreshRunner` absorbing into the «Refresh» step the changes made while waiting and writing the sheet off the UI thread.
- **Minor, fixed**: series accepting NaN and infinity (`SeriesView.cs:119`).
- **Minor, not fixed**: listed in the table (FormatRuleFacade, DocumentFacade, LedgerScriptHost, RandomView, CsvUnattendedOptions, ConditionalRuleEditorView, ConditionalRulesManagerView, ConditionalRuleCompletion, SparklineDialogView). Also remaining: `IQueryRefresher.RefreshAsync` (refresh of a single query, used by `LedgerExternalDataCommands`): it still writes after a `ConfigureAwait(false)`.

To test live:
- Protected sheet, macro `sheet.AddShape("Rectangle","B2")`, `sheet.RemoveObject(...)`, `scenarios.Capture("x","A1:A2")`: «protected» error, nothing changes.
- Multiple operations on the second sheet with row and column input: results as in the reference suite, the first sheet intact.
- Refresh external data on a slow query, write into a cell while waiting, then ⌘Z: only the refresh is undone, the written cell stays.
- Series with increment «NaN»: button disabled.
