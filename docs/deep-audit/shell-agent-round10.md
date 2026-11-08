# Deep audit round 10: Shell and agent

Area: `src/PulseOffice.Shell`, `src/PulseOffice.Agent`, `src/PulseOffice.Agent.UI`. Nothing committed.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Agent/Transport/AgentHttpServer.cs | 261/261 | ok: loopback + Host + Origin checks, bearer code before body, 4 MB cap |
| Agent/Security/AgentAccessCode.cs | 81/81 | ok: 0600 file, fixed-time compare |
| Agent/Security/AgentToolInvoker.cs | 86/86 | ok |
| Agent/Security/AgentSessions.cs | 87/87 | ok (sessions only after authentication) |
| Agent/Security/AgentPreferences.cs | 92/92 | ok |
| Agent/Security/AgentOperationLog.cs | 85/85 | ok: rotation at 4 MB, detail capped |
| Agent/Protocol/McpEndpoint.cs | 191/191 | ok |
| Agent/Protocol/JsonRpc.cs | 97/97 | **defect 3** |
| Agent/Tools/AgentTool.cs | 149/149 | ok |
| Agent/Tools/DocumentTools.cs | 211/211 | ok |
| Agent/Tools/CommandTools.cs | 151/151 | ok |
| Agent/Tools/ScriptCatalogTools.cs | 173/173 | ok: arguments injected as JSON literals only |
| Agent/Tools/AgentDocumentChoice.cs | 18/18 | ok |
| Agent/Hosting/AgentHost.cs | 86/86 | ok |
| Agent/Hosting/ModuleAgentScripts.cs | 35/35 | ok (sandboxed; files only at the files level) |
| Agent.UI/ShellAgentWorkspace.cs | 212/212 | **defect 1** |
| Agent.UI/ShellAgentCommands.cs | 60/60 | **defect 1** (concurrency) |
| Agent.UI/AgentUiThread.cs | 17/17 | ok |
| Agent.UI/DialogAgentConfirmation.cs | 31/31 | ok |
| Agent.UI/AgentDocumentIds.cs | 18/18 | ok |
| Agent.UI/AgentServiceCollectionExtensions.cs | 63/63 | ok |
| Shell/DocumentWorkspace.cs | 234/234 | **defect 1** (no way to pin the target) |
| Shell/UnsavedChangesGuard.cs | 111/111 | **defect 2** |
| Shell/QuitCoordinator.cs, QuitGate.cs | 86/86 | ok |
| Shell/AutosaveCoordinator.cs | 79/79 | ok |
| Shell/CommandExecutor.cs | 91/91 | ok |
| Shell/ShellWindow.cs | 70/583 (close path) | ok |

Not reached in this round (left for the next one): the rest of `PulseOffice.Shell` (StartView, recents,
recovery, password gate, sharing, module document service, surfaces, navigation, about) and the remaining
small files of Agent/Agent.UI (ViewTools, AgentServerController, AgentModules, AgentClientInstructions,
AgentConnectionSettingEditor, AgentStatusIndicator, AgentPageRenderer, ScriptApiAgentCatalog).

## Defects

### 1. Serious: the agent's edit lands on the wrong document after an await
`ShellAgentWorkspace.WithDocumentAsync` (old lines 126-148) activated the target and then awaited the script,
which runs on a worker thread (`JavaScriptEngine.ExecuteAsync` uses `Task.Run`) and whose facades act on
`workspace.Active`. While it ran the UI thread was free: a tab click, or a second concurrent agent request,
changed the active document, so the change went to another document — outside the «Agent: …» undo step
opened on the first one and past its read-only check. The user could also close the target meanwhile.
Fix at the root: `IDocumentWorkspace.Hold(document)` pins the active document; activations and the closing
of the held document requested meanwhile are deferred and applied at release. `IAgentOperationGate`
(new, Agent.UI) serialises agent operations and holds the document for `WithDocumentAsync`, render, save,
save as and export; `ShellAgentCommands.RunAsync` goes through the same gate (without a hold, because commands
like reload and close must act on the workspace). Tests: `DocumentHoldTests` (Shell.Tests, three cases),
`AgentDocumentHoldTests` (Desktop.Tests, two cases); verified failing with the hold disabled.

### 2. Serious: `ledger.window.close` discarded a modified workbook without asking
`LedgerPartialCommands.cs:54-57` was enabled whenever two documents were open and closed the active one
directly: with two different workbooks open, the modified one was lost. Now the command is enabled and acts
only when another window shares the same history (a real second window). In the same family,
`UnsavedChangesGuard` asked to save when closing one of two windows of the same document and listed the
document twice when both were closed: windows kept by another open view are skipped and shared histories
are counted once. Tests: `LedgerWindowCloseTests` (Sheets.UI.Tests), `DocumentHoldTests.Closing_one_of_two_windows…`;
verified failing before.

### 3. Minor: a JSON-RPC message with a repeated member gave HTTP 500
`JsonRpc.cs:48` parsed with duplicates allowed and the first access threw `ArgumentException` outside the
codec. Now duplicates and depth over 64 are a parse error (-32700). Test in `ProtocolTests`; verified failing before.

## Remaining (not fixed)
- A second Ledger window has no `Path` (`LedgerDocumentFactory.CreateSecondView`): saving from it asks for a
  new name. Outside this area.
- While an agent script runs, keystrokes of the user on the same document fall into the agent's undo step.
- `OpenAsync` compares paths ordinally: on case-insensitive volumes a different case is reported as not opened.

## Tests run
Shell.Tests (187, all green), Agent.Tests (65, all green), Architecture.Tests (38, green), Sheets.UI.Tests and
Desktop.Tests filtered on the new classes and on `EffectGuardChannelTests` (one test of that class failed once
and passed on rerun: the known Dispatch issue another agent is fixing).

## To try live
- With the agent on, run a slow Ledger tool from a client and click another tab: the switch happens when the
  tool ends, and the change is in the first document. Close the document during the call: it closes at the end.
- Ledger with two workbooks, one modified: View > Windows > Close window is disabled; after New window it closes
  only the second window, without asking to save.
