# Deep audit, round 2: shell, commands and shared interface

Area: `src/PulseOffice.Shell`, `src/PulseOffice.Desktop`, `src/PulseOffice.UI.Commands`, `src/PulseOffice.UI.Controls`,
`src/PulseOffice.Scripting.Editor`, `LedgerProtectionGuard`, `FolioProtectedContentGuard`.

## Group 1: protection guards on every editing channel

Read in full: `CommandExecutor.cs` (91), `CommandEffectGuard.cs` (11), `HistoryReversal.cs` (34),
`LedgerProtectionGuard.cs` (75), `FolioProtectedContentGuard.cs` (119), `ShellCommands.cs` (repeat command),
`ShellAgentWorkspace.cs`, `MacroEditorLauncher.cs`, `FolioMacros.cs`, `LedgerChartCommands.cs` (async edit path).

Findings:
- **Serious** `src/PulseOffice.Agent.UI/ShellAgentWorkspace.cs:122` — the agent's editing transactions did not go through the
  command effect guards: a content tool that wrote into a protected Folio section (or a locked Ledger cell, through a path
  not checked at the source) kept the change. Fixed: the transaction opens a `CommandEffectScope` (`agent.edit`), closes
  it after the labelled step and, when a guard reverted the change, the tool fails with an explicit error.
- **Serious** `src/PulseOffice.Scripting.Editor/MacroEditorLauncher.cs:58` and
  `src/PulseOffice.Documents.UI/Scripting/FolioMacros.cs:142` — macro runs (editor "Run" button and form-control macros)
  only opened an undo transaction; the guard saw the run only when the whole editor dialog closed, and form-control macros
  never. Fixed with the shared `IGuardedMacroRuns` service: every run is its own guarded scope.
- Structure: the guard loop moved from `CommandExecutor` into `ICommandEffectChecks` / `CommandEffectScope`
  (`PulseOffice.UI.Commands`), shared by executor, agent and macros; `ICommandEffectCheck.Finish` now says whether it
  reverted.
- Verified, no defect: "repeat last command" (`shell.edit.repeat`) runs the repeated command through the executor, so the
  guards wrap it twice; asynchronous commands are awaited by the executor, so changes after the first `await` (dialogs,
  imports) are seen; the fire-and-forget paths found (`EditSelectedChart` from double click, Deck `TryEditSelection`)
  check protection at the source or belong to a module without protection.

Tests: `tests/PulseOffice.Desktop.Tests/EffectGuardChannelTests.cs` (agent and macro tests fail before the fix; repeat
test documents the verified behaviour).

## Group 2: headless tests whose assertions never ran

`Session.Dispatch(async () => …)` returns `Task<Task>`; without `.Unwrap()` the test awaits only the start of the lambda,
so every exception thrown by an assertion is lost in the inner task and the test always passes. A scanner over `tests/`
found 31 such tests: `FontworkGalleryTests`, `TextReviewWatchTests`, `LedgerDataDialogsTests` (8),
`TableInsertTests`, `LedgerAnalysisDialogsTests` (10), `LedgerFormatSurfacesTests`, `GalleryPixelTests` (3) in
Desktop.Tests, and outside the Desktop project `DictionaryManagerViewTests`, `DeepAuditAssetPickerTests`
(UI.Controls.Tests), `FolioStylesPanelTests` (Documents.UI.Tests): all now end with `.Unwrap()`. No `async void` test was
found; Desktop.Sweep.Tests has no such call.

Unwrapped, eight tests failed. Outcome:
- **S2-D3, medium, product defect** `src/PulseOffice.UI.Controls/Galleries/ThumbnailGallery.cs:140` — arrow keys in the
  shared thumbnail gallery (table styles, Fontwork, Deck themes) moved from the *selected* tile, not from the tile that has
  keyboard focus: after Tab or a focus change the first arrow jumped back near the selection. Fixed: navigation starts
  from the focused tile. Test `TableInsertTests.Il_dialogo_segue_tastiera_e_mouse_e_conferma_con_invio` (failed before).
- Test defects (the product behaves correctly): `TextBox.TextChanged` is raised through the dispatcher in Avalonia 12, so
  the tests now run the dispatcher jobs after setting `Text` (sheet move/copy name check, series, name manager,
  validation, data form, inline tab rename); the series test looked for footer buttons in the visual tree, but the footer
  is hosted by the dialog host, so it now walks the logical tree.
- **S2-D5, open** — `GalleryPixelTests.La_galleria_dei_temi_resta_identica_al_pixel`: 4608 pixels differ; the stroked
  decorations of the Deck theme thumbnails (Atelier frame, Brace and Orchid arcs, Midnight frame) are drawn much thinner
  and lighter than in the golden image of 2026-10-22. The regression happened while the test could not fail; the commit
  was not identified in this round. The test is skipped with this reference: compare the theme dialog live, then either
  fix the stroke width or regenerate the golden image (`PULSE_GOLDEN_WRITE`).
- The Documents.UI test scene registers the new `ICommandEffectChecks` and `IGuardedMacroRuns` (minimal change outside
  the area).

## Group 3: Folio character dialog, "Default" language

Read: `src/PulseOffice.UI.Controls/Text/LanguageChoices.cs` (44), `src/PulseOffice.UI.Controls/TextFormat/CharacterFormatTabs.cs`
(language field), `src/PulseOffice.Documents.UI/Commands/FolioCharacterDialog.cs` (130).

- **S2-D6, medium** `src/PulseOffice.UI.Controls/TextFormat/CharacterFormatTabs.cs:117` and
  `src/PulseOffice.Documents.UI/Commands/FolioCharacterDialog.cs:55` — choosing "Default" did nothing: the shared tabs
  replaced the null tag with the current language, and the Folio reader also ignored a null language, so text marked
  German could never go back to the document's language from the dialog. The title itself is translated in the five
  languages (`language.default`) and the initial value is read correctly (no language selects "Default"). Fixed: the
  shared tabs return the chosen tag as is, and `CharacterChange` carries `DefaultLanguage`, which removes the direct
  language from runs and from character styles. Test `FolioFormattingDialogTests.LaLinguaPredefinitaToglieLaLinguaDelTesto`
  (failed before).

## Group 4: Ledger "Select row" shortcut

- **S2-D7, minor** `src/PulseOffice.Sheets.UI/LedgerSelectionCommands.cs:19` — the factory shortcut of "Select row" was
  Ctrl+Space, that is ⌘Space on macOS, taken by Spotlight (reserved in `MacReservedShortcuts`, so it could not even be
  reassigned to itself). It is now **Shift+Space**, as in Excel and the reference suite, on all three systems; "Select
  column" stays Ctrl+Shift+Space (⌘⇧Space, free on macOS). Folio and Deck use Ctrl+Shift+Space for the non-breaking space
  while writing text, which does not clash: the Ledger selection commands act on the grid, not inside the cell editor.
  The shortcut help and the shortcut editor read the registry, so they follow; no static table names the old key.
  Closes B32 of `docs/deep-audit/platform.md`.
- **S2-D8, medium** `src/PulseOffice.UI.Controls/Shortcuts/TypingChordGuard.cs:13` — a key consumed by a window shortcut
  without Ctrl/⌘ (Shift+Space, but also any Shift+letter or F-key shortcut that produces text) still reached the grid as
  typed text and started editing the cell. The guard shared by the three modules now also rejects the text input of a
  key whose KeyDown was already handled.
- Test `LedgerSelectionShortcutTests`: no factory shortcut of any registered command is reserved by macOS or by Windows
  and Linux (failed before on `ledger.select.row`); Shift+Space selects the whole row without writing a space in the cell
  (failed before for both reasons, checked separately).

## Group 5: the macro editor's "File writing" button

Read in full: `src/PulseOffice.Scripting.Editor/MacroWriteAccessButton.cs` (69), the bar construction in
`MacroEditorView.cs` (`Build`, lines 105–240).

- **S2-D9, minor (UI)** `src/PulseOffice.Scripting.Editor/MacroEditorView.cs:150` — the button was the last item of the
  tool `WrapPanel`: with the macro name, Save, language, Run and Check before it, it always wrapped alone onto a second
  line, plain text with no icon. It now sits in its own column at the right end of the first row of the bar.
- Redesigned `MacroWriteAccessButton`: padlock icon (closed when off, open when granted), the state as text
  ("File writing: off" / "File writing: allowed" in the five languages), border and text in secondary tones when off, in
  the warning colour with a light warning tint when granted (`granted` class for styles); explicit transparent background
  and minimum height, so the whole rectangle takes the click; accessible name equal to the state, help text equal to the
  tooltip, `AutomationId` `macro.fileWrite` for automation and for synthetic clicks.
- The synthetic macOS click (System Events "click at") cannot be reproduced headless: to be verified live. While writing
  the test, the headless hit test right after the dialog opens fell through to the ribbon because the frame was not yet
  rendered; this is a property of the headless renderer, not of the dialog.
- Test `LedgerEditorTests.Il_pulsante_Scrittura_file_sta_nella_barra_con_icona_nome_accessibile_e_area_di_clic` (failed
  before: the button was 32 px below the first row of the bar); the existing grant/revoke test still passes.

## Group 6: CommandExecutor, DialogHost and the modal services re-read

Read in full: `src/PulseOffice.Shell/CommandExecutor.cs` (91), `src/PulseOffice.UI.Controls/DialogHost.cs` (628),
`src/PulseOffice.UI.Controls/Services/DialogService.cs` (43).

- `CommandExecutor`: a command whose effects a guard reverted is now reported as `Refused` from the scope itself, not
  only when the guard's status message reaches the notification history (the outcome no longer depends on how the guard
  reports).
- Remaining, minor: `CommandExecutor.ExecuteAsync` collects every notification raised while a command runs; two
  commands that overlap in time (a long asynchronous one and a quick one) can see each other's refusals in their
  outcome. `DialogHost.Use` subscribes `LanguageChanged` and never unsubscribes (one host per window, so no growth).
- Verified, no defect: Escape and Tab outside the dialog are kept in it; Escape with an open drop-down is left to the
  drop-down; a cancelled token removes even a dialog that refuses to close; stacked dialogs present the previous one
  again on close; focus returns to the element focused before the dialog or to the document.

## Final summary

Defects by severity:
- Serious: agent transactions outside the protection guards (`ShellAgentWorkspace.cs:122`); macro runs outside the
  guards (`MacroEditorLauncher.cs:58`, `FolioMacros.cs:142`). Fixed.
- Medium: 31 headless tests never failing (missing `Unwrap`); gallery arrow keys from the selection instead of the
  focus (`ThumbnailGallery.cs:140`); "Default" language ignored (`CharacterFormatTabs.cs:117`,
  `FolioCharacterDialog.cs:55`); keys consumed by a shortcut still typed into the grid (`TypingChordGuard.cs:13`). Fixed.
- Minor: ⌘Space for "Select row" (`LedgerSelectionCommands.cs:19`); the file-writing button wrapped alone on a second
  row without icon (`MacroEditorView.cs:150`). Fixed.
- Open: S2-D5, theme gallery strokes against the golden image (test skipped with reference); the two minor notes of
  group 6.

Tests run, one project at a time: Desktop.Tests (257 passed, 2 skipped), Documents.UI.Tests, UI.Controls.Tests,
Sheets.UI.Tests, Shell.Tests, UI.Commands.Tests, Agent.Tests, Architecture.Tests.

## Live checks

- Folio: protect a section, ask the MCP agent to replace its text: the tool answers with an error and the text stays;
  a form-control macro or a macro run from the editor that writes into the section is undone at the end of the run.
- Repeat the last command (Ctrl+Shift+Y) with the cursor in a protected section: refused with the status message.
- Folio, Format › Character on text marked German: choose "Default", OK: the language goes back to the document's one
  (status bar, spelling).
- Ledger: Shift+Space selects the row and types nothing; ⌘⇧Space selects the column; with a cell being edited
  Shift+Space types a space.
- Thumbnail galleries (table styles, Fontwork, themes): Tab onto a tile, then the arrows move from that tile.
- Macro editor: the "File writing: off" button at the right of the first row with a closed padlock; after granting it
  turns amber with an open padlock; VoiceOver reads the state; System Events "click at" on it opens the confirmation.
- Deck › Themes: compare the thumbnails with `tests/PulseOffice.Desktop.Tests/Golden/theme-gallery.png` (frames and arcs
  of Atelier, Brace, Midnight, Orchid) and decide between a fix and a new golden image.
