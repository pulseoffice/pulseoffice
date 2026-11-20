# Round 314 (2026-11-20): Shell multi-window leftovers

Area: the Shell (310) queue items left by the real torn-off windows of round 310. No builds or tests were run in the
round (audit cycle rule); the coordinator runs them at the join.

## Defects found

| # | Severity | Defect | Fix |
|---|---|---|---|
| 1 | Medium | `ISidePanelHost` was the primary window's `SidePanelView`: pages were registered there only, and panel commands (navigator, styles, review, panels of the three modules) opened the primary window's panel even while a torn-off window was focused; the torn-off window's panel stayed empty. | `FocusedSidePanelHost` (Shell) is now the `ISidePanelHost`: it keeps the page list, registers every page in each window's panel (the panel is part of `ShellWindowScope`, joined on `IShellWindowFocus.Entered`, left on `Left`) and sends Show, Toggle, Close and IsOpen to the focused window's panel; `Changed` comes from the focused panel and on focus change. Tests: `Round314FocusedSidePanelTests`, `Round314TornOffWindowTests`. |
| 2 | Medium | `UpdateContext` from a view always changed the focused window's context: a selection change raised by a view in an unfocused window changed the commands of the focused one. | `IDocumentWorkspace.UpdateContext(Control source, …)` (default member forwards to the old overload); `FocusedDocumentWorkspace` sends it to the window whose active document's view is the source or one of its visual ancestors (then any window holding it, then the focused one). Ledger and Folio views pass themselves. Test: `Round310FocusedWorkspaceTests.A_view_in_an_unfocused_window_updates_its_own_window_context`. |
| 3 | Medium | A closed torn-off window stayed referenced by singletons: settings, ribbon state, interface lock and language events subscribed by `ShellWindow`, the language event of its `DialogHost` and of its tab strip, and the command surfaces' `Blocked`/`Suspended` delegates when it was the last window activated (those delegates also checked the closed window's dialog host until another window was activated). | Named handlers removed in `ShellWindow.OnClosed`; `DialogHost.Release`; the tab strip listens to the language only while attached; the surfaces are rebound to the focused window on close. Weak-reference test: `Round314TornOffWindowTests`. |
| 4 | Medium | Torn-off windows opened at the default 1280 by 800 whatever the source window, and their placement was never saved. | `WindowPlacementCoordinator.AttachTornOff`: the size comes from the last torn-off placement (`window-pixels-torn-off.json`, `ITornOffWindowPlacementStore`) or from the source window, within the minimum and the source screen's working area (`ITornOffWindowSizing`); the position stays the drop point; the normal bounds are saved on close. The primary window's placement is untouched. Test: `Round314TornOffWindowSizingTests`. |
| 5 | Minor | The drop index on another window's tab strip used the strip's viewport coordinates, ignoring its scroll offset, so a drop on a scrolled strip landed on the wrong tab. | `DocumentTabsView.TabIndexAtScreen` converts through `TabStripDragGeometry.ContentX` (viewport x plus offset). Test: `Round314TabDropOffsetTests`. |
| 6 | Minor | Every window drew the one application status service, so an unfocused window showed the focused window's messages and segments. | `StatusBarView.IsFrozen`: with more than one window a deactivated window keeps its own last status (messages, format, module segments) until it is activated again. |

## Decisions

- The notification history stays application-wide: notifications (saves, updates, recoveries, agent operations) carry
  no window, and the reference applications keep one notification list per application. Toasts already show only in
  the active window. Queued as an open point in case a per-window filter is wanted.
- Segments are not split per window: module views register them once and keep them, and a view moves between windows,
  so per-window segment services would leave a moved view writing into its old window. Freezing the unfocused bar
  gives each window its own last status without that hazard.

## Still open (queued)

- A frozen status bar also freezes background activity progress drawn in it until the window is activated.
- Torn-off windows are not reopened at the next start, and a torn-off window that was maximized reopens normal.
- Deck views do not call `UpdateContext`; their context comes from other services and was not audited for windows.
- Not yet tried in the running app on the three systems, nor the native macOS menu with two windows.
