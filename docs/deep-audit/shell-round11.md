# Deep audit round 11: rest of the Shell and small agent files

Area: what round 10 (`shell-agent-round10.md`) did not reach, plus its two remaining minor points. Nothing committed.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Agent/Tools/ViewTools.cs | 50/50 | ok: width and page validated |
| Agent/Transport/AgentServerController.cs | 47/47 | ok: serialised; a failed listen is reported by the server (`Failure`) |
| Agent/Hosting/AgentModules.cs | 34/34 | ok |
| Agent/Hosting/AgentClientInstructions.cs | 36/36 | ok |
| Agent.UI/AgentPageRenderer.cs | 36/36 | ok (width capped by the tool) |
| Agent.UI/AgentStatusIndicator.cs | 46/46 | ok: client names already capped at 64 printable characters |
| Agent.UI/AgentConnectionSettingEditor.cs | 224/224 | ok: themed, translated, unsubscribes on detach |
| Agent.UI/ShellAgentWorkspace.cs (OpenAsync) | 39-52 | **defect 3** |
| Shell/DocumentWorkspace.cs (OpenDocument) | 1-103 | **defects 1, 2** |
| Shell/ModuleDocumentService.cs | 370/370 | **defect 2** |
| Shell/DocumentOpenRouter.cs | 108/108 | ok (now also `FindOpen`) |
| Shell/DocumentPasswordGate.cs | 220/220 | ok: key files capped at 16 MB and zeroed |
| Shell/DocumentRecovery.cs | 266/266 | ok: 0600 files, encrypted snapshots for protected documents |
| Shell/RecoveryCoordinator.cs | 108/108 | ok |
| Shell/RecentDocuments.cs | 169/169 | ok |
| Shell/RecentDocumentsCoordinator.cs | 68/68 | ok (a vanished file is forgotten silently, by design and test) |
| Shell/DocumentSharing.cs | 166/166 | minor, not fixed (see below) |
| Shell/CommandPaletteSource.cs | 56/56 | ok: the executor rechecks applicability |
| Shell/CommandExecutor.cs | 91/91 | ok |
| Shell/InterfaceFaultGuard.cs | 75/75 | ok |
| Shell/DocumentArrivals.cs | 109/109 | ok |
| Shell/UnattendedOpening.cs | 32/32 | ok |
| Shell/TemplateLibrary.cs | 85/85 | minor, not fixed (see below) |
| Shell/ShellWindow.cs | 120-459 | ok |
| Shell/StartView.cs | 440-479 plus event wiring | minor, not fixed (see below) |
| Sheets.UI/LedgerDocumentFactory.cs | 131/131 | **defect 1** |

Not reached: the remaining bodies of StartView (layout), AboutView/AboutDialog, ShellSettings, the navigation and style
panel sources, CommandSurfaceFactory beyond `Execute`, WindowPlacement*.

## Defects

### 1. Serious: a second Ledger window lost the document's file state (data loss, read-only bypass)
`LedgerDocumentFactory.CreateSecondView` built a fresh `OpenDocument` on the shared history: no `Path` (saving from it
asked for a new name), not read-only even when the workbook was opened read-only (edits allowed through the second
window), no password protection (saving from it wrote the file unencrypted), and a saved point equal to the current
state, so a modified workbook looked unmodified in the second window. Closing the first window (skipped by the guard
because another view keeps the history) and then the second lost the changes without a question.
Fix at the root: the file state (path, saved point, forced modification, read-only, protection) is a shared object
inside `OpenDocument`; `OpenDocument.CreateWindow(view, title)` makes another window over the same state, and saving
from one window marks every window saved. Tests: `DocumentWindowTests` (Shell.Tests, four cases), verified failing
with the old construction.

### 2. Serious: an edit made while the file was being written was marked saved
`ModuleDocumentService.WriteAndReportAsync` called `MarkSaved` after awaiting the commit to disk, which runs with the
interface unlocked; `MarkSaved` took the history's state at that moment, so an edit typed during the write was not in
the file but the document said "saved", and closing it lost the edit without asking.
Fix: `OpenDocument.CapturePoint()` before writing, `MarkSaved(path, name, point)` after; forced modifications count
marks so one raised meanwhile survives. Test: `ModuleDocumentServiceTests.An_edit_made_while_the_file_is_being_written_stays_unsaved`,
verified failing before.

### 3. Medium: the agent's open_document failed on case-insensitive volumes
`ShellAgentWorkspace.OpenAsync` looked the opened document up with an ordinal path comparison, while the router opens
with a case-insensitive one on macOS and Windows: asking for `REPORT.ODS` opened `Report.ods` and then answered that it
could not be opened. Fix: `IDocumentOpenRouter.FindOpen(path)`, the router's own lookup, used by both. Test:
`AgentOpenPathCaseTests` (Desktop.Tests), verified failing before.

## Remaining (not fixed, minor)
- `FileDocumentSharing.Holder` reads the whole lock file: a huge lock file on a shared folder is read in memory.
- `TemplateLibrary._previews` keeps one preview per (path, modification time) forever.
- `StartView` refreshes the recent list with a posted call while the recent commands are rebuilt at once: a click in
  that instant opens the entry now at that position.
- A restored recovery snapshot does not keep the read-only state of the original.
- While an agent script runs, keystrokes of the user on the same document fall into the agent's undo step (round 10).

## Tests run
Shell.Tests (192, green), Agent.Tests (65, green), Architecture.Tests (38, green), Sheets.UI.Tests filtered on window
tests (2, green), Desktop.Tests filtered on `AgentOpenPathCaseTests` and `AgentDocumentHoldTests` (3, green).

## To try live
- Ledger: open a saved workbook read-only, View > Windows > New window: the second window is read-only too. Open it
  editable, edit, New window, close the first window, close the second: the save question appears. Save from the
  second window: both tabs lose the modified dot and the file is the original one.
- Save a large document and type during the save: the tab keeps the modified dot.
- With the agent, `open_document` with a path in a different letter case opens and answers with the document.
