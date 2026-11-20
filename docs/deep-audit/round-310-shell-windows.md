# Round 310: real windows for torn-off tabs

Area: Shell tear-off (queue item 300, 303, 307). The model layer (`ShellWindowHost`, `IWorkspaceSet`,
`IDocumentWorkspace.Detach`, `DocumentTabsView.TearOffRequested`) existed; no real second window was ever opened.

## Defects
- Serious: dragging a tab off the strip did nothing (`TearOffRequested` had no subscriber, no `IShellWindowFactory`,
  `ShellWindow` was not an `IShellWindowHandle`, the host was not registered).
- Serious: every window-sensitive service was a single instance bound to the one window (workspace, dialog host),
  so a second window could not have its own documents or dialogs.
- Medium: `ShellWindowHost.DropAt` chose the source window when the pointer was still over it, so a tab dragged off
  the strip into its own window's document area was reordered instead of torn off.
- Medium: a torn-off window left empty by closing its last tab stayed open on the start page.
- Medium: closing any window would have asked about every document in the application; Quit only closed the window
  that ran it.

## Design
- Per-window scope: `ShellWindowFactory` (Desktop composition) builds each window with its own `DocumentWorkspace`,
  `DialogHost` (from `IDialogHostFactory`, shared with the primary registration), `SidePanelView`, ribbon, menus,
  title bar, status bar view, toast host and command context; `IShellWindowWiring` attaches file drops, exchange drops
  and the close gate to every window.
- Routing by focus: `ShellWindowFocus` holds the window scopes (`ShellWindowScope`: workspace, dialogs, collapse) and the
  focused one. The application-wide `IDocumentWorkspace` is now `FocusedDocumentWorkspace`: `Active`, `Context`,
  `UpdateContext` and opening go to the focused window; `Documents` spans every window (recovery, autosave, quit,
  unsaved-changes guard, agent lookup); close, activate, move, detach and hold go to the owning window; a document
  detached from one window and opened in another is relayed as `DocumentMoved`, not `DocumentOpened`, so per-open
  hooks (fonts, form design, read-only, recovery tracking) do not run twice. `FocusedDialogs` routes `IDialogService`
  and `IDialogCollapse` to the focused window's host; `IsDialogOpen` is true when any window shows a modal (agent gate).
- `ShellWindowHost` is registered with the primary window, enters and leaves scopes as windows open and close, moves
  focus on activation, tear-off and move, closes the documents of a closed secondary window, and closes a secondary
  window emptied by closing its last tab. `DropAt` ignores the source window.
- Closing: `ShellWindowCloseGate` asks only about the closing window's unsaved documents when other windows remain;
  the last window goes through `QuitCoordinator` as before. Quit from any window shuts the application down through
  `ApplicationQuitTarget` (lifetime `TryShutdown`, so the all-windows confirmation runs). Each window rebinds the
  command surfaces' blocked and suspended checks to its own dialog host when activated; `IShellPresenter` and the file
  dialog owner follow the focused window; the App refreshes every window's surfaces.

## Tests
- `Round310TwoWindowTests` (Desktop, headless): tear-off opens a visible second `ShellWindow` with its own scope and
  dialog host, commands open documents in the focused window, moving a tab back focuses the primary, closing a window
  with an unsaved document prompts and stays open on cancel, moving its last tab out closes it.
- `Round310FocusedWorkspaceTests` (Shell): focus routing, union of documents, moved-not-opened relay, leaving a scope.
- `Round307ShellWindowHostTests`: drop inside the source window tears off; an emptied torn-off window closes.

## Open
Queued in docs/audit-queue.md under Shell (310): side panel host per window, placement per window, application-wide
status and notifications, event subscriptions of closed windows, context updates from unfocused views, drop index on a
scrolled strip and checks in the running app and the macOS native menu.
