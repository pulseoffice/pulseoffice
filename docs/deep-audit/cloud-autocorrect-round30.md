# Deep audit round 30: cloud storages, remote browser, autocorrect and spelling helpers

Scope: what round 29 (`remote-spelling-round29.md`) left unread.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Remote.Cloud/GoogleDriveStorage.cs | 280/280 | defects C5, C6 (pages capped at 1000, replies bounded) |
| Remote.Cloud/CloudSessions.cs | 237/237 | defect C2 (refresh token only in the keychain, never on disk) |
| Remote.Cloud/DropboxStorage.cs | 144/144 | ok except open point O1 (overwrite mode) |
| Remote.Cloud/OneDriveStorage.cs | 151/151 | ok (nextLink restricted to graph.microsoft.com, segments escaped) |
| Remote.Cloud/ICloudDriveStorage.cs | 145/145 | ok (paths with `..`, `.` or rooted parts refused) |
| Remote/RemoteDocuments.cs | 63/63 | ok |
| Remote.UI/RemoteBrowserView.cs | 674/674 | defects C3, C4 |
| Remote.Cloud/CloudApplications.cs | 82/82 | defect C1 |
| Text.Spelling/Spelling.cs | 150/150 | defect S1 (round 29 open point) |
| Text.Spelling/WordTokenizer.cs | 94/94 | ok |
| Text.Spelling/Grammar.cs | 96/96 | ok (repeated-word regex anchored by `\b`, linear per word) |
| Text.Spelling/WordCompletion.cs | 78/78 | defect S2 |
| Text.Spelling/AutoCorrect.cs | 451/451 | defect S3 |
| Text.Spelling/AutoCorrection/AutoCorrectSources.cs | 194/194 | ok (16 MB cap, depth 8, keys and values bounded) |
| Text.Spelling/AutoCorrection/AutoCorrectProfileCodec.cs | 134/134 | ok |
| Text.Spelling/AutoCorrection/AutoCorrectListImport.cs | 61/61 | ok (DTD prohibited, part and entry caps) |
| Text.Spelling/AutoCorrection/ReplacementListDraft.cs | 118/118 | ok |
| Text.Spelling/AutoCorrection/AutoCorrectDefaults.cs | 91/91 | ok |
| Text.Spelling/AutoCorrection/AutoCorrectDraft.cs | 138/138 | ok |
| Text.Spelling/AutoCorrection/AutoCorrectProfile.cs | 48/48 | ok |
| Text.Spelling/AutoCorrection/WordListDraft.cs | 109/109 | ok |
| Text.Spelling/Dictionaries/DictionaryModel.cs | 120/120 | ok |
| Text.Spelling/Dictionaries/DictionaryCodes.cs | 68/68 | ok |
| Text.Spelling/Dictionaries/DictionaryLicenseClassifier.cs | 75/75 | defect S4 |
| Text.Spelling.Hunspell/DictionaryLocations.cs | 21/21 | ok |

Also checked: `Desktop/Composition/ShellRemoteCommands.cs` (open/save flow): the local name of a downloaded file is
already sanitised (separators, invalid characters, `.` and `..`), so remote names cannot escape the temporary folder.

## Defects

| Id | Severity | Where | Defect | Status |
|---|---|---|---|---|
| C1 | medium | src/PulseOffice.Remote.Cloud/CloudApplications.cs:55 | A `cloud.json` whose root is not an object, or whose `clientId`/`scopes` is not a string, threw `InvalidOperationException` from the constructor, i.e. while composing the app. | fixed: non-object root ignored, non-string values skipped, `UnauthorizedAccessException` caught |
| C2 | medium | src/PulseOffice.Remote.Cloud/CloudSessions.cs:18 | The live token table was a plain `Dictionary` read on the fast path outside the renewal lock while sign-in, renewal and sign-out wrote it. | fixed: `ConcurrentDictionary` |
| C3 | medium | src/PulseOffice.Remote.UI/RemoteBrowserView.cs:464 | Forgetting a cloud connection removed only the password entry: the OAuth refresh token stayed in the keychain and the live session stayed signed in (a new connection with the same name inherited it). Renaming left the old live session too. | fixed: new `IRemoteCredentials` (`src/PulseOffice.Remote/RemoteCredentials.cs`) signs out and removes password and token; used for forget and rename |
| C4 | medium | src/PulseOffice.Remote.UI/RemoteBrowserView.cs:656 | The save name was appended to the remote path unchecked: `a/b`, `..`, `../x` or control characters produced nested or climbing remote paths. | fixed: `IRemoteFileNames` validation, message `remote.nameInvalid` in the five languages |
| C5 | medium | src/PulseOffice.Remote.Cloud/GoogleDriveStorage.cs:136 | Any failure of the file lookup (server error, refused sign-in, too many pages) was treated as "not found" and a new file was created: duplicates in Drive. | fixed: a file is created only when the lookup says not found (`CLOUD015`) |
| C6 | minor | src/PulseOffice.Remote.Cloud/GoogleDriveStorage.cs:198,223 | Drive names are case-sensitive, but the lookup took the first case-insensitive match: saving `report.odt` could overwrite `Report.odt`. | fixed: exact match first, case-insensitive only as fallback |
| S1 | medium | src/PulseOffice.Text.Spelling/Spelling.cs:99 | (round 29 open point) every mistake in a pass asked the dictionary for suggestions: thousands of mistakes ran thousands of suggestion searches. | fixed: at most `SpellChecker.MaximumSuggestedMistakes` (64) mistakes per pass get suggestions; the rest are still marked |
| S2 | minor | src/PulseOffice.Text.Spelling/WordCompletion.cs:42 | Once the list was full (1000 words) a newly seen word was the least frequent and evicted itself, so completion stopped learning; words had no length cap. | fixed: eviction by (count, last use) never removes the word just collected; words over 64 characters ignored |
| S3 | minor | src/PulseOffice.Text.Spelling/AutoCorrect.cs:386 | Ordinal superscript turned `2a`/`3o` into `2ª`/`3º` in every language ("Flat 2a" in English). | fixed: gendered ordinals apply only to it, es, pt, gl, ca |
| S4 | minor | src/PulseOffice.Text.Spelling/Dictionaries/DictionaryLicenseClassifier.cs:25,43 | Parentheses were discarded, so `GPL-2.0 AND (MIT OR BSD)` came out Free; `CC-BY-ND` was Free; unbalanced expressions were accepted. | fixed: recursive-descent SPDX evaluation (nesting capped at 32, `WITH` exceptions skipped, case-insensitive operators); ND and NC are Unknown |

## Open points
- O1 (medium, fixed afterwards in `remote-versions.md`): Google Drive, OneDrive and Dropbox overwrite on save without a version check, so a newer
  version saved elsewhere in the meantime is lost. The fix needs the version (ETag, `rev`) read at open time to travel
  with `RemoteDocument`, an `IRemoteStorage.WriteAsync` taking an expected version (`If-Match`, Dropbox `mode: update`)
  and a conflict prompt in the shell: a cross-cutting interface change (WebDAV included) left to a dedicated block.

## Tests
`tests/PulseOffice.Remote.Tests/CloudRound30Tests.cs` and `tests/PulseOffice.Text.Spelling.Tests/SpellingRound30Tests.cs`.
Verified failing before the fix (source reverted with `git apply -R`): C1 (4 cases), C2 (concurrency), C5, C6, S1, S2
(both), S3, S4 (3 cases). The `RemoteCredentials` and `RemoteFileNames` tests cover the new services that replace
the unchecked view code. After the fix: Remote.Tests 81/81, Text.Spelling.Tests 174/174, Localization.Tests 32/32,
Architecture.Tests 40/40; `PulseOffice.Desktop` builds with 0 warnings.

## Live checks
- Forget a signed-in OneDrive/Google/Dropbox connection and create it again with the same name: it must ask to sign in.
- Save as remote with the name `../x.odt` or `a/b.odt`: the dialog refuses it with the translated message.
- Save to Google Drive with an expired sign-in or offline: no duplicate file appears afterwards.
- Paste a long text full of mistakes in Folio with automatic spelling on: no stall; the first mistakes have suggestions.
