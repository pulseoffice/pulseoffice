# Deep audit round 25: scripting (CPython, facades, IDE, Deck and Folio macro events)

Area: what round 24 did not reach: `PulseOffice.Scripting.CPython`, `ScriptApiCatalog` and the module facades,
the macro IDE, the Deck and Folio macro events, and the open minor (hard links in `MacroFiles.AppendText`).
Nothing committed.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Scripting.CPython/CPythonScriptEngine.cs | 233/233 | ok: refused unless `AllowsUnsandboxedEngines`; the runtime is located and started only after that check; `Analyze` never starts it |
| Scripting.CPython/CPythonHost.cs | 44/44 | ok |
| Scripting/ScriptEngine.cs | 103/103 | ok: `Sandboxed` and `Trusted` both leave `AllowsUnsandboxedEngines` off |
| Scripting/MacroFiles.cs | 123/123 | **defect 1** (hard links, the open minor of round 24) |
| Scripting.Editor/MacroEditorLauncher.cs | 177/177 | ok: only the editor's Run sets `AllowsUnsandboxedEngines`, an explicit user action |
| Documents.UI/Scripting/FolioMacros.cs | 164/164 | ok: control events pass the gate and run with `Sandboxed`, so CPython and IronPython refuse document macros |
| Agent/Hosting/ModuleAgentScripts.cs | 35/35 | ok: JavaScript only, sandboxed |
| Presentations.UI/DeckInteractionForm.cs | 68/68 | ok: `RunMacro` is stored and edited, never executed, so Deck runs no document macro on events |
| Presentations.UI/DeckMacroEventsSource.cs | 31/31 | ok |
| Presentations.UI/Scripting/DeckScripting.cs | 196/196 | ok: every change through `DeckActions` (undoable), read-only refused, slide numbers checked for NaN, infinity and range |
| Documents.UI/Scripting/FolioScripting.cs | 498/498 | **defect 2** (`SetTrackChanges`) |
| Scripting.Editor/Facades/ScriptFacades.cs | 155/155 | ok |
| Document.Commands/UndoStack.cs | 70/~200 (outside-history part) | ok |
| Scripting/ScriptApi.cs | 86/86 | metadata extended (defect 3) |
| Scripting.Editor/ScriptApiComposition.cs | 121/121 | metadata extended (defect 3) |
| Agent.UI/ScriptApiAgentCatalog.cs | 10/10 | ok |
| Agent/Tools/ScriptCatalogTools.cs | 173/173 | **defect 3** |
| Desktop/Composition/ShellMacroRecorderCommands.cs | 110/110 | **defect 4** |
| Shell/CommandRecorder.cs | 85/85 | **defect 4** |
| Scripting/ScriptExecutionPermissions.cs | 69/69 | ok |
| Scripting/ScriptOutput.cs | 47/47 | ok |
| Scripting.Editor/MacroEditorView.cs | 713/713 | **defect 5** (output without a cap) |
| Sheets.UI/Scripting/SheetFacade.cs | 717/717 | **defect 6**; otherwise ok: ranges capped at 1,000,000 cells, protection checked before each change, every change pushed to the history |
| Foundation/Units/Measurements.cs | 30/30 (Twip) | ok |

Not reached in this round: the other Ledger facades (`DocumentFacade`, `FilterFacade`, `FormatRuleFacade`, `PivotFacade`,
`PrintFacade`, `ScenarioFacade`, `ValidationFacade`), `PythonDiscovery`, `StandalonePython`, `DocumentationPanel`,
`MacroWriteAccess*`, `PythonDownloadConsent`.

## Defects

### 1. Minor: appending through a hard link changed a file outside the macro folder
`ScriptFilesFacade.AppendText` (MacroFiles.cs:94) opened the path in append mode. A hard link planted in the macro
folder shares its inode with a file elsewhere, `LinkTarget` does not see it, and the append wrote to that file.
.NET has no portable link-count API, so instead of counting links the fix removes the write through the inode:
`AppendText` now builds a temporary file (existing content plus the new text) and moves it over the name, like
`WriteText`. The other name of a hard link keeps its old content. The size cap is unchanged.
Tests: `MacroWritePermissionTests.Appending_to_a_file_that_is_a_hard_link_never_changes_the_other_name` (verified
failing before), `Appending_keeps_what_the_file_already_held`; `HardLinks` test helper (libc `link`, `CreateHardLinkW`).

### 2. Medium: `doc.SetTrackChanges` did not mark the document modified
`FolioFacade.SetTrackChanges` (FolioScripting.cs:303) set `Changes.IsRecording` directly. Decision 21 puts change
recording outside the history through `UndoStack.ApplyOutsideHistory`, which is also what marks the document modified:
a macro or the agent turning tracking on left the document "unmodified", so closing it dropped the setting silently.
Fix: through `ApplyOutsideHistory`, and no change when the state is already the wanted one.
Test: `FolioFacadeOutsideHistoryTests` (verified failing before).

### 3. Medium: the agent changed state outside the history without confirmation
Decision 21 says the agent treats what ⌘Z cannot undo as irreversible. Command tools did (`OutsideHistory`), script
tools did not: `writer_doc_SetTrackChanges` ran without asking. Fix: the catalog carries the fact as metadata
(`ApiMemberSpec.OutsideHistory` → `ApiSymbol.IsOutsideHistory`), `ScriptMethodTool` asks an `Irreversible`
confirmation for such members and its description says so. Test:
`ScriptPermissionTests.A_script_member_outside_the_undo_history_is_confirmed_as_irreversible` (verified failing before).

### 4. Medium: `app.Run` ran commands outside the history, bypassing the agent's confirmation
`ScriptCommands.Run` (ShellMacroRecorderCommands.cs:26) checked only the command id. A macro or the agent (`*_app_Run`,
an edit-level tool without confirmation) could run protection or change-recording commands, which are not in the
macro's undo step and which the agent's command tools confirm. The recorder recorded them too. Fix:
`IScriptableCommandPolicy.Allows(CommandDefinition)` refuses `OutsideHistory` commands; `ScriptCommands.Run` and
`CommandRecorder.Note` use it. Test: `CommandRecorderTests.Commands_outside_the_undo_history_are_neither_recorded_nor_run_by_a_macro`
(verified failing before).

### 5. Medium: the macro editor's output had no cap
`MacroEditorView.RunOnceAsync` (MacroEditorView.cs:668) collected every `app.Log` line in a list. The JavaScript memory
cap does not count .NET strings, so `for(;;) app.Log("x".repeat(1e6))` for 30 seconds could exhaust memory and freeze
the text box. Fix: `ScriptTranscript` (Scripting/ScriptOutput.cs) keeps at most 1,000,000 characters and flags the
truncation; the editor shows `macro.output.truncated` (five languages). Tests: `ScriptTranscriptTests` (the type did not
exist before, so they failed to compile).

### 6. Minor: row heights and column widths without a cap from macros
`SheetFacade.RowHeight`/`ColumnWidth` (SheetFacade.cs:694) accepted any finite millimetres: `1e9` saturated the twip
value at `int.MaxValue`, whose sums overflow in the grid geometry. Fix: 0–500 mm, the range of the dialog (0.1–50 cm).
Test: `ScriptFacadeTests.Row_heights_and_column_widths_beyond_what_the_dialog_allows_are_refused` (verified failing before).

## Checked and sound
- CPython never starts without an explicit Run in the macro editor: document event macros (Folio controls, Ledger
  forms) and the agent run with `Sandboxed`, which refuses both Python engines before the runtime is located; the
  standalone download asks `IPythonDownloadConsent`.
- Deck stores `RunMacro` interactions but never executes them.

## Remaining
- The agent sees the `files` tools, which always fail for it (sandboxed): harmless, could be hidden.
- `SheetFacade.Replace` and text setters have no cap on the resulting text length (bounded by the macro memory cap
  only for JavaScript strings, not for the cells).
- One intermittent failure in `PulseOffice.Sheets.UI.Tests` on the first run, not reproduced in three reruns.

## Tests run
`PulseOffice.Scripting.Tests` 146/146, `PulseOffice.Documents.UI.Tests` 301/301, `PulseOffice.Agent.Tests` 66/66,
`PulseOffice.Shell.Tests` 212/212, `PulseOffice.Sheets.UI.Tests` 420/420, `PulseOffice.Localization.Tests` 32/32,
`PulseOffice.Architecture.Tests` 38/38. `PulseOffice.Desktop` builds with no warnings.

## To try live
- Folio: a macro with `doc.SetTrackChanges(true)` on a saved document: the title shows it modified, closing asks to save.
- Agent: call the Folio `SetTrackChanges` tool: a confirmation modal appears. `app_Run` with a protection command is refused.
- Macro editor: `for (;;) app.Log("x".repeat(100000))`: the run stops at the time cap, the output ends with the truncation line.
- `files.AppendText` twice on the same file: both texts are there.
