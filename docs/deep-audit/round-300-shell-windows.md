# Round 300: Shell windows and document tabs

Area: the Shell (295) leftovers, then document tabs and window management (drag, close order, focus after close).

## Defects found

| Severity | Defect | Fix |
|---|---|---|
| Medium | Only Ledger offered New Window and Close Window; Folio and Deck had no second window of a document (rule 10). | `DocumentWindows` (Shell, behind `IDocumentWindows`) numbers and opens second views from the module's `IDocumentViewSource` (Ledger, Folio and Deck document factories); `ShellWindowCommands` registers `shell.window.new` and `shell.window.close`, in the View menu and on each module's View tab. The Ledger-only `ledger.window.*` commands are gone, their texts and icons renamed. |
| Medium | A lock left by our own dead process was reported as ours and `Claim` kept it with the dead process id. | `ShareClaim.IsStale` marks a lock of our account whose process is dead; `Claim` rewrites it with the current process; the share command claims a stale lock instead of offering to release it. |
| Medium | Closing the active tab activated the last tab, not its neighbour. | `DocumentWorkspace.Close` activates the tab that took its place, or the one before it at the end. |
| Minor | The welcome page never re-checked missing recent entries. | `StartView` re-checks on window activation and every 30 seconds while shown; an entry that came back is unmarked. |
| Minor | Tabs could not be reordered and had no middle-click close. | `IDocumentWorkspace.Move` and `DocumentMoved`; `DocumentTabsView` moves a tab released over another and closes on middle press. A non-active tab now activates on release, so the strip is not rebuilt under the pointer during a drag. |
| Minor | The second window walk used a stand-in view. | `Round300SecondWindowTests` composes the app headlessly and opens and closes real second LedgerView, FolioView and DeckView windows. |

## Checked without defects
- Closing a window of a document that has another window does not prompt to save (`UnsavedChangesGuard.KeptByAnotherView`).
- Focus after close: `ShellWindow.ShowDocument` focuses the newly active view through `IDocumentFocusTarget`.

## Open
Queued in docs/audit-queue.md under "Shell and formats" with "(300)": release of closed second-window views, duplicate
recovery snapshots per window, tear-off windows, drop indicator and keyboard reordering, placement of a new window's
tab, headless pointer walk of the tab strip.
