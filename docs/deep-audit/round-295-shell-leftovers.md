# Round 295: Shell leftovers (2026-11-20)

Area: the four Shell items queued by round 290.

## Defects

Medium
- Closing several modified documents asked one question for all of them (save all, discard all, cancel). Word asks
  about each document. `UnsavedChangesGuard` now asks per document, with Save, Don't save, Save all (this and the rest),
  Don't save any and Cancel; saving starts only after every answer, so Cancel on a later document saves nothing.
  Unsavable documents get no Save button. Tests: `Round295PerDocumentCloseTests`, updated `FolioFixesTests`.
- A lock file written by the same user on the same machine was always taken as ours, so a document open in another
  office program, or in another live PulseOffice instance, opened editable with no warning. The lock file's fifth
  field (left empty before) now carries `pulseoffice-process:<id>:<start ms>`; `FileDocumentSharing` judges ownership
  through `IRecoveryOwnership`: ours when it names this process, or a process of ours that is no longer alive; a lock
  without our field, or naming a live other instance, is foreign. Tests: `Round295LockOwnershipTests`.

Minor
- Missing recent files were found only on click. `StartView` now checks the shown entries through
  `IRecentAvailability` (`FileRecentAvailability`, on the thread pool, cancelled when the list is rebuilt) and marks the
  missing ones (dimmed, "Not found" label, tooltip with the path, class `startRecentMissing`). Test:
  `Round295MissingRecentTests`.
- Every further Ledger window was titled "<name> : 2", and a window opened from the second became "<name> : 2 : 2"'s
  sibling with the same title. Window numbers now follow the highest open one of the same document. Test:
  `Round295LedgerWindowWalkTests`, which also walks the shared history across windows.

## Open (queued)
- Only Ledger offers New Window. (295)
- A lock of our own dead process is kept rather than rewritten by Claim. (295)
- No re-check of missing recent entries while the welcome page stays open. (295)
- The real second LedgerView is not driven headlessly. (295)
