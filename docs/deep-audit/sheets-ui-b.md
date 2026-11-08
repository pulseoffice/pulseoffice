# Deep audit — sheets-ui, group B

Date: 2026-11-01. Scope: 50 files of `src/PulseOffice.Sheets.UI` (project root), read in full.

## Coverage

| File | Lines |
|---|---|
| ActiveSheetContext.cs | 364 |
| ActiveShellViewOptions.cs | 27 |
| CellFormatDialog.cs | 53 |
| CellNavigator.cs | 148 |
| CellOverflow.cs | 60 |
| CellTextComposer.cs | 72 |
| CellTextMeasure.cs | 15 |
| CellTextPlacement.cs | 55 |
| ConditionalIcons.cs | 23 |
| DrawingTools.cs | 18 |
| FilterMenuView.cs | 305 |
| FormEventRunner.cs | 71 |
| FormatCellsView.cs | 236 |
| FunctionCategoryLooks.cs | 44 |
| FunctionWizardView.cs | 338 |
| GridGeometry.cs | 147 |
| LedgerChangeResolution.cs | 63 |
| LedgerCommandSet.cs | 69 |
| LedgerDocumentFactory.cs | 141 |
| LedgerDocumentOperations.cs | 105 |
| LedgerFindTarget.cs | 112 |
| LedgerMacroEventsSource.cs | 29 |
| LedgerNavigatorSource.cs | 117 |
| LedgerProtectionGuard.cs | 74 |
| LedgerRemedies.cs | 12 |
| LedgerReviewSource.cs | 157 |
| LedgerStylesSource.cs | 100 |
| LedgerTableLooks.cs | 47 |
| LedgerView.cs | 558 |
| LocalizedSeriesNames.cs | 9 |
| NameBoxResolver.cs | 80 |
| PivotPromptView.cs | 442 |
| QueryRefreshRunner.cs | 59 |
| SheetChartImages.cs | 45 |
| SheetClipboardService.cs | 37 |
| SheetGridView.cs | 465 |
| SheetImageInsertion.cs | 75 |
| SheetPrintDocumentBuilder.cs | 81 |
| SheetScriptApi.cs | 196 |
| SheetSelection.cs | 117 |
| SheetSpelling.cs | 109 |
| SheetTabStrip.cs | 61 |
| SheetTabSurface.cs | 30 |
| SheetTextTransform.cs | 66 |
| SparklineBars.cs | 54 |
| SparklinePainter.cs | 99 |
| SpreadsheetDocumentService.cs | 108 |
| SpreadsheetSnapshotProvider.cs | 38 |
| WebResourceAccess.cs | 58 |
| WorksheetTabPolicy.cs | 56 |
| **Total: 50 files** | **5845 lines read out of 5845** |

## Defects found

### Severe
1. `WebResourceAccess.cs:35-41` — the WEBSERVICE response was read **in full** into memory (`ReadAsStringAsync`) and only afterwards truncated to 2 MB: a server answering with gigabytes exhausted the process memory during recalculation. In addition an unknown `charset` in the header made it throw `InvalidOperationException`, uncaught, inside the calculation engine. **Fixed**: streaming read (`ResponseHeadersRead`) limited to 2 MB of characters, encoding resolved with fallback to UTF-8, `IOException` caught too. Test `WebResourceAccessTests`.

### Medium
2. `FilterMenuView.cs:181,183` — `Brush.Parse` on a filter colour read from the file: in ODS the mapper accepts any seven-character `#xxxxxx` (`#GGGGGG`), and the cells' background comes from the file; the filter menu threw `FormatException` on opening. **Fixed** with `Color.TryParse` and fallback. Test `Il_menu_del_filtro_regge_un_colore_malformato_letto_dal_file` (Desktop.Tests).
3. `LedgerNavigatorSource.cs:76` and `LedgerReviewSource.cs:151` — an identifier «-1:…» passed the check (only `< Count`) and ended in `Sheets[-1]` (`ArgumentOutOfRangeException`) in Go/Edit/Delete. **Fixed**: index read with `NumberStyles.None` and invariant culture (no sign). Test `LedgerPanelSourcesTests`.

### Minor
4. `LedgerDocumentFactory.cs:72-94 / 113-134` — construction and wiring of `LedgerView` duplicated between first and second view (risk of divergence: an event added to only one). **Fixed**: a single `Compose`.
5. `SparklinePainter.cs:84-95` — `Highlight` rebuilt the list of points at every bar/marker (quadratic per sparkline, in the drawing loop). **Fixed**: extremes computed once per drawing.

## Remaining (not fixed, outside the area or to be decided)
- **Severe, outside the area** — `PulseOffice.Sheets.Queries/QueryRefresher.cs:92`: `RefreshAllAsync` uses `ConfigureAwait(false)` and then writes the sheet's cells on the pool thread, while the interface may draw or modify the same `Workbook`. `QueryRefreshRunner` awaits with `ConfigureAwait(true)` but the inner work is already off the UI thread. To be fixed in the Queries project (writes brought back to the caller).
- Medium — `QueryRefreshRunner.cs:42`: if the refresh is cancelled or throws, the status bar stays on «refresh in progress» and the partial changes do not enter the history.
- Minor — `SpreadsheetSnapshotProvider.cs:20`: the result of `Write` is ignored; a failed snapshot is silent.
- Minor — `SheetImageInsertion.cs:29`: it checks neither read-only nor protection; protection is covered by `LedgerProtectionGuard` for commands, read-only depends on the caller (e.g. scanning in `ShellScannerCommands`).
- Minor — `PivotPromptView.cs:123,287-290`: preview with fixed (white/green) colours not themed; intended as «sheet look», to be confirmed in the dark theme.
- Minor — `LedgerTableLooks.cs:29`: applying a table style removes bold from the body cells (replacement, not derivation).

## To try live
- Filter menu on an ODS with an invalid filter colour.
- Navigator and Review panel: go to / edit / delete comment.
- WEBSERVICE on a large URL and on one with a strange charset.
- Second view of a sheet (Window › New window): chart edit, rename, filter menu events.
- Column/line sparkline with high/low/first/last points highlighted.
