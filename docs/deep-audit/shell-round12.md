# Deep audit, round 12: Shell (what round 11 did not reach)

Scope: `src/PulseOffice.Shell`, `src/PulseOffice.Agent*`. Partial round: the three minor points left open by round 11
that live in files of this area were closed; the larger remaining surface is listed under "Not reached".

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Shell/DocumentSharing.cs | 166/166 | **defect 1** fixed |
| Shell/TemplateLibrary.cs | 85/85 | **defect 2** fixed |
| Shell/StartView.cs | 624/624 | **defect 3** fixed; dark theme handled (`ActualThemeVariantChanged` rebuilds, tiles lightened); texts all from the catalog |
| Shell/RecentDocumentsCoordinator.cs | 68/68 | ok |
| Shell/DocumentOperations.cs | 97/97 | ok |
| Shell/WindowPlacementCoordinator.cs | 73/73 | minor, not fixed (see below) |
| Shell/WindowPlacementStore.cs | 67/67 | ok (atomic write, non-finite bounds refused) |

Not reached: AboutView/AboutDialog, ShellSettings, the navigation and style panel sources, CommandSurfaceFactory, the
rest of ShellWindow, DocumentWorkspace/OpenDocument; round-11 minors outside the files above (recovery losing the
read-only state, user keystrokes during an agent script falling into the agent's step).

## Defects

### 1. Minor: lock file read without a ceiling
`FileDocumentSharing.Holder` read the whole lock file (`File.ReadAllText`): a huge or hostile lock file on a shared
folder was loaded entirely in memory at every open. Fix: only the first `LockFileFormat.MaximumLength` (4096)
characters are read, with `FileShare.ReadWrite | Delete` so another suite holding it does not block the read.
Test: `SharedLockReadingTests` (Shell.Tests, 32 MB lock file, allocations under 1 MB), verified failing before.

### 2. Minor: template preview cache without a ceiling
`TemplateLibrary._previews` kept one preview per (path, modification time) forever: every edit of a template added an
entry. Fix: one entry per path (a newer modification time replaces it) and at most `PreviewCapacity` (64) entries, the
oldest evicted first. Tests: `TemplatePreviewCacheTests` (two cases), verified failing before.

### 3. Medium: a click on a recent entry could open a different document
`StartView` rebuilds its recent list with a posted call, while `RecentDocumentsCoordinator` re-registers the
`shell.file.recent.N` commands at once; the entry button carried its old position, so a click in between opened the
document now at that position. Fix: the button carries its document and resolves the current position at click time;
a document no longer in the list does nothing. Test: `StartRecentClickTests` (Desktop.Tests, headless), verified
failing before.

## Remaining (not fixed, minor)
- `WindowPlacementCoordinator.Restore` converts the saved position with the scaling of the screen the window is on at
  start, not of the target screen: on mixed-DPI multi-monitor setups the restored position can be shifted (the
  policy still keeps it on a working area).
- Round-11 minors outside the files read here stay open (see above).

## Tests run
Shell.Tests (195, green), Architecture.Tests (38, green), Desktop.Tests filtered on `StartRecentClickTests` (1, green).

## To try live
- Start page: open a document from outside (Finder) while the start page is visible, then immediately click a recent
  entry: the clicked document opens.
- Templates gallery: edit and save a template several times, reopen the gallery: the preview is current.
