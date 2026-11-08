# Deep audit: Folio

Area: `PulseOffice.Documents.UI`, `PulseOffice.Documents`, `PulseOffice.Documents.Layout`, `PulseOffice.Document`,
`PulseOffice.Document.Commands`, `PulseOffice.Document.Data` (39,882 lines of C#).

## Coverage
**Partial, not complete.** Read in full: `Editing/FolioProtectedContentGuard.cs`, `Commands/FolioCommandSet.cs`,
`Protection/EditGuard.cs`, `Protection/DocumentProtection.cs`, `Blocks/Block.cs`, `Blocks/TableBlock.cs`; in part:
`Editing/FolioEditor.cs` (lines 1–550), `Blocks/BlockTree.cs`, `Fields/FolioFieldsModel.cs` and
`Shell/FolioSnapshotProvider.cs`. Over the whole area: targeted searches for `async void`, `.Result`, `.Wait()`,
`GetResult`, broad `catch`es, and for `Document, … History` writes in the commands (28 command files write through the
services, only 3 check `RefusesEdit`). All the other files **are still to be read**: the rule "every file in
full" is not yet satisfied.

## Defects
### Severe (fixed)
- `Documents.UI/Editing/FolioProtectedContentGuard.cs`: the net for commands that write through the services bypassing
  the editor (open point of the review) covered only protected sections. With the document protected as read-only
  or "comments only", and in protected cells, commands such as "Numbered list" changed the text. Now the guard
  snapshots three kinds of region (section, cell, whole body under document protection; in the body the comment marks
  do not count, so commenting stays possible; removing protection from a section or cell stays allowed) and
  undoes back to the starting point. Tests in `ContextRefusalTests`.

### Minor (fixed in the second round)
- `Documents.UI/Fields/FolioFieldsModel.cs:206`: caught `NullReferenceException`; `FolioEditor.Layout` is not
  nullable, so the filter only hid bugs. Removed.
- `Documents.UI/Shell/FolioSnapshotProvider.cs:81`: an unreadable snapshot is now written to the log (warning, English).

## Remaining
- "Forms" protection (`DocumentRestriction.Forms`): not covered by the guard, because filling in the controls changes the
  paragraphs; writes inside form controls need to be told apart.
- Complete reading of all the files of the area: the second round did not do it. Almost all of the
  302 files are still to be read (Documents.UI 124 / 18,345 lines, Documents 107 / 12,227, Documents.Layout 33 / 5,803, Document 34 / 3,206,
  Document.Commands 3 / 260, Document.Data 1 / 92). To be resumed subfolder by subfolder.

## To try live
- Document protected as "comments only": "Numbered list", bold, insert table → refused with the message
  in the status bar; insert a comment → allowed.
- Protected cell: formatting from the ribbon refused; removing protection from the cell stays possible.

## Third round: full reading of `Documents.UI/Commands`
Every file opened with Read in full (lines read = total).

| File | Lines | Outcome |
|---|---|---|
| Commands/AutoTextView.cs | 188/188 | no defect |
| Commands/EnvelopePreview.cs | 69/69 | no defect |
| Commands/FolioAccessibilityCommands.cs | 140/140 | no defect |
| Commands/FolioAutoTextCommands.cs | 128/128 | **medium, fixed**: `Save` opened the file with `File.Create` before writing; a failed write left an unreadable entry and, when overwriting, destroyed the good one. Now it writes in memory and only on success to disk. Test `UnaScritturaFallitaNonRovinaLaVoceEsistente` |
| Commands/FolioBreakCommands.cs | 100/100 | no defect |
| Commands/FolioCellCommands.cs | 95/95 | no defect |
| Commands/FolioCharacterCommands.cs | 77/77 | no defect |
| Commands/FolioCharacterDialog.cs | 113/113 | no defect |
| Commands/FolioClipboardCommands.cs | 64/64 | no defect |
| Commands/FolioCommandSet.cs | 43/43 | no defect |
| Commands/FolioContentControlCommands.cs | 48/48 | no defect |
| Commands/FolioContentControls.cs | 195/195 | minor: `Activate` launches `ChooseAsync` without awaiting it (`_ =`), an exception from the modal would be lost; left |
| Commands/FolioDialogCommands.cs | 68/68 | minor: default tab stop and paragraph properties are two distinct history steps; left |
| Commands/FolioDrawCommands.cs | 43/43 | no defect |
| Commands/FolioEditCommands.cs | 59/59 | no defect |
| Commands/FolioExportCommands.cs | 67/67 | no defect |
| Commands/FolioFieldCommands.cs | 302/302 | no defect |
| Commands/FolioFindCommands.cs | 108/108 | **severe, fixed**: "Replace all" wrote through the services outside any command (no effect guard) and also replaced in protected sections, cells and documents; now it skips occurrences that cannot be modified. Test `FolioFindProtectionTests` |
| Commands/FolioFindTarget.cs | 35/35 | no defect |
| Commands/FolioForms.cs | 47/47 | no defect |
| Commands/FolioImageCommands.cs | 116/116 | no defect |
| Commands/FolioIndexCommands.cs | 367/367 | no defect |
| Commands/FolioInsertCommands.cs | 200/200 | no defect |
| Commands/FolioListCommands.cs | 207/207 | no defect |
| Commands/FolioMailMergeCommands.cs | 190/190 | minor: choosing the data source sets `MergeSource` without a history step (⌘Z does not remove it); left |
| Commands/FolioMasterCommands.cs | 172/172 | no defect |
| Commands/FolioNavigationCommands.cs | 26/26 | no defect |
| Commands/FolioObjectCommands.cs | 177/177 | no defect |
| Commands/FolioObjectDialogs.cs | 209/209 | no defect (order of the `Margins` verified: left, top, right, bottom) |
| Commands/FolioObjectInsertCommands.cs | 140/140 | no defect |
| Commands/FolioPageCommands.cs | 110/110 | no defect |
| Commands/FolioPageDialog.cs | 265/265 | no defect |
| Commands/FolioParagraphCommands.cs | 105/105 | no defect |
| Commands/FolioParagraphDialog.cs | 222/222 | minor: the "Flow" tab cannot remove an already set page-numbering restart or page style (0 and "same style" produce no change, because `ParagraphProperties` has no value for "remove"); left, it needs a sentinel in the model |
| Commands/FolioReviewCommands.cs | 381/381 | no defect |
| Commands/FolioRibbonCommands.cs | 256/256 | no defect |
| Commands/FolioSectionCommands.cs | 124/124 | no defect |
| Commands/FolioSendCommands.cs | 71/71 | no defect |
| Commands/FolioShapeCommands.cs | 208/208 | no defect |
| Commands/FolioSpellingCommands.cs | 198/198 | minor: in the spell check, a refused replacement (protected zone) with "Change all" to an empty text would leave the cursor stuck on the same word; edge case, left |
| Commands/FolioStyleCommands.cs | 106/106 | no defect |
| Commands/FolioTableCommands.cs | 292/292 | no defect (`optimalRowHeight` and `minimalRowHeight` do the same thing, automatic height, as in the reference word processor for rows without a fixed height) |
| Commands/FolioTableDialogs.cs | 278/278 | minor: the column letters in the reordering restart from A beyond the twenty-sixth (`'A' + column % 26`); left |
| Commands/FolioTableLooks.cs | 28/28 | no defect |
| Commands/FolioTextToolCommands.cs | 168/168 | no defect |
| Commands/FolioTitlePageCommands.cs | 56/56 | no defect |
| Commands/FolioToolCommands.cs | 379/379 | no defect |
| Commands/FolioViewCommands.cs | 58/58 | no defect |
| Commands/FolioZoomCommands.cs | 45/45 | no defect |
| Commands/TableGridPreview.cs | 96/96 | no defect |
| Commands/TablePlacementPreview.cs | 82/82 | no defect |

**Coverage of `Commands`: 51 files of 51, 7,321 lines read of 7,321.** Defects: 1 severe and 1 medium fixed with tests;
6 minor noted and left.

### Full reading of `Documents.UI/Editing`

| File | Lines | Outcome |
|---|---|---|
| Editing/FolioEditRefusals.cs | 20/20 | no defect |
| Editing/ContentControlPlaceholders.cs | 21/21 | no defect |
| Editing/FolioObjectCycle.cs | 29/29 | no defect |
| Editing/PreeditLayout.cs | 30/30 | no defect (state restored in `finally`) |
| Editing/FolioContextMenus.cs | 33/33 | no defect |
| Editing/PendingCharacterFormat.cs | 39/39 | no defect |
| Editing/FolioObjectHits.cs | 42/42 | no defect |
| Editing/FolioImages.cs | 48/48 | no defect |
| Editing/FolioEditingServices.cs | 50/50 | no defect |
| Editing/ParagraphOrder.cs | 52/52 | minor (performance): `Compare` walks all paragraphs on every comparison; used as a sort comparer (`FolioReviewCommands.StepComment`) it costs O(c·log c·n) on large documents; left |
| Editing/FolioFieldRefresh.cs | 65/65 | no defect |
| Editing/FolioRichTransfer.cs | 68/68 | no defect |
| Editing/FolioCaretSpeech.cs | 70/70 | no defect |
| Editing/FolioPageStyles.cs | 71/71 | no defect |
| Editing/FolioLinks.cs | 84/84 | no defect |
| Editing/CaretNavigator.cs | 97/97 | no defect |
| Editing/LayoutLines.cs | 104/104 | no defect |
| Editing/FolioPageStatus.cs | 109/109 | no defect (service living as long as the app: the events hooked in the constructor need not be unhooked) |
| Editing/FolioTypingAssist.cs | 112/112 | no defect |
| Editing/FolioKeyMap.cs | 113/113 | **severe, fixed**: Tab and Shift+Tab at the start of a list item changed the level with `editor.Change`, which looks only at read-only; the key does not go through the command registry, so no effect guard: in protected sections, cells and documents the level changed. Now `RefusesEdit()` refuses and says so. Test `FolioListKeyProtectionTests` |
| Editing/FolioDrawing.cs | 115/115 | no defect (insertion goes through `editor.Insert`, which refuses protected zones) |
| Editing/FolioProtectedContentGuard.cs | 118/118 | no new defect; the known limit remains: it does not look at objects nor at "forms" protection |
| Editing/FolioProofing.cs | 129/129 | no defect (cache with a cap, emptied on dictionary changes) |
| Editing/FolioObjectMover.cs | 131/131 | **severe, fixed**: dragging, moving with the arrows or re-anchoring an object looked only at read-only; mouse and arrows do not go through the command registry, so in the protected document (read-only, comments, forms) and with the anchor in a protected section or cell the object moved. Now `Locked` refuses. Test `NelDocumentoProtettoGliOggettiNonSiSpostano` |
| Editing/FolioNavigatorSource.cs | 146/146 | **severe, fixed**: the Navigator moved, promoted and dragged chapters looking only at read-only (the panel does not go through the command registry): in the protected document chapters were reordered. Now `CanMove`/`CanDrop` ask for the guard's verdict. Test `FolioNavigatorProtectionTests` |
| Editing/FolioObjectEditing.cs | 215/215 | no defect (called by the commands, covered by the effect guard) |
| Editing/FolioReviewSource.cs | 214/214 | **minor, fixed**: from the Review panel "Resolve" did not check `CanComment` like the other actions, and in the read-only document marked the comment as resolved. Test `NelDocumentoInSolaLetturaIlCommentoNonSiRisolve` |
| Editing/FolioEditor.cs | 863/863 (now 871) | **severe, fixed**: Tab in the last cell added a row even in the protected document or in the protected cell (`MoveToCell` looked only at read-only): now `RefusesEdit()`. **Severe, fixed**: the Delete key on a selected object and rotating or resizing with the mouse (`DeleteSelectedObject`, `ChangeSelectedObject`) bypassed protection; now `ObjectLocked`, shared with `FolioObjectMover`. **Medium, fixed** (in `Commands/FolioCellCommands.cs`): number recognition wrote into the cell just left even if protected. Tests `TabNellUltimaCellaNonAggiungeRigheNelDocumentoProtetto`, `NelDocumentoProtettoGliOggettiNonSiSpostano` (verified red by removing the fix). Minor, left: `Replace` checks the guard only on the starting paragraph of the range |

**Coverage of `Editing`: 28 files of 28, 3,188 lines read of 3,188.**

### Summary of the third round
- Files read in full: 79 (51 in `Commands`, 28 in `Editing`), 10,509 lines.
- Defects: 7 severe fixed (Replace all, Tab in lists, Tab in the last cell, objects with mouse and arrows,
  Delete/rotate/resize objects, Navigator), 2 medium fixed (glossary, number recognition), 1 minor fixed
  (Resolve from the Review panel), 8 minor left and noted above.
- Common root of the severe ones: actions that do not go through the command registry (keyboard, mouse, panels, search
  windows) are not covered by `FolioProtectedContentGuard`, and `FolioEditor.Change` looks only at read-only. Still to
  evaluate whether to move the protection check inside `Change` itself (it changes the behaviour of dozens of callers:
  to be done with the sweep of all commands in `Desktop.Sweep.Tests`).

### To try live
- Document protected as "comments only": Find and replace → Replace all changes nothing; Tab at the start of a list
  item; Tab in the last cell of a table; dragging, rotating, resizing, moving with the arrows and
  deleting an image; moving chapters in the Navigator (arrows disabled).
- Glossary (⌃F3): saving an entry in a folder without write permission leaves no unreadable entries.

## Fourth round: the rest of `Documents.UI`

Folders `Canvas`, `Fields`, `Rendering`, `Shell` (with `Shell/Snapshot`), `Scripting`, `StyleCatalog`, `Indexes`, `Exchange`, `Fonts`
and the files at the root, read in full one by one.

| File | Lines read | Outcome |
|---|---|---|
| Canvas/FolioCanvas.cs | 1120/1120 (now 1121) | **severe, fixed**: dragging the column border (`ResizeColumn`) and the shape adjustment handles (`IFolioObjectEditing.Replace`) looked only at read-only; now the column asks for the guard's verdict on every cell and the handles neither appear nor act on a locked object (`ObjectLocked`). The "Add header/footer" button does not appear in the protected document |
| Canvas/FolioCommentMargin.cs | 175/175 | no defect |
| Canvas/FolioRulers.cs | 154/154 | **severe, fixed** (in `Editing/FolioPageStyles.cs`): dragging the margins on the ruler changed the page style in the protected document (`Change` looked only at read-only); now `RefusesDocumentEdit()`. Indents and tab stops go through `Apply`, already protected |
| Canvas/FolioViewLayout.cs | 80/80 | no defect |
| Canvas/PageDesk.cs | 144/144 | no defect |
| Exchange/FolioExchange.cs | 108/108 | no defect (pasting goes through `Paste`/`Type`, protected); minor, left: `Type` returns true even if every line is refused |
| Fields/FolioFieldInsertion.cs | 85/85 (now 89) | **medium, fixed**: editing a field (`Replace`) looked only at read-only; the cross-reference prepared the target bookmark even when the insertion was refused. Now `RefusesEdit` on the field's paragraph and before preparing the reference |
| Fields/FolioFieldOptions.cs | 248/248 | no defect |
| Fields/FolioFieldsDialog.cs | 25/25 | no defect |
| Fields/FolioFieldsModel.cs | 229/229 (now 231) | **medium, fixed**: the user field wrote the document variable (`Commit`) before the insertion was refused at a protected position; now the model refuses before any write |
| Fields/FolioFieldsView.cs | 480/480 | no defect |
| Fonts/FolioFaces.cs | 77/77 | no defect |
| Fonts/FolioFontCollector.cs | 26/26 | minor, left: it does not collect the fonts of lists, frames and objects for embedding |
| Indexes/FolioIndexEntryPanel.cs | 176/176 | **severe, fixed**: the Index Entries panel inserted the mark into the protected paragraph (`editor.Change` looks only at read-only); now `RefusesEdit()` |
| Indexes/FolioIndexGallery.cs | 213/213 | no defect |
| Rendering/EmbeddedRasterizer.cs | 64/64 | no defect |
| Rendering/FolioChangeBars.cs | 34/34 | no defect |
| Rendering/FolioCommentSheet.cs | 54/54 | minor, left: the quotation of the commented text is searched only among the top-level paragraphs of the body |
| Rendering/FolioPrintMarks.cs | 97/97 | no defect |
| Rendering/FolioPrintable.cs | 81/81 | minor, left: the print paper size is that of the first page even with pages of different orientation |
| Rendering/FolioSpotlight.cs | 117/117 | no defect |
| Rendering/GlyphRunPainters.cs | 137/137 | no defect |
| Rendering/PageRenderer.cs | 415/415 | no defect |
| Rendering/PageSurfaces.cs | 241/241 | minor, left: a new brush for every stroke drawn (allocations in the drawing loop) |
| Scripting/FolioMacros.cs | 162/162 | minor, left: "Form properties" changes `FormSettings` without looking at document protection (the effect guard looks only at paragraphs) |
| Scripting/FolioScripting.cs | 492/492 (now 496) | **severe, fixed**: `AcceptAll`/`RejectAll` resolved the tracked changes and `SetTrackChanges(false)` turned recording off even when the "tracked changes only" protection or the key forbids it; now `CanResolveChanges`/`ForcesRecording` |
| Shell/FolioDocumentFactory.cs | 36/36 | no defect |
| Shell/FolioDocumentOperations.cs | 78/78 | minor, left: `CompareVersion` and `PreviewFile` do not catch I/O errors (the shell caller handles them) |
| Shell/FolioDocumentService.cs | 57/57 | no defect |
| Shell/FolioMergeOutput.cs | 141/141 | minor, left: if a file fails halfway the count of those already written is lost from the message |
| Shell/FolioMergeWizard.cs | 285/285 | no defect |
| Shell/FolioSnapshotProvider.cs | 110/110 | no defect |
| Shell/Snapshot/AnnotationConverters.cs | 133/133 | no defect |
| Shell/Snapshot/BlockConverters.cs | 100/100 (now 106) | **severe, fixed**: the recovery snapshot lost the cells' `Protected` and `NumberFormat` and the tables' `Direct`, `PageStyleName`, `PageBreakBefore`: after a recovery protected cells became editable again |
| Shell/Snapshot/DocumentConverters.cs | 96/96 (now 105) | **severe, fixed**: it lost the protection of marks (`Marks`), line numbering, `IsMaster`, the mail merge source and the bibliography |
| Shell/Snapshot/SnapshotNodes.cs | 47/47 | no defect |
| Shell/Snapshot/SnapshotTypes.cs | 82/82 | no defect |
| Shell/Snapshot/SnapshotValidator.cs | 47/47 | no defect |
| Shell/Snapshot/StyleConverters.cs | 144/144 (now 146) | **minor, fixed**: it lost `DefaultTabInterval` and `TabsRelativeToIndent` |
| StyleCatalog/FolioStyleDialogs.cs | 177/177 (now 183) | **severe, fixed** (same root): "Modify style" wrote into the protected document; now it refuses before opening and before writing |
| StyleCatalog/FolioStyleNames.cs | 50/50 | no defect |
| StyleCatalog/FolioStylesSource.cs | 401/401 (now 405) | **severe, fixed**: the Styles panel applied page, frame and table styles and created, derived, updated and deleted styles looking only at read-only (the panel does not go through the command registry); now `RefusesEdit` to apply and `RefusesDocumentEdit` for the definitions, and the buttons are disabled (`Locked`, `DocumentLocked`) |
| ActiveFolioContext.cs | 50/50 | no defect |
| FolioServiceCollectionExtensions.cs | 249/249 | no defect |
| FolioView.cs | 323/323 | **medium, fixed**: clicking "Add header" enabled the header in the protected document and the direct cursor added paragraphs after a protected paragraph; now `RefusesDocumentEdit()` and `RefusesEdit(target)`. Minor, left: Undo/Redo from `Perform` look only at read-only |

**Coverage: 45 files of 45. 7,840 lines read of 7,840.**

### Fix at the root
New checks in `FolioEditor`: `RefusesEdit(paragraphId)` (the guard's verdict on a given paragraph), `DocumentLocked()` and
`RefusesDocumentEdit()` (restriction of the whole document: read-only, comments only, forms only), which warn through `Refused`
like `RefusesEdit()`. `ObjectLocked` and `IFolioObjectEditing.Change`/`Replace` use them: the commands on objects too, which the
effect guard does not see, now respect protection.

### Summary of the fourth round
- Defects: 7 severe fixed (margin ruler, Styles panel and dialog, Index Entries panel, column border with the mouse,
  adjustment handles and object commands, macros on tracked changes and recording, snapshot losing the protection of
  cells and marks), 3 medium fixed (fields: user variable and bookmark written before the refusal, editing a protected
  field; header by click and direct cursor), 1 minor fixed (tab stops in the snapshot), 9 minor left and noted.
- Tests: `FolioPanelProtectionTests` (8 cases), each verified red before the fix.

### To try live (fourth round)
- Document protected as "comments only": dragging the margins on the ruler; applying a page style from the Styles panel, creating,
  modifying and deleting a style (buttons disabled); inserting an entry from the Index Entries panel; dragging the border of a
  table column; dragging the yellow handle of a shape; the "Add header" button above the page does not appear.
- Direct cursor active, last paragraph in a protected section: clicking below the text does not add paragraphs.
- Recovery after a forced close of a document with protected cells: the cells stay protected.

## Fifth round (layout, common model, commands and data)

Scope: `src/PulseOffice.Documents.Layout` (33 files, 5,803 lines), `src/PulseOffice.Document` (34 files, 3,206 lines),
`src/PulseOffice.Document.Commands` (3 files, 260 lines), `src/PulseOffice.Document.Data` (1 file, 92 lines). Every file read in
full with Read; the log is updated and committed every five files.

| File | Lines read | Outcome |
|---|---|---|
| Documents.Layout/Flow/Paginator.cs | 1264/1264 (now 1265) | **blocking, fixed**: a splittable table row with a minimum height greater than the page and cells without content (no atoms) sent the layout into an infinite loop: `Split` made no progress, returned `None`, `Advance` opened an empty page and it repeated. Now, with an empty column and exhausted cells, the row is closed cut at the available height (as the reference word processor does). Test `EdgeCaseAuditTests` (red: timed out at 20 s) |
| Documents.Layout/Flow/TableMeasure.cs | 47/47 | no defect |
| Documents.Layout/Flow/Stories.cs | 394/394 | no defect (the cause of the loop is in the paginator: `RowPlacement.Height` includes the minimum height even without atoms, which is right) |
| Documents.Layout/Flow/LayoutEngine.cs | 347/347 | no defect |
| Documents.Layout/Tree/LayoutTree.cs | 529/529 | minor, left: `PageBox.Children` builds on every read a `HashSet` of the rows in the tables (allocation in the drawing loop) |
| Documents.Layout/Flow/FlowBuilder.cs | 338/338 | no defect |
| Documents.Layout/Flow/PageFrames.cs | 274/274 | no defect (minimum body 1 pt when header and footer exceed the page: no divisions by zero) |
| Documents.Layout/Flow/Placement.cs | 244/244 | no defect (the balancer divides by `Columns.Count`, always ≥ 1) |
| Documents.Layout/Flow/ChangeMarkup.cs | 161/161 | minor, left: the `ChangeMarkup` cache by paragraph id is never emptied (one entry per paragraph with tracked changes, replaced on every edit) |
| Documents.Layout/Lines/LineComposer.cs | 153/153 | no defect |
| Documents.Layout/Lines/LineBreaker.cs | 152/152 (now 153) | **medium, fixed**: in a column narrower than a character outside the basic plane (emoji, extended ideographs) `Choose` cut the surrogate pair in half: the line showed an orphan surrogate (�) and the next one the other. Now the pair stays whole (it steps back before the high one or, if it is the first character of the line, advances after the low one, adding the missing width). Test `EdgeCaseAuditTests` (red before) |
| Documents.Layout/Flow/FlowItems.cs | 149/149 | no defect |
| Documents.Layout/Inline/ParagraphShaper.cs | 147/147 | no defect |
| Documents.Layout/Flow/ListLabeling.cs | 140/140 | minor, left: `LetterNumbers` for a huge value (numbering restart read from a file) builds a string value/26 characters long |
| Documents.Layout/Flow/ParagraphEntries.cs | 121/121 | no defect |
| Documents.Layout/Inline/RunTypesetting.cs | 120/120 | no defect (the case mappings use `char.ToUpper`, which preserves the length: offsets stay aligned) |
| Documents.Layout/Flow/ParagraphEntry.cs | 119/119 | no defect |
| Documents.Layout/Queries/LineOffsets.cs | 118/118 | no defect (the click uses the clusters of `ITextClusters`, no cursor inside a surrogate pair) |
| Documents.Layout/Queries/LayoutQueries.cs | 104/104 | no defect |
| Documents.Layout/Inline/ShapedParagraph.cs | 102/102 | no defect |
| Documents.Layout/Flow/FlowSettings.cs | 96/96 | no defect |
| Documents.Layout/Measurement/OpenType/OpenTypeMeasurer.cs | 95/95 | no defect (surrogate pairs are measured on the code point, the low one counts 0) |
| Documents.Layout/LayoutServiceCollectionExtensions.cs | 78/78 | no defect |
| Documents.Layout/Inline/InlineInserter.cs | 75/75 | no defect |
| Documents.Layout/Flow/PageFields.cs | 74/74 | no defect |
| Documents.Layout/Flow/PageBackdrops.cs | 64/64 | no defect (watermark: minimum width 1, angles at 0° and 90° without divisions by zero) |
| Documents.Layout/Lines/Tabs.cs | 58/58 | no defect (a null or negative tab interval falls back to 36 pt) |
| Documents.Layout/Measurement/TextMeasurement.cs | 57/57 | no defect |
| Documents.Layout/Flow/LayoutFieldFacts.cs | 48/48 | no defect |
| Documents.Layout/Lines/LineSpacing.cs | 41/41 | minor, left: `LeadingSpacing` with a negative extra spacing larger than the line produces a negative height (lines overlap; the paginator advances anyway) |
| Documents.Layout/Flow/PlaceholderMarkup.cs | 37/37 | no defect |
| Documents.Layout/Queries/LineNumbers.cs | 35/35 | no defect |
| Documents.Layout/Flow/PageNumbers.cs | 22/22 | no defect |
| Document.Commands/UndoStack.cs | 142/142 | no defect (an exception in `Undo`/`Redo` leaves the stack as it was) |
| Document.Commands/UndoableAction.cs | 69/69 | no defect |

**Documents.Layout: 33 files of 33, 5,803 lines of 5,803.**
| Document.Commands/RecordedMacroLibrary.cs | 49/49 | no defect |
| Document.Data/DataTableLoader.cs | 92/92 | minor, left: it catches only `DbException`; an error of another type from the provider (e.g. a driver's `InvalidOperationException`) goes up to the caller instead of becoming `LastError` |
| Document/Tables/TableFormulas.cs | 453/453 (now 470) | **severe, fixed** (three defects on input read from files): a range such as `<A1:XFD2000000000>` made `RangeNode` walk billions of cells (hang on opening and on recalculation); now the range is limited to the rows and columns of the table. An excessively nested formula (`((((…` or `----…1`) exhausted the stack and closed the process with no way to catch it; now beyond 256 levels it is a `TableFormulaException`. A column of too many letters overflowed the `int` into a negative index; now beyond 4 letters it is not a reference. Test `TableFormulaLimitsTests` (red before: the first hung, the third with the test host crashed) |
| Document/Forms/FormControlModel.cs | 239/239 (now 243) | **medium, fixed**: `NumericRange.Clamp` called `Math.Clamp(value, Minimum, Maximum)`, which throws `ArgumentException` when the minimum read from the file exceeds the maximum: a click on the spin button or the scroll bar of a form written this way made the command fail. Now the bounds are ordered (`Lower`, `Upper`) in `Clamp` and `Snap`. Test `FormControlRangeTests` (red before) |
| Document/Forms/FormControlCatalog.cs | 203/203 | no defect |
| Document/Forms/FormControlInteraction.cs | 153/153 | no defect (the cause was in `NumericRange`) |
| Document/Forms/MacroBindings.cs | 144/144 | no defect |
| Document/Embedding/TableSizing.cs | 195/195 | minor, left: `TableGeometry.Columns` recomputes `fractions.Sum()` for every column (quadratic in the number of columns, irrelevant at real sizes) |
| Document/Versions/VersionComparison.cs | 141/141 | minor, left: `RemovedFirst` is `private static` (pure helper, allowed) |
| Document/Embedding/TableStructure.cs | 137/137 | no defect |
| Document/Borders/BorderPresets.cs | 132/132 | no defect |
| Document/Embedding/DataTableObject.cs | 122/122 | no defect |
| Document/Embedding/ImageMap.cs | 101/101 | no defect (polygon: the horizontal side is skipped before the division) |
| Document/Comparison/SequenceDiff.cs | 86/86 | minor, left: below the cap of 16 million cells the LCS table `int[,]` can take up to 64 MB for one comparison |
| Document/Metadata/DocumentProperties.cs | 77/77 | no defect |
| Document/Versions/DocumentVersions.cs | 76/76 | no defect |
| Document/Forms/TabOrdering.cs | 69/69 | no defect |
| Document/Borders/BorderFrame.cs | 68/68 | no defect |
| Document/Tables/ColumnWidths.cs | 67/67 | minor, left: `Fit` with minimum 0 and requested widths all zero divides by zero (NaN); the callers pass a positive minimum |
| Document/Pages/PageSketchLayout.cs | 64/64 | no defect |
| Document/Pages/PageSketch.cs | 63/63 | no defect |
| Document/Tables/TableSpans.cs | 61/61 | no defect (the `Enclose` loop terminates: the range only grows, within the bounds of the merges) |
| Document/Tables/TableSorter.cs | 58/58 | minor, left: in descending order empty cells end up at the top instead of at the bottom, as the reference word processor does instead |
| Document/Forms/MacroLibrary.cs | 57/57 | no defect |
| Document/Fonts/DocumentFonts.cs | 57/57 | no defect |
| Document/Embedding/EmbeddedObject.cs | 52/52 | no defect |
| Document/Tables/TableStylePresets.cs | 49/49 | no defect (the names "Default Style", etc. are style names of the formats, not interface texts) |
| Document/Borders/BorderFrameResolver.cs | 48/48 | no defect |
| Document/Tables/TableCellRange.cs | 39/39 | no defect |
| Document/Forms/OptionGroups.cs | 39/39 | no defect |
| Document/Forms/SignatureLine.cs | 34/34 | no defect |
| Document/Themes/ThemePalettes.cs | 33/33 | no defect |
| Document/Embedding/PackagedObject.cs | 32/32 | minor, left: a packaged file declares `EmbeddedObjectKind.Table`, so the object index (`ObjectEntries`, outside the area) counts it among the spreadsheets |
| Document/Embedding/FormulaObject.cs | 21/21 | no defect |
| Document/Forms/ListBoxLayout.cs | 20/20 | no defect |
| Document/Forms/FormControlObject.cs | 16/16 | no defect |

**Coverage of the fifth round: 71 files of 71 (Documents.Layout 33, Document 34, Document.Commands 3, Document.Data 1). 9,361 lines read of 9,361.**

### Summary of the fifth round
- Blocking, fixed: infinite layout loop with a table row without content and a minimum height greater than the page (`Paginator.Split`).
- Severe, fixed: table formulas read from files (huge range that hangs, nesting that exhausts the stack and closes the process, column that overflows the `int`).
- Medium, fixed: surrogate pair split by line breaking in very narrow columns (`LineBreaker.Choose`); `NumericRange` with minimum and maximum swapped throwing exceptions on click.
- Minor left and noted: 11 (caches never emptied, allocations in `PageBox.Children`, negative line spacing, LCS up to 64 MB, `LetterNumbers` for huge values, descending sort of empty cells, `PackagedObject` counted among the sheets, `DataTableLoader` catching only `DbException`, `ColumnWidths.Fit` with zero minimum, quadratic `TableGeometry.Columns`, static `RemovedFirst`).
- New tests: `EdgeCaseAuditTests` (layout), `TableFormulaLimitsTests`, `FormControlRangeTests`, each seen red before the fix.

### To try live (fifth round)
- Folio: table with an empty row at a minimum height of 50 cm: the document opens and the row is cut at the page.
- Folio: very narrow table column with emoji: no replacement character (�).
- Folio: open an ODT with a table formula `<A1:XFD2000000000>` or heavily nested: no hang or crash.
- Ledger/Folio: form with a spin button with minimum > maximum: the click changes the value without errors.

## Sixth round (Folio's model and editing: `PulseOffice.Documents`)

Scope: `src/PulseOffice.Documents` (107 files, 12,227 lines), the last Folio project not yet read. Every file read in
full with Read; the log is updated and committed in groups of files.

| File | Lines read | Outcome |
|---|---|---|
| Fields/FieldCondition.cs | 258/258 (now 281) | **severe, fixed**: field conditions (hidden text, hidden paragraph, conditional text, variables) come from files; `((((…1` or `----…1` or `!!!!…1` made the parser recurse without limit until the stack was exhausted, and the process crashed with no way to catch it on field update. Now beyond 256 levels it is a `FormatException` (condition false, value left as it is). Test `FieldConditionLimitsTests` (red before: test host crashed) |
| Fields/FieldSources.cs | 230/230 (now 243) | **medium, fixed**: the creation and modification date fields applied the format read from the file without catching `FormatException` (e.g. `%`), as the date/time field already does: the whole field update failed. Now an invalid format falls back to `d`. Test `PropertyDateFormatTests` (red before) |
| Editing/Tables/TableConversion.cs | 223/223 (now 235) | **medium, fixed**: in the text → table conversion the separator was also searched inside a field's text; `Split` fired at the end of the field and deleting a character removed the first character *after* the field (text lost) or, at the end of the paragraph, threw `ArgumentOutOfRangeException`. Now the separator is searched only outside fields. Test `TableConversionFieldTests` (red before) |
| Editing/DocumentEditor.cs | 418/418 | no defect |
| Objects/AnchoredObject.cs | 278/278 | no defect |
| Editing/ParagraphText.cs | 248/248 | no defect (fields are atomic in `Snap`, `Atomic`, `Map`) |
| Styles/Styles.cs | 263/263 | minor, left: `ListStyle.Level` with an empty list of levels calls `Math.Clamp(level, 0, -1)` and throws; the ODT and DOCX readers always build the levels |
| Styles/DefaultStyles.cs | 288/288 | no defect |
| Controls/ContentControlEditing.cs | 245/245 | no defect (invalid date format already caught) |
| Editing/ObjectEditing.cs | 242/242 | no defect |
| Editing/RangeDeletion.cs | 235/235 | no defect |
| Fields/FieldContext.cs | 90/90 | no defect |
| Fields/FieldUpdater.cs | 172/172 | no defect |
| Revisions/DocumentComparison.cs | 220/220 | minor, left: `RemoveParagraphs` copies the list of paragraphs twice for every gap (`mine.ToList().IndexOf`), quadratic in the number of gaps |
| Editing/DocumentEdit.cs | 221/221 | no defect (undo restores in reverse order, redo from the after snapshots) |
| Indexes/IndexEditing.cs | 217/217 | no defect |
| Styles/Properties.cs | 219/219 | no defect |
| Clipboard/FragmentRemapper.cs | 193/193 (now 198) | **medium, fixed**: copying tables (paste, paste from another document, mail merge and text deleted with tracked changes, all of which go through `Remap`) silently lost the cells' number format and protection and the table's direct formatting, page style and page break. Now they are copied. Test `FragmentRemapTableTests` (red before) |
| Blocks/TableBlock.cs | 134/134 | no defect |
| Editing/TableEditing.cs | 197/197 | minor, left: `DeleteColumn` on an irregular table (rows with fewer cells) can leave rows without cells |
| Editing/StyleEditing.cs | 197/197 | no defect |
| Revisions/TrackedEditing.cs | 195/195 | no defect |
| Editing/ListEditing.cs | 194/194 | no defect (levels limited to 0…9 and 1…10) |
| Editing/StyleFromSelection.cs | 155/155 | no defect |
| Styles/DirectFormatting.cs | 192/192 | no defect |
| Fields/MailMerge.cs | 185/185 | no defect |
| Editing/Tables/TableFormatting.cs | 176/176 | no defect |
| Annotations/Comments.cs | 67/67 (now 76) | **severe, fixed**: `Thread` followed the replies by recursion without remembering the comments already seen; a cyclic `ParentId` read from the file (reply to itself, or two comments each a reply to the other) recursed forever — stack exhausted and process crashed on deleting or resolving the comment, or on deleting the text anchoring it — and a very long chain of replies was quadratic and as deep as the chain. Now an iterative visit with a set of seen comments and an index of replies. Test `ThreadAndIndexLetterTests` (red before) |
| Indexes/IndexEntries.cs | 165/165 (now 175) | **minor, fixed**: the letter separator of the alphabetical index took the first `char` of the entry; with an entry outside the basic plane (mathematical letters, extended ideographs, emoji) it showed an orphan surrogate (�) and grouped different entries under the same "letter". Now it takes the first code point (`Rune`), and an entry starting with an orphan surrogate does not throw. Test `ThreadAndIndexLetterTests` (red before) |
| Marks/InlineMark.cs | 98/98 | no defect (the path of an index entry always has at least the entry) |
| Commenting/CommentEditing.cs | 164/164 | no defect (`Root` already has a cap of 1000 steps; the cause of the loop was in `Thread`) |
| Styles/StyleSheet.cs | 168/168 | no defect |
| DocumentsServiceCollectionExtensions.cs | 167/167 | no defect |
| Editing/Tables/TableCellEditing.cs | 166/166 | no defect (the parts of a split are limited to 2…32) |
| TextDocument.cs | 152/152 | minor, left: `IdSequence.Reserve` accepts an identifier read from the file such as `p2147483647`; the following `Next` overflows (unchecked arithmetic) and produces negative identifiers, unique but ugly |
| Navigation/DocumentNavigation.cs | 148/148 | no defect (the short title cuts at 60 `char`s: it can split a surrogate pair only in the navigator entry, minor) |
| Clipboard/FragmentPaster.cs | 148/148 | no defect |
| Clipboard/FragmentExtractor.cs | 139/139 | no defect (the replies come from `Thread`, now without cycles) |
| Styles/StyleResolver.cs | 137/137 | no defect (the style chain already has the set of seen styles: no `ParentName` cycles) |
| Revisions/RevisionResolver.cs | 136/136 | no defect |
| Editing/Positions.cs | 129/129 | no defect |
| Accessibility/AccessibilityAudit.cs | 125/125 | no defect |
| Editing/ParagraphAutoFormat.cs | 116/116 | no defect (the start number has at most three digits) |
| Clipboard/FragmentScanner.cs | 116/116 | no defect (the style closure terminates: each round adds styles or stops) |
| Editing/Tables/CellNumberFormatting.cs | 113/113 (now 119) | **medium, fixed** (two defects): a cell in date format with a value outside the range of OLE dates (e.g. `99999999`, typed or read from the file) made `DateTime.FromOADate` throw and the format command or value recognition fail; now the value is shown as a number. An empty currency symbol read from the file made `string.Replace` throw on every recognition; now an empty symbol is not removed. Test `CellValueLimitsTests` (red before) |
| Revisions/ChangeMarking.cs | 115/115 | no defect |
| Fields/DocumentOutline.cs | 111/111 | no defect (`TextOf` limits the offsets to the length of the paragraph) |
| Editing/SectionEditing.cs | 105/105 | no defect |
| Controls/ContentControlCatalog.cs | 105/105 | minor, left: `All` walks the whole document again on every `Find` and `At` (linear per call, quadratic in `Refill`) |
| Annotations/TrackedChanges.cs | 105/105 | no defect |
| Editing/Tables/TableFormulaEditing.cs | 100/100 | no defect (recalculation stops after four passes even with circular references) |
| Protection/DocumentProtection.cs | 100/100 | minor, left: the iteration count read from the file goes up to 10 million (a few seconds of hashing on every password attempt); it is Word's cap |
| Fields/FieldEditing.cs | 98/98 | no defect |
| Revisions/TrackingDocumentEditor.cs | 98/98 | no defect |
| Editing/TextSearch.cs | 96/96 (now 99) | **medium, fixed**: case-insensitive search uses the culture comparison, which can match a text of a different length from the searched string (ignorable characters such as the soft hyphen, composed and decomposed forms); the range found, however, was computed with the length of the searched string, so it highlighted and replaced the wrong text (with "parola" on "pa­rola" an "a" remained after the replacement), and a match shorter than the searched string at the end of the paragraph was not found at all. Now the actual length of the match is used (`CompareInfo.IndexOf` with `matchLength`) and empty matches are discarded. Test `SearchMatchLengthTests` (red before) |
| Blocks/BlockTree.cs | 97/97 | no defect (the depth follows the nesting of tables, limited by the readers) |
| Editing/ContentTrails.cs | 96/96 | no defect |
| Editing/Borders/BoxBorderFrames.cs | 96/96 | no defect |
| Editing/Subdocuments.cs | 94/94 | minor, left: in `Refresh` the search for orphan objects is quadratic (`stale.Any(... AnchoredTo(id).Contains(item))`) |
| Navigation/ChapterMoving.cs | 93/93 | no defect |
| Protection/EditGuard.cs | 92/92 | no defect |
| Fields/ReferenceTargets.cs | 88/88 | minor, left: `Prepare` for a caption that no longer has a sequence field (target gone stale between list and choice) indexes `runs[-1]` and throws |
| Revisions/ChangeRegions.cs | 86/86 | no defect (a start mark without an end becomes a point) |
| Editing/FormatBrush.cs | 83/83 | no defect |
| Objects/ObjectGrouping.cs | 81/81 | no defect (width and height of the group at least 1: no divisions by zero) |
| Editing/Tables/TableGeometryEditing.cs | 80/80 | no defect |
| Annotations/Notes.cs | 79/79 | no defect |
| Editing/Envelopes.cs | 75/75 | no defect |
| Editing/Tables/TableGrid.cs | 72/72 | minor, left: `Whole` on a table without rows calls `Max` on an empty sequence and throws (the readers do not produce empty tables) |
| Editing/Tables/TablePasting.cs | 70/70 | no defect |
| Objects/FrameChains.cs | 67/67 | no defect (frame chains with a set of seen frames: no cycles from files) |
| Objects/GroupStories.cs | 67/67 | no defect (the recursion follows the nesting of groups, limited by the readers) |
| Controls/ContentControl.cs | 67/67 | no defect |
| Editing/TitlePages.cs | 64/64 | no defect |
| Editing/Tables/TableSorting.cs | 58/58 (now 62) | **medium, fixed**: on an irregular table (rows with fewer cells, as the DOCX and ODT file filters read them) `CanSort` — called to enable the command — and `Sort` indexed past the end of the row and threw `ArgumentOutOfRangeException`. Now an irregular table does not declare itself sortable and `Sort` does nothing. Test `IrregularTableSortTests` (red before, both) |
| Indexes/IndexDefinition.cs | 62/62 | no defect |
| Fields/Bibliography.cs | 62/62 | no defect |
| Blocks/Block.cs | 61/61 | no defect |
| Styles/StyleUsage.cs | 60/60 | no defect |
| Styles/TableAppearance.cs | 58/58 | no defect |
| Editing/ParagraphSorting.cs | 57/57 | no defect (stable sort, non-numeric text at the end) |
| Controls/LegacyFormFields.cs | 54/54 | no defect (a selected index out of range falls back to the first entry) |
| Navigation/AutoAbstract.cs | 52/52 | minor, left: an outline level beyond 10 read from the file produces a nonexistent "Heading 11…" style in the abstract (it resolves to the default style) |
| Editing/Stories.cs | 52/52 | no defect |
| Editing/Tables/TableBorderFraming.cs | 51/51 | no defect |
| Styles/TextThemes.cs | 49/49 | minor, left: a non-hexadecimal palette colour would make `FromRgbHex(...)!.Value` throw; the palettes are the predefined ones |
| Indexes/ObjectEntries.cs | 49/49 | no defect (a `PackagedObject` is classified by MIME type before its kind: the fifth-round note on `PackagedObject` counted among the sheets does not apply to the object index) |
| Clipboard/DocumentFragment.cs | 49/49 | no defect |
| Blocks/SectionBlock.cs | 48/48 | no defect |
| Clipboard/ObjectFragments.cs | 47/47 | no defect |
| Controls/ContentControlGuard.cs | 44/44 | no defect |
| Blocks/ParagraphBlock.cs | 41/41 | no defect |
| Editing/ObjectAnchoring.cs | 41/41 | no defect |
| Annotations/UnanchoredAnnotations.cs | 39/39 | no defect (`Root` has the set of seen items) |
| Objects/GroupMemberText.cs | 34/34 | no defect |
| Editing/DirectCursor.cs | 33/33 | no defect |
| Controls/ContentControlLockTokens.cs | 26/26 | no defect |
| Styles/UnusedHeaderVariants.cs | 24/24 | no defect |
| Marks/MarkOrder.cs | 23/23 | no defect |
| Objects/PortableObjects.cs | 22/22 | no defect |
| Editing/TypingRun.cs | 20/20 | minor, left: `TypingRun.Of` is a `static` factory method on the record (rule 1); it is pure and stateless |
| TextDocumentFactory.cs | 19/19 | no defect |
| Editing/Tables/TableSelections.cs | 19/19 | no defect |
| Revisions/AuthorColors.cs | 14/14 | no defect |
| Styles/LineNumbering.cs | 14/14 | no defect |
| Revisions/RevisionAuthor.cs | 13/13 | no defect |
| Blocks/BlockText.cs | 13/13 | no defect |

**Coverage of the sixth round: 107 files of 107 of `PulseOffice.Documents`, 12,227 lines read of 12,227 (before the fixes).**

### Summary of the sixth round
- Severe, fixed: unbounded recursion in the field condition parser (`((((…`, `----…`, `!!!!…` from files: stack exhausted and process crashed); comment threads with a cyclic `ParentId` read from the file (`CommentCollection.Thread` recursed forever on deletion or resolution).
- Medium, fixed: invalid date format in the creation and modification date fields; separator searched inside fields in the text → table conversion (text lost or exception); `FragmentRemapper` losing the number format, protection and direct formatting of copied tables; cells in date format outside the OLE range and an empty currency symbol throwing; case-insensitive search computing the range with the length of the searched string; sorting of irregular tables throwing already in the enablement check.
- Minor, fixed: letter separator of the alphabetical index splitting surrogate pairs.
- Minor left and noted: 13 (empty list levels, quadratic `DocumentComparison`, columns deleted in irregular tables, `IdSequence` overflow, control catalog walked again on every search, iteration count up to 10 million, quadratic `Subdocuments.Refresh`, `ReferenceTargets.Prepare` on a stale target, `TableGrid.Whole` on an empty table, outline level beyond 10 in the abstract, non-hexadecimal palette colour, short title of the navigator, static `TypingRun.Of`).
- New tests: `FieldConditionLimitsTests`, `PropertyDateFormatTests`, `TableConversionFieldTests`, `FragmentRemapTableTests`, `ThreadAndIndexLetterTests`, `CellValueLimitsTests`, `SearchMatchLengthTests`, `IrregularTableSortTests`, each seen red before the fix.

### To try live (sixth round)
- Folio: open an ODT with a hidden text field whose condition is nested thousands of times: the document opens and the fields update.
- Folio: open a DOCX with two comments each a reply to the other and delete or resolve the comment: no crash.
- Folio: copy and paste a table with cells in currency format and protected: format and protection remain.
- Folio: search "parola" with "Match case" off in a text containing "pa­rola" (soft hyphen) and replace: no leftover letter.
- Folio: table with rows of different length (from DOCX): the Sort menu opens disabled, without errors.
