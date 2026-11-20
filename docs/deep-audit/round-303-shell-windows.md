# Round 303: Shell windows leftovers

Area: the Shell (300) queue items: release of closed second-window views, recovery snapshots per window, placement of
a new window's tab, drop indicator and keyboard reordering, headless pointer walk of the tab strip, tear-off windows.

## Defects found

| Severity | Defect | Fix |
|---|---|---|
| Medium | A closed second window kept listening to the shared model: LedgerView to the history (full refresh and chart-table check on every step) and to the engine's recalculation through its status segments, FolioView's editor to the text document (and its position tracker), DeckView to the presentation. Each closed window kept its whole view alive and kept repainting it. | `IDocumentViewRelease` (Shell); the three views unsubscribe in `ReleaseSharedModel` (Folio disposes its editor); `DocumentWorkspace.Close` calls it when another open window shares the history. |
| Medium | Recovery snapshots were keyed per tab: a modified document with two windows wrote two snapshots, and closing one window discarded the snapshot of a document still open and modified. | `DocumentRecovery` keys identifiers and discard counts by `UndoStack`; `AutosaveCoordinator` snapshots one window per history; `RecoveryCoordinator` discards only when the last window of a history closes. |
| Minor | A new window of a document opened at the end of the tab strip. | `DocumentWindows.OpenAnother` moves it after the document's last window. |
| Minor | Tabs could not be reordered from the keyboard. | `shell.window.moveTabLeft` / `moveTabRight` (Ctrl+Shift+PageUp/PageDown, View › Windows), five languages. |
| Minor | Tab dragging showed no drop indicator. | `DocumentTabsView` shows a themed bar at the target edge while a pressed tab moves over another; hidden on drop or capture loss. |
| Minor | Tab drag and middle-click close were not driven headlessly. | `Round303TabStripPointerTests` drives press, move, release and a middle press in a headless window. |

## Tests
`Round303DocumentWindowTests` (Shell: tab placement, one snapshot per history kept until the last window closes,
release only while another window remains), `Round303TabStripPointerTests` (Desktop, headless pointer),
`Round300SecondWindowTests` (the real second views report the release), `CompositionTests` (move tab commands).

## Open
Tear-off windows (design written in docs/audit-queue.md: per-window workspace scopes under a process-wide host),
the keyboard gesture overlap with a future Ledger sheet group selection, no auto-scroll of an overflowing tab strip
during a drag. Queued under "Shell and formats" with "(303)".
