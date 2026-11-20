# Round 290: Shell (recent documents, recovery, read-only files, settings reset, Save As)

Area: the Shell parts round 288 left open — settings dialog, open and save dialogs with format choice, unsaved-changes
prompts, read-only and locked files, live language switching, document tabs and windows, the recovery dialog — plus the
two recent-documents items queued by 288.

## Defects found and fixed

Serious
- A document held by another user (a lock file naming someone else) or not writable on disk (read-only attribute, no
  permission, locked by another program on Windows) opened as an ordinary editable document, with no warning; the user
  learned only at save time, or overwrote someone else's work with Save. `ReadOnlyOnOpen` (Shell, started at startup
  like `FormDesignModeOnOpen`) now opens such a file read-only and says why in a warning; `IFileWriteAccess` probes the
  attribute and a write open without truncating. Save As to another path makes the document editable again, as Word
  does.

Medium
- Unpinned recent entries whose file was missing were dropped silently at load (a drive not yet mounted emptied the
  list); opening a missing entry forgot it without asking. Entries now stay; opening a missing one asks, through the
  shared `ChoicePromptView` (`IMissingRecentPrompt`), whether to remove it or keep it, as Word does.
  `ModuleDocumentService.OpenAsync` reports a missing path but no longer edits the list.
- Two running instances overwrote each other's recent list (each saved its own memory copy). `RecentDocuments` rereads
  the stored list before every change.
- The recovery dialog was all-or-nothing: each document now has a checkbox; unchecked snapshots are discarded, checked
  ones restored. A snapshot that cannot be restored is named in a warning and kept for the next start (before, the
  status said only how many were restored). Snapshot times show in local time.
- Settings › Reset all reset every setting on one click with no confirmation and no undo: it now needs a second click
  (the button reads "Click again to reset all"; any rebuild disarms it), through `TwoStepConfirmation`.
- Save As with a typed extension no writer knows (for example `conti.txt` in Ledger) wrote the native package under that
  name; it now writes nothing and says the extension is unknown, as Save To already did.

Minor
- Welcome page rows had no pin glyph: pinned rows show one before the date, with a tooltip.
- File › Recent had no pin action: `shell.file.recent.pinCurrent` pins or unpins the active document (checked when
  pinned, disabled without a saved document); entries show a check when pinned.
- Document tab close buttons kept the old language after a live switch until another refresh; tabs now rebuild on
  `LanguageChanged`.

## Reviewed without defects
- Settings search (navigator query rebuilds pages, anchors scroll), per-row reset and live refresh on change; settings
  persistence across instances (`JsonSettingsStore` with a file monitor and merged writes).
- Unsaved-changes guard on close and quit (`UnsavedChangesGuard`, `QuitGate` re-entrancy): shared histories of several
  views counted once, unsavable documents explained, a failed or cancelled save stops the quit.
- Live language switching of the shell window, ribbon, menus, welcome page and settings dialog.

## Open (queued)
- The unsaved-changes prompt for several documents offers save all / discard all / cancel; Word asks document by
  document. (290)
- A lock file left by the same user on the same machine (the document open in another office program) is treated as
  ours, so no read-only warning; the reference suites warn. (290)
- Missing recent entries are not marked on the welcome page before clicking (no file probe while painting). (290)
- Separate windows of the same document (`OpenDocument.CreateWindow`) were not walked headlessly in this round. (290)
