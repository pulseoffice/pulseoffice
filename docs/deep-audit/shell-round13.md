# Deep audit, round 13: Shell (what round 12 did not reach)

Scope: `src/PulseOffice.Shell`, `src/PulseOffice.Agent*`. The three minors left open by rounds 11 and 12 were closed,
and part of the surface listed as "not reached" by round 12 was read.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Shell/DocumentRecovery.cs | 266/266 | **defect 1** fixed |
| Shell/RecoveryCoordinator.cs | 108/108 | ok |
| Shell/DocumentWorkspace.cs (OpenDocument, DocumentWorkspace) | 345/345 | ok (holds, deferred closures and activation consistent) |
| Agent.UI/AgentOperationGate.cs | 38/38 | ok |
| Agent.UI/ShellAgentWorkspace.cs | 100-190 of 190+ (WithDocumentAsync, Render, Holding, Resolve) | **defect 2** (root in UndoStack) |
| Agent.UI/ShellAgentCommands.cs | 30-60 (RunHereAsync) | same as defect 2 |
| Agent/Tools/ScriptCatalogTools.cs | 60-120 | ok |
| Agent/Hosting/ModuleAgentScripts.cs | 36/36 | ok |
| Shell/WindowPlacementCoordinator.cs | 73/73 | **defect 3** fixed (restructured) |
| Shell/WindowPlacementStore.cs | 67/67 | ok |
| Shell/About/AboutView.cs | 70/70 | ok (texts from the catalog, themed brushes, dark theme follows the palette) |
| Shell/About/AboutDialog.cs | 35/35 | ok |
| Shell/ShellSettings.cs | 196/196 | ok (integer settings bounded) |
| Shell/Navigation/ModuleNavigatorSource.cs | 86/86 | ok (read-only locks moves and drops) |
| Shell/Navigation/ModuleReviewSource.cs | 106/106 | ok (read-only locks every change) |
| Shell/StylePanels/ModuleStylesSource.cs | 51/51 | **defect 4** fixed |
| Shell/Surfaces/CommandSurfaceFactory.cs | 257/257 | **defect 5** fixed |
| Shell/ShellWindow.cs | 583/583 | ok (window clicks use the live `_context`; palette resolves the context after its await) |

Outside the area, touched to the minimum: `Document.Commands/UndoStack.cs` (root of defect 2),
`Desktop/Composition/CommandingComposition.cs` (registration of the new mapper, new placement file name).

## Defects

### 1. Minor: recovery after a crash lost the read-only state (`DocumentRecovery.cs:8`, `:72`, `:152`)
The snapshot manifest did not carry `IsReadOnly`, so a document opened read-only came back editable after a crash.
Fix: `RecoverySnapshot.ReadOnly` (defaults to false for manifests written before) is written with the snapshot and
re-applied on restore. Test: `DocumentRecoveryTests.UnDocumentoInSolaLetturaRestaInSolaLetturaDopoIlRecupero`, failing
before.

### 2. Medium: user keystrokes during an agent call fell into the «Agent: …» step (`UndoStack.cs:46`, `:103`)
The agent opens a labelled step and awaits the script, which reaches the UI thread through the dispatcher; meanwhile
the user's edits on the same document were pushed into the open transaction, so undoing the agent's step undid the
user's typing too, and the user's own step disappeared. Fix at the root: a labelled step belongs to the asynchronous
flow that opened it (an `AsyncLocal` owner token, flowed by Avalonia's dispatcher, which captures the execution
context); actions pushed from another flow go straight to the history as their own steps. Plain transactions are
unchanged. Test: `UndoStackTests.UnaModificaDiUnAltroFlussoNonFinisceNelPassoEtichettato`, failing before.
Residual: a transaction opened by the user while an agent step is open nests as before (its actions become separate
steps instead of one group).

### 3. Minor: window position restored with the wrong screen scaling (`WindowPlacementCoordinator.cs`)
Positions were saved divided by the scaling of the window's screen and restored multiplied by the scaling of the
screen the window was on at start: on mixed-DPI setups the window came back shifted, and two different physical
positions could map to the same logical one. Restructured: placement is stored in pixels (position and size), the
policy works in pixel space, and `PlacementPixelMapper` (`IPlacementPixelMapper`, injected) turns the result into a
window frame using the scaling of the screen that holds most of the window. The file is now `window-pixels.json`, so
an old logical file is not misread as pixels (the first start returns to the default placement). Tests:
`PlacementPixelMapperTests` (four cases).

### 4. Medium: the styles panel changed read-only documents (`ModuleStylesSource.cs:39-50`)
Unlike the navigator and review sources, the shell's styles source forwarded Apply/New/Edit/Delete to Folio and
Ledger sources that do not check read-only. Fix: the aggregator locks `CanApply`, `Can`, `Apply` and `RunAsync` on a
read-only active document, like the other two. Test: `ReadOnlyStylesPanelTests`, failing before.

### 5. Medium: a menu item ran with the context it was built with (`CommandSurfaceFactory.cs:211`, `:236`)
Context-menu and menu leaves executed with the context captured at build time: a document that became read-only (or
another document's state) after the menu opened still let the command through the executor's check. Fix: the click
uses the workspace's current context. Test: `StaleMenuContextTests` (Desktop.Tests, headless), failing before.

## Remaining
- Styles panel does not refresh its enabled state when the active document toggles read-only without another change
  (actions are refused anyway).
- Not read in this round: StartView-adjacent coordinators already covered by round 12; the remaining parts of
  `ShellAgentWorkspace.cs` (lines 1-100) and of `ShellAgentCommands.cs`.

## Tests run
Shell.Tests (201, green), Document.Commands.Tests (29, green), Architecture.Tests (38, green), Agent.Tests (65, green),
Desktop.Tests filtered on `StaleMenuContextTests`, `AgentDocumentHoldTests`, `StartRecentClickTests` (4, green).

## To try live
- Agent: start a long agent edit on a document and type in it meanwhile; ⌘Z first undoes the «Agent: …» step, then the
  typing separately.
- Open a document read-only, kill the app, restart and recover it: it is still read-only.
- Mixed-DPI monitors (Windows/Linux): move the window to the secondary screen, quit, restart: same place and size.
- Read-only Folio document: the styles panel cannot apply, create, edit or delete styles.
- Right-click menu open, then make the document read-only (from the agent): the editing items do nothing.
