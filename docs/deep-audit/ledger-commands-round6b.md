# Deep audit, round 6, part 2: Ledger commands and dialogs

Scope: the `Ledger*Commands` files of `src/PulseOffice.Sheets.UI` not covered by part 1, the views in
`DataDialogs/`, `Conditional/`, `PageLayout/`, and the open observations left by part 1 that do not touch the files
reserved to it (`LedgerDataDialogCommands`, `LedgerDocumentFactory`, `LedgerFinalCommands`, `LedgerPivotCommands`,
`LedgerSheetCommands`, `LedgerSheetToolsCommands`, `WorksheetTabPolicy`, `Scripting/DocumentFacade`: not touched).

Tests: `tests/PulseOffice.Sheets.UI.Tests/LedgerRound6bAuditTests.cs`. Every test below was run against the code
before its fix and failed (verified by temporarily reintroducing the old line), then passes with the fix.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| LedgerConditionalRuleCommands.cs | 155/155 | fixed (one policy) |
| LedgerConditionCommands.cs | 129/129 | fixed (one policy) |
| LedgerSheetExtraCommands.cs | 351/351 | fixed (reader, token); notes |
| LedgerChartCommands.cs | 249/249 | fixed (anchor, z-order) |
| LedgerDatabaseRangeCommands.cs | 126/126 | fixed (name rules) |
| LedgerFillCommands.cs | 203/203 | clean |
| LedgerFilterCommands.cs | 199/199 | note |
| LedgerTailCommands.cs | 571/571 | fixed (data loss, empty steps, name rules) |
| LedgerAnalysisCommands.cs | 471/471 | fixed (cross-sheet protection) |
| LedgerToolCommands.cs | 399/399 | notes |
| LedgerShapeCommands.cs | 571/571 | fixed (z-order, protection at source) |
| LedgerGalleryCommands.cs | 218/218 | fixed (z-order) |
| LedgerFormCommands.cs | 81/81 | fixed (z-order) |
| LedgerSignatureCommands.cs | 56/56 | fixed (z-order) |
| LedgerImageCommands.cs | 336/336 | note |
| LedgerSpellingCommands.cs | 320/320 | clean |
| LedgerPartialCommands.cs | 275/275 | note |
| LedgerRibbonCommands.cs | 258/258 | fixed (font size cap, locale date) |
| LedgerStyleCommands.cs | 251/251 | note |
| LedgerMiscCommands.cs | 245/245 | note (duplicate commands) |
| LedgerCommandSet.cs, LedgerProtectionGuard.cs, SheetImageInsertion.cs, SheetValueWrites.cs | all | context; SheetImageInsertion restructured |
| DataDialogs/GoalSeekView.cs | 126/126 | note (probe cost) |
| DataDialogs/RandomView.cs | 208/208 | clean |
| DataDialogs/SheetProtectionView.cs | 94/94 | clean |
| DataDialogs/SheetMoveView.cs | 156/156 | note (duplicated sheet-name rule) |
| DataDialogs/DataFormView.cs | 195/195 | clean (see note on FormAsync) |
| Conditional/ConditionalRuleCompletion.cs | 56/56 | clean |
| Conditional/ConditionalRulesManagerView.cs | 254/254 | note (async void handlers) |
| PageLayout/SheetPageSetupView.cs | 342/342 | note (margins not checked against paper) |

Not read in this part (left for the next round): the other `Ledger*Commands` files (Calculation, CellFormat,
CellShift, Change, Comment, Compare beyond its file-reading method, Data, DocumentExtra, DocumentOperations, Draw,
EditData, ExternalData, FormDialog, FormatDialog, Function, Insert, Macro, Merge, Name, Object, ObjectFile, Pane,
Panel, Paste, Print, Redaction, Script, Selection, SelectionExtra, ShellView, Sparkline, Structure, TableInsert, Text,
View, Web, Zoom), 22 of the 27 views in `DataDialogs/` and `ConditionalRuleCatalog`, `ConditionalRuleEditorView`,
`ConditionalRulePreview`.

## Defects

### Serious

1. **Refreshing external data whose source became unreadable wiped the linked area.** `LedgerTailCommands.Read`
   returned `TabularData.Empty` on a read failure, and `RefreshAsync`, `ExternalLinksAsync`, `StreamsAsync`,
   `XmlSourceAsync`, `ProviderAsync` then wrote the empty table, which clears the previous area
   (`ExternalDataWriter.Write` clears `previous`). Fix: `Read` returns `null` on failure, every caller stops (refresh
   counts it as a failure and leaves the area alone). Test `AggiornareUnaFonteIlleggibileNonCancellaIDatiNeAggiungeUnPassoVuoto`.
2. **Goal seek and solver wrote locked cells of another protected sheet.** The variable cells can live on any sheet;
   `LedgerProtectionGuard` only watches the active one, and `WriteNumbers` did not check. Fix: `WriteNumbers` asks
   `ISheetEditGuard.CanWrite` for every target cell's own sheet and refuses with the protection message.
   Test `LaRicercaObiettivoNonScriveUnaCellaBloccataDiUnAltroFoglioProtetto`.

### Medium

3. **Two protection policies for conditional rules.** `LedgerConditionalRuleCommands` used
   `ISheetEditGuard.CanFormat` (protected sheet with "format cells" allowed accepts rules), `LedgerConditionCommands`
   refused any protected sheet with the "cell is protected" message. Restructured: a single `ISheetFormatGate`
   (`SheetFormatGate`, registered in `LedgerCoreComposition`) built on `CanFormat`, used by both command sets.
   Tests `GestireLeRegoleSuUnFoglioProtettoUsaLaStessaPoliticaDelFormato`, `IlCancelloDelFormatoLasciaPassareIFogliCheConsentonoIlFormato`.
4. **Inserted objects went under existing ones.** Only charts set `ZOrder`; shapes, gallery shapes, text boxes,
   fontwork, QR codes, formulas, form controls, signature lines and images were added with `ZOrder = 0`, so after any
   reorder (which renumbers 0..n-1) a new object was drawn below all but the bottom one. Restructured:
   `ISheetImageInsertion.Insert(PlacedObject, label)` is the single insertion path (read-only and protection refusal,
   `ZOrder = NextOrder`, history step, selection); `InsertObject`, `LedgerShapeCommands`, `LedgerChartCommands`,
   `LedgerGalleryCommands`, `LedgerFormCommands`, `LedgerSignatureCommands` go through it. This also moves the
   protection check of those insertions to the source (before, only the after-the-fact net caught them).
   Test `UnOggettoInseritoVaSopraQuelliGiaRiordinati`.
5. **Chart anchored beyond the last column.** With a selection ending in the last two columns the anchor was
   `End.Column + 2`, an invalid address. Clamped to the last column. Test `IlGraficoInseritoSulleUltimeColonneRestaDentroIlFoglio`.
6. **Database range name not validated.** `ledger.database.define` accepted any text (`B2`, `TRUE`, spaces, symbols),
   which then breaks references and the file formats. It now uses `IDefinedNameRules` and reports `names.invalid`;
   the data-provider dialog in `LedgerTailCommands` used a whitespace-only check and now uses the same rules.
   Test `UnNomeDiIntervalloDiDatabaseNonValidoVieneRifiutato`.
7. **Sheet from file: `new OdsReader()` and ignored token.** `FromFileAsync` built the reader by hand (no DI, ODS only)
   and passed `CancellationToken.None`. It now reads through `ISpreadsheetFormatRegistry` (every readable format, as
   the compare command does) and forwards the command token to the file dialog, the stream and the copy.
   Test `InserireUnFoglioDaFileRispettaIlTokenEOffreTuttiIFormatiLeggibili`.
8. **Empty undo steps from external data.** `ExternalLinksAsync`, `XmlSourceAsync`, `ProviderAsync` and `RefreshAsync`
   pushed a checkpoint step even when the write was refused (protected sheet, too large) or nothing was refreshed.
   They now use the `applied` overload of `Checkpointed`. Covered by the refresh test above (`History.CanUndo` is false).
9. **Ribbon font size without bounds.** `ledger.font.size` with an argument (macro, agent, typed value) applied any
   double, including negative, NaN or 1e9. Now finite and clamped to 1..409. Test `LaDimensioneDelCarattereDaArgomentoHaUnTetto`.
10. **Ribbon "short date" ignored the locale.** It always applied `dd/mm/yyyy`; it now uses `ISystemDateCode`, as the
    format dialog and input recognition do.

### Minor (noted, not changed)

- `LedgerFilterCommands.ShowMenuAsync` refuses a sort with `status.protection.locked` instead of a sort-specific text
  (`LedgerToolCommands.SortAsync` does the same).
- `LedgerToolCommands.TextToColumnsAsync` checks `CanWrite` only on the selection, while the split writes columns to
  its right; locked cells there are still caught by `LedgerProtectionGuard` (active sheet).
- `LedgerShapeCommands` connector, align and order commands do not disable on a protected sheet (the net refuses the
  change); `LedgerMiscCommands` registers `toForeground`/`toBackground`, a second copy of `toFront`/`toBack` with its
  own protection check: duplicated commands.
- `LedgerShapeCommands.InsertVectorAsync`/`InsertRasterAsync` read the chosen file without a size cap.
- `LedgerPartialCommands.Unique` and `LedgerSheetExtraCommands.Unique` can produce names longer than 31 characters;
  `SheetMoveView.NameProblem` duplicates the sheet-name rule instead of using a shared service.
- `GoalSeekView` runs a full goal seek on every keystroke through `EngineGoalSeekProbe`: on large models this can be slow.
- `ConditionalRulesManagerView` uses `async` lambdas on events (async void); an exception from the nested editor would
  be unobserved.
- `SheetPageSetupView` accepts margins up to 200 mm each without checking them against the paper size.
- `LedgerSheetExtraCommands.FormAsync` writes a new record on the row after an explicit selection without checking it
  is empty; random, shuffle and form writes go through `SheetValueWrites`, which snapshots the whole sheet.
- Sheet from file copies values and formulas only (no styles, sizes, objects): a functional limit.

## Files changed

- `src/PulseOffice.Sheets.UI/SheetFormatGate.cs` (new), `LedgerConditionCommands.cs`, `LedgerConditionalRuleCommands.cs`
- `src/PulseOffice.Sheets.UI/SheetImageInsertion.cs`, `LedgerShapeCommands.cs`, `LedgerChartCommands.cs`,
  `LedgerGalleryCommands.cs`, `LedgerFormCommands.cs`, `LedgerSignatureCommands.cs`
- `src/PulseOffice.Sheets.UI/LedgerDatabaseRangeCommands.cs`, `LedgerTailCommands.cs`, `LedgerAnalysisCommands.cs`,
  `LedgerSheetExtraCommands.cs`, `LedgerRibbonCommands.cs`
- `src/PulseOffice.Desktop/Composition/LedgerCoreComposition.cs` (one registration, outside the area, needed for DI)
- tests: `LedgerRound6bAuditTests.cs` (new), constructor updates in `LedgerCommandsAuditTests.cs`,
  `LedgerCommandsAuditA2Tests.cs`, `LedgerProtectionOutsideHistoryTests.cs`, `SheetsUiRootAuditTests.cs`

## Live checks

- Protect a sheet allowing "format cells": Format > Conditional > Condition and Manage now open; without that
  permission they refuse with the format message.
- Insert a shape, reorder objects (bring to front/back), insert another shape, text box, form control, signature line:
  the new object must appear on top.
- Select the last two columns (XFC:XFD) and insert a column chart: it must appear at the last column.
- Define a database range named `B2`: refused with the invalid-name message.
- Sheet > Insert from file: the dialog offers every readable spreadsheet format (XLSX too).
- Link an external CSV, then make it unreadable and refresh: the linked cells must stay; no new undo step.
- Goal seek with the variable cell on another protected sheet: refused, the cell keeps its value.
- Ribbon short date with an English (US) system locale: the applied code follows the system pattern.
