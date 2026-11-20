# Round 288: shell audit (recent documents)

## Covered
- Recent documents model (`RecentDocuments`), its menu coordinator and the welcome page list.
- Autosave timer and the recovery coordinator, read through (no defect found in this pass).

## Defects
- Medium, fixed: recent documents could not be pinned, a basic feature of the reference word processors and Office.
  `RecentDocument.Pinned` is persisted in the recent list file; pinned entries come first, are always shown beyond
  the configured count, survive Clear (which now clears unpinned entries, as Office's "clear unpinned") and are kept
  at load even when their file is momentarily missing (removable or network drive). The welcome page entry has a
  context menu: pin or unpin, remove from list. Texts in the five languages. Test: `Round288RecentPinningTests`.
- Minor, open: unpinned entries whose file is missing are dropped silently at startup, and opening a missing recent
  entry forgets it without telling the user.
- Minor, open: the pinned state shows only in the context menu and the accessible name; no pin glyph on the row, and
  the File › Recent menu has no pin action.

## Not covered in this round
Settings dialog (search, reset, application of every setting, persistence across instances), open/save dialogs and
format choice, unsaved-changes prompts on close and quit, read-only and locked files, live language switching,
document tabs and windows, recovery dialog behaviour in depth. Queued.
