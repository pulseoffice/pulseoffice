# Round 322 (2026-11-20): per-window module contexts

Area: the Shell (321) queue item. The Ledger, Folio and Deck module contexts (`IActiveSheetContext`,
`IActiveFolioContext`, `IActiveDeckContext`) were application singletons holding the view attached last. No builds or
tests were run in the round (audit cycle rule).

## Defects found

| # | Severity | Defect | Fix |
|---|---|---|---|
| 1 | Medium | A view in an unfocused window that attached its target (sheet change, load, Deck mode or selection rebuilds, a claim from a macro, agent or find) replaced the shared module context, so ribbon state, commands and the agent of the focused window acted on the other window's document until the focused window was activated again. | Restructured: each window owns its own context. `WindowScopedSlots<TSlot>` (Shell) keeps one slot per `ShellWindowScope`, created on demand and dropped when the window leaves; `WindowScopeLocator` finds the window that shows a view (the window's documents, then the view's top level). `FocusedSheetContext`, `FocusedFolioContext` and `FocusedDeckContext` implement the module interfaces over those slots: reads, commands and events follow the focused window's slot, focus changes raise the change event, a slot's change in another window is not forwarded. Views attach with the new owner overloads (`Attach(Control owner, target)`, Ledger `Detach(Control owner, workbook)`), which put the target in their own window's slot and release it from a slot they held in another window (tear-off). The plain `ActiveSheetContext`, `ActiveFolioContext` and `ActiveDeckContext` stay as the per-window slot and as the context of module unit tests. Tests: `Round322WindowModuleContextTests` (Desktop, Ledger, Folio and Deck). |

## Reviewed without defects

- Commands, ribbon, agent tools and find resolve the module context singleton, which is now the focused window's
  context: no caller changed.
- `ShellWindow.OnWindowActivated` still lets the active view claim its context; with per-window slots the claim only
  refreshes that window's own slot.

## Still open (queued)

- Not tried in the running app.
- Two views of the same document in one window share that window's slot (last attached wins), as before.
