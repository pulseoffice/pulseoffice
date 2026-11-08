# Deep audit, round 5: Folio interface, the remaining command sets

Area: the `PulseOffice.Documents.UI` command files that round 4 left unread ("Part 2", list "not read"), plus the three
points round 4 left open (macro paste after the history transaction, renaming a bibliography identifier, the paragraph
dialog unable to clear a page start).

Method: every file read in full with the reader; for each defect a test that fails on the original code (verified by
restoring the original sources with `git show HEAD:<file>` and running the new tests: all ten failed), then the fix.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Commands/FolioToolCommands.cs | 385/385 | **medium**, fixed (R5-D1) |
| Commands/FolioReviewCommands.cs | 383/383 | no defect (protection and change recording go through `ApplyOutsideHistory`) |
| Commands/FolioRibbonCommands.cs | 256/256 | no defect |
| Commands/FolioMailMergeCommands.cs | 190/190 | **medium**, fixed (R5-D2) |
| Shell/FolioMergeWizard.cs | 285/285 | no defect (range bounded and ordered by `Plan`) |
| Commands/FolioMasterCommands.cs | 172/172 | minor, left (R5-L2) |
| Commands/FolioInsertCommands.cs | 200/200 | no defect |
| Commands/FolioObjectCommands.cs | 177/177 | no defect |
| Commands/FolioShapeCommands.cs | 208/208 | no defect |
| Commands/FolioSpellingCommands.cs | 198/198 | no defect |
| Commands/FolioTextToolCommands.cs | 168/168 | **severe**, fixed (R5-D3) |
| Commands/FolioAccessibilityCommands.cs | 140/140 | no defect |
| Commands/FolioAutoTextCommands.cs | 130/130 | minor, left (R5-L3) |
| Commands/AutoTextView.cs | 188/188 | no defect |
| Commands/FolioCellCommands.cs | 95/95 | no defect |
| Commands/FolioPageCommands.cs | 110/110 | no defect |
| Commands/FolioFieldCommands.cs | 302/302 | no defect |
| Commands/FolioContentControls.cs | 195/195 | **medium**, fixed (R5-D4); minor, fixed (R5-D5) |
| Commands/FolioContentControlCommands.cs | 48/48 | no defect |
| Commands/FolioBreakCommands.cs | 100/100 | **medium**, fixed (R5-D6) |
| Commands/FolioObjectInsertCommands.cs | 140/140 | no defect |
| Commands/FolioImageCommands.cs | 116/116 | no defect (the external-edit callback runs outside a command, but `IFolioObjectEditing.Replace` checks read-only and `ObjectLocked`) |
| Commands/FolioFindCommands.cs, FolioFindTarget.cs | 113/113, 35/35 | no defect (replace-all from the dialog checks every range) |
| Commands/FolioStyleCommands.cs | 106/106 | no defect |
| Commands/FolioParagraphCommands.cs | 105/105 | no defect |
| Commands/FolioCharacterCommands.cs | 77/77 | no defect |
| Commands/FolioClipboardCommands.cs | 64/64 | no defect |
| Commands/FolioExportCommands.cs | 67/67 | no defect |
| Commands/FolioSendCommands.cs | 71/71 | no defect |
| Commands/FolioTitlePageCommands.cs | 56/56 | no defect |
| Commands/FolioDialogCommands.cs | 68/68 | fixed with R5-D9 |
| Commands/FolioNavigationCommands.cs, FolioTableLooks.cs, FolioCommandSet.cs, FolioDrawCommands.cs, FolioZoomCommands.cs, FolioForms.cs, FolioViewCommands.cs, FolioEditCommands.cs | all | no defect |
| Commands/EnvelopePreview.cs, TableGridPreview.cs, TablePlacementPreview.cs | all | minor, left (R5-L4) |
| Indexes/FolioIndexGallery.cs | 213/213 | no defect |
| Commands/FolioIndexCommands.cs (bibliography part, reread) | 280–377 | **medium**, fixed (R5-D7, R5-D8) |
| Commands/FolioParagraphDialog.cs (reread) | 231/231 | medium, fixed (R5-D9) |
| Scripting.Editor/Facades/ScriptFacades.cs, the three script hosts | all | medium, fixed (R5-D10) |
| Editing/FolioFieldRefresh.cs (context) | 56/56 | changed for R5-D2 |

## Defects

| # | Severity | Where | Defect | Fix | Test |
|---|---|---|---|---|---|
| R5-D1 | medium | `FolioToolCommands.PageNumberAsync` (≈ line 166) | The page number wizard inserted the footer paragraph, then ran `UpdateAll` outside the transaction: a second undo step that also re-dated every date field and rebuilt every index of the document | One transaction around the insertion and a refresh limited to the inserted paragraph | `FolioPageNumberWizardTests` |
| R5-D2 | medium | `FolioMailMergeCommands.Refresh` (≈ line 144) | Moving between records in the merge preview (a viewing command) ran `UpdateFields` through the history: every "next record" was an undo step, and undo showed one record while the pointer was on another | New `IFolioFieldRefresh.ShowMergeRecord`: refreshes only database fields, outside the history | `FolioMailMergePreviewTests` |
| R5-D3 | **severe** | `FolioTextToolCommands.AutoRedactAsync` (≈ line 115) | Auto-redaction selected all of the caret's story only: e-mail addresses and the other patterns stayed in headers, footers, notes, comments and text frames, while the status bar reported the document redacted | Auto-redaction walks every story (`IStoryCatalog.Paragraphs`); `Rewrite` takes the paragraph list | `LOscuramentoAutomaticoArrivaAIntestazioniENote` |
| R5-D4 | medium | `FolioContentControls.Activate` / `ChooseAsync` (≈ lines 37, 60) | A click on a check box, drop-down or date content control (applied outside a command, so not netted by the effect guards) only checked the caret paragraph: with the caret elsewhere, a control inside a protected section could be toggled or changed | The control's own paragraphs are checked with `RefusesEdit(paragraphId)` | `IlClicNonCommutaLaCasellaDiUnaSezioneProtettaConIlCursoreAltrove` |
| R5-D5 | minor | `FolioContentControls.Activate` (≈ line 48) | `_ = ChooseAsync(...)`: a failure in the chooser started by a click was lost | `ChooseObservedAsync` awaits and logs in English | — (logging path) |
| R5-D6 | medium | `FolioBreakCommands.PageBreak` (≈ line 80) | A landscape break, or a break with a page style or numbering restart from the break dialog, in a table cell, header, footer or note: the split happened, then `SetPageStyle` threw `InvalidOperationException` | `StartsPages` (body story, outside tables): the landscape break is disabled elsewhere; the dialog falls back to a plain page break | `UnaInterruzioneConStileDiPaginaInUnaTabellaDiventaUnaInterruzioneSemplice` |
| R5-D7 | medium | `FolioIndexCommands.Save` (round-4 leftover) | Renaming a bibliography identifier left every citation on the old identifier (they pointed to nothing) | Citations are retargeted (`MapRuns` on the field argument, then a field refresh of those paragraphs) inside the same transaction: one undo step | `RinominareLaFonteSpostaLeCitazioniESiAnnullaInUnPasso` |
| R5-D8 | medium | `FolioIndexCommands.Save` | A new source, or a rename, with an identifier already used by another source silently replaced that source | Refused with "The identifier … is already used by another source" (new key `folio.bibliography.taken`, five languages); the citation is not inserted | `UnaFonteNuovaNonSovrascriveQuellaConLoStessoIdentificatore` |
| R5-D9 | medium | `FolioParagraphTabs.Read`, `FolioDialogCommands.ParagraphAsync` (round-4 leftover) | The paragraph dialog could not clear a page style or a page-number restart: "same" and 0 were ignored because `ParagraphProperties.Over` has no "clear" value | New `ParagraphPageClear` in the answer (`IFolioParagraphTabs.PageClear`): the command clears the fields on the selected paragraphs in the same transaction as the other changes | `FolioParagraphPageStartTests` (two cases) |
| R5-D10 | medium | `AppFacade.Cut/Paste/PasteValues` (round-4 leftover) | The macro's paste started asynchronously and could complete after the macro's history transaction had closed: a separate undo step | `IScriptHost` clipboard operations return their `Task`; `IScriptDispatcher.Complete` waits for it on the macro thread (the UI-thread dispatcher marshals with `InvokeAsync`; a call already on the UI thread is not blocked); the read-only guard applies to it | `UnIncollaDaMacroFinisceDentroLaMacro` |

## Left

- R5-L1: closed on 2026-11-05 (user decision of 2026-11-04): auto-redaction also clears document properties, renames
  bookmarks keeping their references and neutralises comment and change authors, in one undo step (see the work log).
- R5-L2 (minor): `FolioMasterCommands.LinksAsync` "update" on an image link updates the subdocuments instead and leaves
  the image link as it was.
- R5-L3 (minor): `AutoTextLibrary.Save` writes the file with `File.WriteAllBytes` instead of `IAtomicFileWriter`.
- R5-L4 (minor): `TableGridPreview` draws every column of the sampled lines; a line with thousands of separators draws
  thousands of cells per frame.
- Content controls stay unfillable under the "forms" document restriction (`EditGuard` refuses every paragraph and
  `Locked()` follows it); whether that restriction should allow filling content controls is a product decision.

## Tests run

- `tests/PulseOffice.Documents.UI.Tests` (whole project): 272 green.
- `tests/PulseOffice.Architecture.Tests`: 38 green.
- `tests/PulseOffice.Sheets.UI.Tests`, filter `Script|Facade|Macro`: 139 green.
- `tests/PulseOffice.Presentations.UI.Tests`, filter `Script|Facade|Macro`: 5 green.
- `tests/PulseOffice.Desktop.Tests`, filter `Composition|Macro|Script|Locali|Translation`: 25 green.
- `tests/PulseOffice.Localization.Tests`: 32 green.

## To try live

- Insert → page number wizard with "page X of Y": one ⌘Z removes it; dates elsewhere do not change.
- Mail merge preview: next/previous record several times, then ⌘Z: nothing to undo from the navigation.
- Tools → auto-redact with an e-mail in the header and in a footnote: both are masked.
- Caret in normal text, click a check-box content control inside a protected section: refused.
- Caret in a table cell: the landscape break is disabled; the break dialog with a page style inserts a plain page break.
- Edit a citation and change its identifier: the citation shows the new identifier; one ⌘Z restores both.
- New source with an identifier already in use: warning, nothing replaced.
- Format → Paragraph on a paragraph that starts a landscape page: choose "same" page style, OK: the page style goes.
- A macro running `app.Paste()` then other edits: one ⌘Z undoes the whole macro including the paste (Folio, Deck, Ledger).
