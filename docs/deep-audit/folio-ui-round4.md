# Deep audit: Folio interface, fourth round

Area: `PulseOffice.Documents.UI` (editor, canvas, keyboard, mouse, panels, commands). Starting point:
[folio.md](folio.md) and [folio-round2.md](folio-round2.md), which concentrated on the model and the formats.

Method: full reading of the central files of the interface, looking first for the paths that change the document
without going through `CommandExecutor` (keyboard, mouse, panels), because those are the paths where the command effect
guards (decision 19) do not run and protection must be checked at the source.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Editing/FolioEditor.cs | 893/893 | **severe**, fixed (D1, D2) |
| Canvas/FolioCanvas.cs | 1121/1121 | medium, fixed (D5); brush release path fixed through D2 |
| FolioView.cs | 323/323 | **severe**, fixed (D3); minor, fixed (D4) |
| Editing/FolioKeyMap.cs | 113/113 | routes ⌘B/⌘I/⌘U to `Toggle` and ⌘Z/⌘C/⌘X/⌘V to `FolioView.Perform`: covered by D2, D3, D4 |
| Editing/FolioProtectedContentGuard.cs | 121/121 | no defect; it only runs for commands, which is why D1–D3 and D7–D8 existed |
| Editing/FolioNavigatorSource.cs | 90–149/149 | no defect: chapter moves check `Guard.Check` on the heading; `Shift` touches only top-level paragraphs, so it never reaches into a protected section |
| StyleCatalog/FolioStylesSource.cs | 40–130/405 | paragraph, character and list styles go through `FolioEditor.Apply`: covered by D2 |
| Editing/FolioObjectMover.cs | 131/131 | **severe**, fixed (D7) |
| Editing/FolioReviewSource.cs | 214/214 | **severe**, fixed (D8) |
| Canvas/FolioRulers.cs | 100–154/154 | paragraph indents and tabs go through `FolioEditor.Apply`: covered by D2 |
| Rendering/PageRenderer.cs (`FolioLineNumberMarks`) | 120–139/415 | medium, fixed (D6) |
| Rendering/FolioChangeBars.cs | 34/34 | no defect |
| Documents/Protection/EditGuard.cs (model, read for context) | 98/98 | no defect |
| Documents/Controls/ContentControlCatalog.cs (model, read for context) | 1–80 | full walk of every story on each call; see D5 and "Left" |

## Defects

| # | Severity | Where | Defect | Fix | Test |
|---|---|---|---|---|---|
| D1 | **severe** | `Editing/FolioEditor.cs` `PaintBrush` | The format brush, released by the mouse (`FolioCanvas.OnPointerReleased`), checked only the view's read-only flag: in a document protected as read-only, comments-only or forms-only it painted the formatting anyway, and nothing else netted it (it is not a command) | `PaintBrush` refuses through `RefusesEdit(range)` | `IlPennelloNonDipingeNelDocumentoProtetto` |
| D2 | **severe** | `Editing/FolioEditor.cs` `Toggle`, `Apply`, `PaintBrush` | Protection was checked only on the anchor and caret paragraphs: a selection from a free paragraph to another free paragraph across a protected section applied bold (⌘B), paragraph/character/list styles from the Styles panel, ruler indents and tabs, and the brush to the protected section | New `FolioEditor.RefusesEdit(DocumentRange)` / `RefusesRange(DocumentRange)`: every paragraph of the range must be editable; used by `Toggle`, `Apply` and `PaintBrush` | `IlGrassettoNonAttraversaLaSezioneProtetta`, `LoStileDalPannelloNonAttraversaLaSezioneProtetta`, `IlPennelloNonAttraversaLaSezioneProtetta` |
| D3 | **severe** | `FolioView.cs` `Perform(Undo/Redo)` | ⌘Z and ⌘⇧Z in Folio (from the key map and, on macOS, from the native menu through `TextEditingRouter`) called `UndoStack.Undo` directly, so the protected-content guard that refuses `shell.edit.undo` from the menu never ran: undoing the creation of a protected section, or a change made before protecting it, went through from the keyboard | New service `IFolioEditingKeys` (`Editing/FolioEditingKeys.cs`) runs the history step inside `ICommandEffectChecks` with the same command identities as the shell, so the same guards (and reversal) apply | `Annullare_da_tastiera_rispetta_la_sezione_protetta_come_il_comando` (Desktop.Tests) |
| D4 | minor | `FolioView.cs` `Perform(Copy/Cut/Paste)` | `_ = CopyAsync()` and siblings: a failing clipboard read or write was dropped silently (no log, no status message) | `IFolioEditingKeys` awaits the exchange inside the effect checks, logs the error in English and reports "Paste: operation failed" (existing keys, five languages) | `UnIncollaDaTastieraCheFallisceLoDiceInveceDiTacere` |
| D5 | medium | `Canvas/FolioCanvas.cs` `Render` → `ControlRanges` | Every frame, including every caret blink (530 ms), walked every paragraph of every story twice (`ContentControlCatalog.All` and `ControlAt`) even with nothing changed: O(document) per blink on long documents | Ranges cached per (display layout, document revision, caret, shading) | `IlLampeggioDelCursoreNonRipercorreIlDocumento` |
| D6 | medium | `Rendering/PageRenderer.cs` `FolioLineNumberMarks.Apply` | With line numbering on, every frame renumbered every line of the whole layout | Table memoised per layout instance and numbering settings | `LaNumerazioneDelleRigheNonSiRicalcolaAOgniFotogramma` |
| D7 | **severe** | `Editing/FolioObjectMover.cs` `Move`, `Reanchor` | Dragging a free object (character or as-character anchor) onto a protected section re-anchored it there, adding its anchor mark or inline position to the protected paragraph; only the object's current anchor was checked | The target paragraph must be editable (`RefusesEdit(paragraphId)`); page anchors are unaffected | `TrascinareUnOggettoNonLoAncoraNellaSezioneProtetta` (two cases) |
| D8 | **severe** | `Editing/FolioReviewSource.cs` `Resolve` | Accepting or rejecting tracked changes from the Review panel checked only the document-level permission: a rejected insertion inside a protected section removed its text | Changes whose regions touch a non-editable paragraph are left out; the rest are resolved | `RifiutareUnaModificaNelPannelloNonToccaLaSezioneProtetta` |

Test infrastructure: `FolioScene` accepts an optional `configure` callback to replace services (counting decorators,
a broken clipboard).

## Left

- `ContentControlCatalog.All` walks the whole document on every call and is called several times per keystroke
  (`FolioEditor.Type` → `FillPlaceholder`, `Typable`, `Beside`). D5 removes the per-frame cost; the per-keystroke cost
  needs a revision-keyed cache in the model, which must first be sure that every mark change raises `TextDocument.Raise`.
- The macro hosts of the three modules (`FolioScriptHost`, `DeckScripting`, `LedgerScriptHost`) start copy, cut and paste
  with `_ = …Async()`: a failure is lost. To be fixed once for all three with a shared component.
- Keyboard undo in Folio does not write "Undone: …" in the status bar as the shell command does.
- `FolioEditor.Cut` with a selected object that is locked returns the copied fragment although nothing was deleted.

## Tests run

- `tests/PulseOffice.Documents.UI.Tests` (whole project): all green.
- `tests/PulseOffice.Desktop.Tests`, filter `EffectGuardChannelTests`: green.

## To try live

- Protected section between two free paragraphs: select across it and press ⌘B, apply a style from the Styles panel,
  move the ruler indent, use the format brush; nothing must change inside the section and the status bar must say why.
- Insert → Section (protected), then ⌘Z from the keyboard and from Edit → Undo: both refused in the same way.
- Drag an image anchored to a character into a protected section: refused.
- Review panel: reject a tracked insertion made inside a section that was protected afterwards: refused.
- Long document with line numbering on: the caret blink must not cost anything visible.


## Part 2

Area: the format dialogs of `PulseOffice.Documents.UI` and the `Folio*Commands` files not read in part 1, plus the two
points part 1 left open (macro clipboard, content-control catalog per keystroke).

### Coverage

| File | Lines read | Outcome |
|---|---|---|
| Commands/FolioCharacterDialog.cs | 116/116 | no defect |
| Commands/FolioParagraphDialog.cs | 222/222 | **medium**, fixed (P2-D6) |
| Commands/FolioPageDialog.cs | 265/265 | medium, fixed (P2-D3) |
| Commands/FolioTableDialogs.cs | 278/278 | minor, fixed (P2-D5) |
| Commands/FolioObjectDialogs.cs | 209/209 | minor, fixed (P2-D4) |
| Commands/FolioSectionCommands.cs | 124/124 | no defect |
| Commands/FolioListCommands.cs | 207/207 | no defect (commands: the effect guards net protection) |
| Commands/FolioIndexCommands.cs | 367/367 | medium, fixed (P2-D8) |
| Commands/FolioTableCommands.cs | 292/292 | no defect |
| Fields/FolioFieldOptions.cs | 248/248 | no defect |
| Fields/FolioFieldsModel.cs | 231/231 | medium, fixed (P2-D7) |
| Fields/FolioFieldsView.cs | 480/480 | minor, fixed through P2-D9 (status said "inserted" after a refusal) |
| Fields/FolioFieldInsertion.cs | 89/89 | **severe**, fixed (P2-D9) |
| Indexes/FolioIndexEntryPanel.cs | 176/176 | no defect |
| StyleCatalog/FolioStyleDialogs.cs | 183/183 | **medium**, fixed with P2-D6 |
| Scripting/FolioScripting.cs (host), Deck `DeckScriptHost`, Ledger `LedgerScriptHost` | hosts | minor, fixed (P2-D1) |
| Documents/Controls/ContentControlCatalog.cs, ContentControlEditing.cs (model) | 106/106, 246/246 | medium, fixed (P2-D2) |
| Documents/Editing/DocumentEdit.cs (model, read for context) | full | every `EditRecorder.Commit`, undo and redo raise the revision |

Not read in this part: `FolioToolCommands`, `FolioReviewCommands`, `FolioRibbonCommands`, `FolioMailMergeCommands`,
`FolioMasterCommands`, `FolioInsertCommands`, `FolioObjectCommands`, `FolioShapeCommands`, `FolioSpellingCommands`,
`FolioTextToolCommands`, `FolioAccessibilityCommands`, `FolioAutoTextCommands`, `FolioCellCommands`, `FolioPageCommands`
and the smaller command sets; `Indexes/FolioIndexGallery.cs`, `Shell/FolioMergeWizard.cs`. They remain for a later round.

### Defects

| # | Severity | Where | Defect | Fix | Test |
|---|---|---|---|---|---|
| P2-D1 | minor | `FolioScriptHost`, `DeckScriptHost`, `LedgerScriptHost` (`Copy`/`Cut`/`Paste`/`PasteValues`) | `_ = …Async()`: a failing clipboard operation started by a macro was lost (no log, no message) | Shared `IScriptClipboardOperations` (`Scripting.Editor/Facades/ScriptClipboardOperations.cs`) awaits the operation, logs in English and reports "Paste: operation failed" (existing keys, five languages) in the status bar; wired into the three hosts and the Desktop composition | `FolioMacroClipboardTests` |
| P2-D2 | medium | `ContentControlCatalog.All` | Walked every story several times per keystroke (`Type` → `FillPlaceholder`, `Typable`, `Beside`, guard) | Decorator `RevisionCachedContentControlCatalog` keyed by `TextDocument.Revision` (weak table per document), registered for Folio's interface. Verification: every recorder commit, undo and redo raises the revision; `Join` now raises it too. The mutator `ContentControlEditing` re-reads the catalog mid-edit (before the commit raises), so it depends on the new `ILiveContentControlCatalog` (the uncached walker) | `FolioContentControlCacheTests` (three cases) |
| P2-D3 | medium | `FolioPageDialog.Write` | Margins up to 50 cm each were accepted on any page size: left + right beyond the width left a 1 pt body (one character per line, page count explodes) | Opposite margins are scaled down to leave at least 1 cm of body, on the oriented width and height | `FolioPageDialogTests` |
| P2-D4 | minor | `FolioObjectDialogs.PropertiesAsync` | "Top and bottom" wrap is shown as "None"; confirming the dialog unchanged turned it into `None`, which the ODT writer distinguishes | The original wrap is kept when the choice did not change | `LeProprietaDellOggettoConfermateSenzaModificheNonPerdonoSopraESotto` |
| P2-D5 | minor | `FolioTableDialogs.SortAsync` / `Column` | Column letters `'A' + column % 26`: column 27 was labelled "A" again | Spreadsheet-style letters (Z, AA, AB…) | `LeColonneOltreLaZetaHannoLettereDistinte` |
| P2-D6 | **medium** | `FolioStyleDialogs.ParagraphAsync` + `FolioParagraphTabs.Read` | The style dialog dropped the whole Flow tab (to hide page break fields), and `Read` treated missing flags as false: editing any paragraph style and pressing OK cleared keep-with-next (headings), keep-together and hyphenation | `Read` only changes the flags present in the answer; the style dialog now removes only the three page fields, so keep, widows, orphans and hyphenation are editable in styles | `LoStileSenzaLaSchedaFlussoNonPerdeTieniConIlSuccessivoNeSillabazione` |
| P2-D7 | medium | `FolioFieldsModel.Apply` | Inserting a user field wrote the variable and the field as two undo steps | One transaction around the options' commit and the insertion | `UnCampoUtenteSiInserisceESiAnnullaInUnSoloPasso` |
| P2-D8 | medium | `FolioIndexCommands` (citation, bibliography) | Citing a new source was two steps (store source, insert citation); renaming a source identifier was two steps (remove, store) | `AskSourceAsync` only asks; `Save` stores in one transaction; the citation of a new source stores and inserts in one transaction | `CitareUnaFonteNuovaSiAnnullaInUnSoloPasso` |
| P2-D9 | **severe** | `FolioFieldInsertion.InsertReference` / `ReplaceReference` | A cross-reference to a heading or caption inside a protected section added a bookmark to the protected heading (or rewrote the caption's sequence field): only the caret paragraph was checked, and the fields dialog applies outside a command | New `IReferenceTargets.Touches` says which paragraph `Prepare` would change; the insertion refuses when that paragraph is not editable, and returns false so the dialog does not report "inserted" | `UnRiferimentoAUnTitoloNellaSezioneProtettaNonLoModifica` |

Every test above failed before its fix (checked by reverting the fix and running it).

### Left

- `AppFacade.Paste` runs inside `dispatcher.Change`, but the paste completes asynchronously after the macro's history
  transaction may have closed: a macro paste can become its own undo step.
- Renaming a bibliography identifier leaves existing citations pointing to the old identifier.
- The paragraph dialog cannot clear a page style or a page-number restart once set (choosing "same" or 0 is ignored):
  `ParagraphProperties.Over` has no "clear" value for those fields.
- The command files listed under "Not read" above.

### Tests run

- `tests/PulseOffice.Documents.UI.Tests` (whole project): 252 green.
- `tests/PulseOffice.Documents.Tests` (whole project): 272 green.
- `tests/PulseOffice.Architecture.Tests`: green.
- `tests/PulseOffice.Desktop.Tests`, filter `CompositionTests|Macro|Script`: green.
- `tests/PulseOffice.Formats.Odf.Tests` (whole project): 760 green.

### To try live

- A macro calling `app.Paste()` with an empty or unreadable clipboard, in Folio, Deck and Ledger: the status bar says
  "Paste: operation failed" and the log has the error.
- Format → Page: set left and right margins to 15 cm on A4: the page keeps a 1 cm body.
- Edit the "Heading 1" style and press OK: headings still keep with the next paragraph; the Flow tab is now there.
- Insert → Field → user field: one ⌘Z removes both the field and the variable.
- Insert a citation with "New source": one ⌘Z removes both.
- Cross-reference to a heading inside a protected section: refused, no "inserted" message.
- Typing in a long document full of content controls: no lag.
