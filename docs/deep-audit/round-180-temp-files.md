# Round 180: document content in temporary files

Scope: the open items of round 176 about content of (possibly protected) documents reaching disk in clear, plus a
search of `src` for every use of the shared temporary folder.

## Defects

1. **Medium: `OdsWriter.WriteBody` spooled the spreadsheet body to the shared temporary folder.** The body of
   `content.xml` went to a `DeleteOnClose` file in the system temporary folder, in clear and with default permissions,
   also while saving a protected spreadsheet. The writer now takes an `IContentSpool` (Formats.Odf); every existing
   constructor chains to `MemoryContentSpool`, so the ~160 call sites are unchanged and nothing reaches disk. A
   platform-backed spool (owner-only file) can be injected through the four-argument constructor if very large
   workbooks ever need it. Test: `Round180ContentSpoolTests` (Formats.Odf.Tests).
2. **Medium: `AtomicFileWriter.WriteThroughPathAsync` used a scratch file with default permissions in the shared
   temporary folder** for non-local destinations. The writer now takes `IPrivateFiles` (default: `PrivateFilesFactory`)
   and produces into a fresh owner-only folder, deleted with its content afterwards. Test:
   `Round180ScratchFolderTests` (Robustness.Tests).
3. **Minor: opening or comparing a stored version wrote its package in clear under the shared temporary folder.** The
   folder was already owner-only on Unix and deleted after use; it now comes from `ITemporaryFiles` (private session
   folder, swept after a crash), so a crash no longer leaves the version behind in the shared folder.
4. **Medium: opening a remote document stored the download under `<temp>/pulseoffice-remote` with default
   permissions**, readable by other users for the whole session and never removed. It now lives in a folder from
   `ITemporaryFiles`, owner-only and removed when the session ends.

Guard: `Round180TemporaryFolderRuleTests` (Architecture.Tests) fails when a project outside `PulseOffice.Platform*`
calls `Path.GetTempPath`, `Path.GetTempFileName` or `Directory.CreateTempSubdirectory`.

## Reviewed without defects

The remaining uses of the shared temporary folder are in the platform projects: `TemporaryFiles` (owner-only root,
refused when shared or a link) and the update installers' scripts and dmg mount point (no document content).

## Open items

- PDF export and printing of a protected document still produce clear output by design (round 176).
- The in-memory spool holds the whole sheet body of very large workbooks in memory while saving.

## Not built or run

Code written without building or running tests, by instruction; the coordinator builds and tests after the join.
