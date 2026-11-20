# Round 307: shell tab tear-off

## Scope
Multiple shell windows per process (queue item Shell (303)) and drag scrolling of an overflowing tab strip.

## Findings
- Medium: no way to move a document between workspaces without closing it; `Close` releases the shared model of a
  second view and raises `DocumentClosed`, which discards recovery and recorder state. Fixed: `IDocumentWorkspace.Detach`
  removes the tab without release and raises `DocumentDetached` (tabs and the welcome page follow it); a held document
  is refused.
- Medium: no process-wide model of windows. Fixed at the model level: `IWorkspaceSet` (every window's workspace and
  documents), `IShellWindowHandle`/`IShellWindowFactory` and `ShellWindowHost` (tear-off to a new window at the
  pointer, move between windows at a tab index, hit test with the focused window first, emptied secondary windows close,
  the primary role passes on, closing the last window quits through `IQuitTarget`).
- Medium: a drag in the tab strip neither scrolled an overflowing strip nor recognised a drag out of it. Fixed:
  `TabStripDragGeometry` scrolls near the edges and detects a tear-off beyond 24 px above or below the strip;
  `DocumentTabsView.TearOffRequested` carries the document and the screen point.
- Tests: `Round307ShellWindowHostTests` (host, detach, geometry), no UI thread needed.

## Not done (queued)
- The composition still builds one `ShellWindow` singleton and binds ~65 services and every module's commands to the
  single `IDocumentWorkspace`. No `IShellWindowFactory` is registered and `TearOffRequested` has no subscriber in the
  app, so the gesture is inert. Remaining work: per-window DI scope (workspace, ribbon, menus, status bar, dialog host,
  notification center, command context), `ShellWindow` implementing `IShellWindowHandle`, command routing and shortcuts
  to the focused window, `RecoveryCoordinator`, `AutosaveCoordinator`, `QuitCoordinator`, `UnsavedChangesGuard` and the
  agent's document lookup over `IWorkspaceSet`, the native macOS menu over all windows, window placement per window.
