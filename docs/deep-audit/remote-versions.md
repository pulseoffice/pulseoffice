# Remote versions: optimistic concurrency on save (round 30, O1)

Status: done (2026-11-05). Closes open point O1 of `cloud-autocorrect-round30.md`.

## Coverage
| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Remote/RemoteConnections.cs | 85/85 | interface to change: read returns a version, write takes conditions |
| src/PulseOffice.Remote/RemoteDocuments.cs | 63/63 | `RemoteDocument` must carry the version |
| src/PulseOffice.Remote/WebDavStorage.cs | 345/345 | PUT without `If-Match` |
| src/PulseOffice.Remote.Cloud/CloudSessions.cs | 112-238 | replies drop status and headers on failure: conflict indistinguishable |
| src/PulseOffice.Remote.Cloud/DropboxStorage.cs | 144/144 | `mode: overwrite` always |
| src/PulseOffice.Remote.Cloud/OneDriveStorage.cs | 151/151 | PUT without `If-Match` |
| src/PulseOffice.Remote.Cloud/GoogleDriveStorage.cs | 291/291 | PATCH without any revision check |
| src/PulseOffice.Remote.Cloud/ICloudDriveStorage.cs | 145/145 | local synced folder, overwritten blindly |
| src/PulseOffice.Desktop/Composition/ShellRemoteCommands.cs | 238/238 | save-as and check-in write unconditionally |
| src/PulseOffice.UI.Controls/ChoicePromptView.cs | 121/121 | reused for the conflict modal (cards, veil from the dialog host) |

## Progress
- Done: `IRemoteStorage.ReadAsync` returns `RemoteContent(Bytes, Version)`; `WriteAsync` takes
  `RemoteWriteConditions(Held, ExpectedVersion)` and returns `RemoteWriteOutcome(Written | Conflict, Version)`;
  `RemoteDocument.Version`. WebDAV, OneDrive, Dropbox, Google Drive and iCloud Drive storages rewritten; Remote.Cloud builds.
- Next: conflict resolution service and modal (Remote.UI), shell wiring, texts, tests.
- Done: `src/PulseOffice.Remote.UI/RemoteUploads.cs` (`IRemoteUploads`: write, conflict prompt, overwrite or copy),
  `src/PulseOffice.Remote.UI/DialogRemoteConflictPrompt.cs` (modal on `ChoicePromptView`), texts `remote.conflict.*`
  in the five languages, `ShellRemoteCommands` and its composition rewired.

## Design
- `IRemoteStorage.ReadAsync` returns `RemoteContent(Bytes, Version)`; the version is opaque to everyone but its storage.
- `WriteAsync(connection, path, content, RemoteWriteConditions(Held, ExpectedVersion))` returns
  `RemoteWriteOutcome(Written | Conflict, Version)`. A conflict is a successful result with its own status, never a failure;
  transport and HTTP errors stay failures. A null `ExpectedVersion` writes unconditionally (forced overwrite, new files).
- Per storage:

| Storage | Version read | Precondition on save | Conflict signal | Version after save |
|---|---|---|---|---|
| WebDAV | strong `ETag` (`etag:` prefix); weak or missing ETag falls back to `Last-Modified` (`modified:` prefix) | `If-Match` / `If-Unmodified-Since`, plus the lock `If` header when checked out | 412 | `ETag` of the PUT reply, otherwise a `HEAD` |
| OneDrive | `eTag` of the item metadata, read before the content | `If-Match` | 412 | `eTag` of the uploaded driveItem |
| Dropbox | `rev` in the `Dropbox-API-Result` header | `mode: {".tag":"update","update":rev}`, `autorename: false` | 409 with `error_summary` starting `path/conflict` (other 409s stay failures) | `rev` of the upload reply |
| Google Drive | `headRevisionId` (fallback `modifiedTime`) from the lookup | the lookup made before the upload compares the revision (Drive v3 has no `If-Match`; the check is not atomic) | revision differs, nothing uploaded | `headRevisionId` requested with `fields` on the upload |
| iCloud Drive | last write time ticks and length of the synced file | compared before writing | differs | recomputed after writing |

- `RemoteDocument.Version` is filled on open and replaced after every successful upload.
- `IRemoteUploads` (`src/PulseOffice.Remote.UI/RemoteUploads.cs`) runs the save: write with the known version; on a conflict it
  asks `IRemoteConflictPrompt` and then overwrites without the version, or asks `IRemoteCopyTarget` for a new name
  (suggested `remote.conflict.copyName`, e.g. `a (copy).odt`) and writes the copy, which the local document then follows, or
  cancels leaving the server untouched. `DialogRemoteConflictPrompt` is a `ChoicePromptView` (two cards plus Cancel in the
  footer) shown by `IDialogService`, so it has the veil. Texts `remote.conflict.*` in it, en, de, es, fr.
- `ShellRemoteCommands`: open stores the version; save-as-remote sends the known version (and the lock) only when the target
  is the remote file that local document already follows; check-in sends the version, then unlocks and follows the result.

## Tests
`tests/PulseOffice.Remote.Tests/RemoteVersionTests.cs`, fake `HttpMessageHandler`s only, no network: for each of WebDAV,
OneDrive, Dropbox, Google Drive and iCloud Drive a conflict is detected, a normal save sends the version and returns the new
one, a forced save sends no precondition; Dropbox distinguishes a version conflict from other 409s; WebDAV weak ETags fall back
to `If-Unmodified-Since`. `RemoteUploads` with a fake storage and prompt: normal save without prompt, overwrite, copy,
cancel (from the prompt and from the name dialog), failure without prompt. The tests target the new API, so they could not
compile before the change. Results: Remote.Tests 99/99, Localization.Tests 32/32, Architecture.Tests 40/40; Desktop builds
with 0 warnings. README does not describe remote saving: unchanged.

## Live checks
- Open a document from Dropbox, OneDrive, Google Drive and a WebDAV server; change it from the web interface or another device;
  save as remote to the same path: the "Version conflict" modal must appear with the veil and three choices.
- "Overwrite anyway" replaces the server copy; "Save as a copy" proposes `name (copy).ext` and the local document then follows
  the copy; "Cancel" (or Esc) leaves the server untouched and says so in the status bar.
- Save twice in a row without external changes: no modal (the version is updated after each save).
- WebDAV check-out, check-in after an external change by the same user: modal, then the lock is released.
