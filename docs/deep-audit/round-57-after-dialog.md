# Deep audit, round 57: commands that apply a dialog's answer to a world that changed meanwhile

## Problem

Round 56 found Ledger commands that checked protection only before awaiting a modal dialog. While a modal is open the
user cannot act, but the MCP agent can (and macros it runs), so a protection change, a sheet deletion, a read-only
switch or a document close done meanwhile was ignored when the command applied the answer. The effect guards
(`LedgerProtectionGuard`, `FolioProtectedContentGuard`) open their check when the command starts: a protection applied
during the dialog was not netted either. Patching command by command (round 56 `StillEditable`) does not scale to the
hundreds of async commands with a dialog in Folio, Ledger and Deck.

## Design (two layers, both in shared code)

1. **No external mutation while a modal is open.** `IAgentOperationGate.EditAsync` takes the agent turn and refuses with
   an `AgentHostException` when `IDialogService.IsDialogOpen`. Every mutating agent entry goes through it: document
   edits and macros (`WithDocumentAsync` with an undo label), commands (`ShellAgentCommands.RunAsync`), open, create,
   activate, save, save as. Reads (`undoLabel` null, render, export, listings) still run. The agent's own confirmation
   modal opens inside its turn, after the check, so it does not block itself.
2. **Dialog answers are admitted against the state seen when the dialog opened.** `DialogHost.ShowAsync` asks the
   injected `IDialogResultAdmission` (`WorkspaceDialogAdmission`, Shell) for a ticket when the dialog opens; when it
   closes with an answer, a ticket that no longer admits turns the answer into a cancel (`default`, the same value
   Escape gives, which every caller already handles) and a warning goes to the status bar (so `CommandExecutor` reports
   the command as refused to the agent). The ticket checks: the document is still open, it did not become read-only,
   and every module `IDocumentStateWitness` (Strategy, registered per module) still holds:
   - `LedgerStateWitness`: workbook structure protection, active sheet protection (records compared by value), the
     sheet list (removed, added or moved sheets);
   - `FolioStateWitness`: document restriction, field/bookmark protection, tracked-change lock key, protected sections
     and the count of protected table cells per table;
   - `DeckStateWitness`: the current slide is still in the deck, the selected elements (normal view) are still on it.
   Because the comparison is between dialog open and dialog close, a command that changes protection itself after its
   dialog, or a second dialog shown afterwards, is not affected.

Layer 2 covers every command with a modal at once, including those added later, and also changes made by anything other
than the agent. Round 56's per-command rechecks stay (they are harmless and cover dialogs that return no answer).

## Limits (declared)

- A dialog whose view applies changes itself before completing (live preview + Completed with a null answer) is not
  netted by layer 2; layer 1 still prevents the agent from interleaving.
- Folio tables are witnessed by the count of protected cells, so swapping protection between two cells of one table
  during a dialog is not seen.
- Deck has no protection model; only the target slide and selection are witnessed.

## Coverage

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Agent.UI/AgentOperationGate.cs | 38/38 | restructured (EditAsync) |
| src/PulseOffice.Agent.UI/ShellAgentWorkspace.cs | 204/204 | mutating entries through EditAsync |
| src/PulseOffice.Agent.UI/ShellAgentCommands.cs | 64/64 | commands through EditAsync |
| src/PulseOffice.Shell/CommandExecutor.cs | 91/91 | clean (refusal derived from the warning) |
| src/PulseOffice.UI.Controls/DialogHost.cs | ShowAsync, Close, Remove, Use | admission ticket added |
| src/PulseOffice.UI.Commands/CommandEffectGuard.cs | 25/25 | clean; guards begin at command start (cause of the bypass) |
| src/PulseOffice.Sheets.UI/LedgerProtectionGuard.cs | 80/80 | clean, see above |
| src/PulseOffice.Documents.UI/Editing/FolioProtectedContentGuard.cs | 1-80 | clean, see above |

## Defects

### Medium

1. **Answers of modal dialogs applied after the target changed** (all modules): protection of sheet, structure,
   document, sections, fields/bookmarks and tracked changes, read-only, document closed, sheet/slide/shape removed while
   the dialog was open. Fixed by layer 2.
2. **Agent mutations accepted while the user had a modal open** (`ShellAgentWorkspace`, `ShellAgentCommands`). Fixed by
   layer 1.

## Tests (headless, `UiThreadSession`)

- Shared harness `tests/Shared/DialogAdmissionHarness.cs` (real `DialogHost` in a headless window, real
  `WorkspaceDialogAdmission`), linked into the three module test projects.
- `tests/PulseOffice.Sheets.UI.Tests/LedgerRound57DialogTests.cs` (7): structure, sheet protection, sheet removed,
  document closed, read-only, untouched answer kept, end to end through `ledger.worksheet.colour`. Six fail with the
  admission check disabled (verified).
- `tests/PulseOffice.Documents.UI.Tests/FolioRound57DialogTests.cs` (6): document restriction, section, field
  protection, tracked-change lock, document closed, unrelated change keeps the answer.
- `tests/PulseOffice.Presentations.UI.Tests/DeckRound57DialogTests.cs` (4): slide deleted, selected shape deleted,
  read-only, unrelated slide removed keeps the answer.
- `tests/PulseOffice.Desktop.Tests/AgentDialogGateTests.cs` (5): edit refused, read allowed, activate, save and command
  refused while a dialog is open; edits resume once it closes.

Runs: Sheets.UI 448, Documents.UI 318, Presentations.UI 77, UI.Controls 367, Desktop 282 (+1 skipped), Architecture 40:
all green.

## Live checks for the coordinator

- Open Format Cells (or any Ledger dialog), from the agent protect the sheet: the agent call is refused with "A dialog
  is open". Close the dialog normally: the command applies.
- With a macro bound to a timer or event that protects the sheet while a dialog is open, confirming the dialog shows
  the "protection changed" warning and applies nothing.
