# Round 81: remaining Deck dialogs and the generated agent tools

Scope: what round 80 left unread (Deck `ShapeStyleDialog`, `AltTextDialog`, `ChartEditDialog`, `DeckTextDialogCommands`,
`SlideSizeView`, `AnimationPresetThumbnail`, `TransitionThumbnail`, `TransitionDialogView`) and the MCP agent tools
generated from `ScriptApiCatalog` (`PulseOffice.Agent`, `PulseOffice.Agent.UI`).

## Coverage
| File | Lines read | Result |
|---|---|---|
| Presentations.UI/ObjectDialogs/ShapeStyleDialog.cs | 42/42 | ok |
| Presentations.UI/ObjectDialogs/AltTextDialog.cs | 27/27 | ok |
| Presentations.UI/ObjectDialogs/ChartEditDialog.cs | 38/38 | ok |
| Presentations.UI/DeckTextDialogCommands.cs | 187/187 | see defects |
| Presentations/SlideSize.cs | 25/25 | ok |
| Presentations.UI/Design/SlideSizeView.cs | 237/237 | ok |
| Presentations.UI/Motion/TransitionDialogView.cs | 79/79 | ok |
| Presentations.UI/Motion/AnimationPresetThumbnail.cs | 153/153 | ok |
| Presentations.UI/Motion/TransitionThumbnail.cs | 182/182 | ok |
| Agent/Tools/ScriptCatalogTools.cs | 179/179 | defect 2 (confirmation subject) |
| Agent/Tools/AgentTool.cs | 149/149 | ok |
| Agent/Security/AgentToolInvoker.cs | 86/86 | ok |
| Agent/Hosting/ModuleAgentScripts.cs | 35/35 | ok |
| Agent/Hosting/AgentHost.cs | 86/86 | fixed (bounded subject) |
| Agent.UI/ShellAgentWorkspace.cs | 208/208 | defect 3 |
| Agent.UI/AgentOperationGate.cs | 50/50 | fixed |
| Agent.UI/ScriptApiAgentCatalog.cs | 10/10 | ok |
| Scripting/ScriptApi.cs | 88/88 | ok |
| Scripting.Editor/ScriptApiComposition.cs | 135/135 (catalog parts) | ok |
| Sheets.UI/SheetScriptApi.cs | 196/196 | ok |
| Presentations.UI/Scripting/DeckScripting.cs | 196/196 | ok |
| Sheets.UI/Scripting/SheetFacade.cs | 724/724 | defect 1 |
| Agent/Tools/CommandTools.cs | 158/158 | ok |
| Agent.UI/DialogAgentConfirmation.cs | 31/31 | ok |
| Agent/Tools/DocumentTools.cs | 211/211 | ok |
| Agent/Tools/ViewTools.cs | 50/50 | ok |
| Agent.UI/ShellAgentCommands.cs | 64/64 | ok |
| Agent/Protocol/McpEndpoint.cs | 203/203 | ok |
| Agent/Tools/AgentDocumentChoice.cs | 18/18 | ok |
| Agent/Hosting/AgentModules.cs | 34/34 | ok |

Not read in this round: `AgentHttpServer`, `AgentSessions`, `AgentPreferences`, `AgentAccessCode`, `AgentOperationLog`,
`JsonRpc`, `AgentClientInstructions`, `AgentServerController`, the Agent.UI settings editor, status indicator, page
renderer and composition, `FolioScripting` beyond its catalog section, the other Ledger facades.

## Checks that held
- Levels: content tools take read for properties and the named readers, files for any member with a `path` parameter
  (only `page.ExportPdf`), edit otherwise; the Shell roots (`app`, `files`) are not exposed. Every reader in the list is a
  real getter in all three modules (`Style`, `Find`, `Title`, `Body`, `Notes`, `Cell`… change nothing).
- Undo: every edit-level tool runs inside one labelled step `agent.history` («Agente: …», five languages) with the
  effect guards; `SetTrackChanges` (OutsideHistory) asks for an irreversible confirmation.
- Arguments: passed as JSON literals, never as code; NaN/Infinity cannot arrive as JSON, `1e400` reaches the facades as
  Infinity and is refused by their finite checks; negative or out-of-range slide, row, column and paragraph numbers are
  refused with readable messages; bodies are capped at 4 MB by the HTTP server.
- Decision 26: edit-level script tools, commands, open/create/activate/save go through `IAgentOperationGate.EditAsync`.
- Deck dialogs: all keys of `SlideSizeView`, transition and animation views, object dialogs exist in the five languages;
  each object dialog edits through `DeckObjectDialog` in one undo step; the thumbnails stop their timers on detach.

## Defects
1. Minor, `SheetFacade.AddShape` (script and agent tool `ledger_sheet_AddShape`): the size was only checked to be
   positive, so `1e12` mm overflowed the `int` twip and gave a garbage size. Now 0 < size <= 5000 mm.
   Test `ScriptFacadeTests.AShapeTooLargeForTheSheetIsRefusedInsteadOfOverflowing` (failed before).
2. Minor, agent confirmations: the subject of the modal was the whole argument JSON (up to 4 MB), flooding the
   confirmation the user reads. `AgentConfirmationNeed` now bounds the subject to 400 characters plus «…» (never cutting
   a surrogate pair). Test `ScriptPermissionTests.A_huge_argument_does_not_flood_the_confirmation_the_user_reads`.
3. Medium, decision 26: `render_page` and `export_document` committed the pending in-place edit (a half-typed cell, a
   slide text being edited) even while a modal was open, changing the document under the dialog. The commit now goes
   through `IAgentOperationGate.CommitPendingEdits`, which commits only when no dialog is open. Test
   `AgentDialogGateTests.Drawing_or_exporting_for_the_agent_does_not_commit_an_edit_pending_under_an_open_dialog` (failed before).

## Remaining
- Undo labels of content tools show the tool name (`Agente: ledger_sheet_Write`), not a translated title: the catalog
  has no per-member localized short title for history.
- Live check (coordinator): open the function wizard while editing a Ledger cell and call `render_page` from an agent;
  the cell must stay in edit mode.
