# Deep audit, round 2 — platform, shared text, security, scripting, agent

Area: `src/PulseOffice.Platform*`, `Text*`, `Security`, `Scripting*`, `Agent*`, `Remote`, `Data*`, `Settings`.
Branch: the agent worktree, started from `main` at `1ce6f8a8`.

## Group 1 — Deck video export streams its frames

**Defect (serious, performance/memory).** `DeckExportCommands.ExportVideoAsync` rendered every visible slide to a BGRA
array before calling the encoder (`IVideoEncoder.EncodeAsync(IReadOnlyList<VideoFrame> …)`): about 8 MB per slide at
1920 px, so roughly 800 MB for 100 slides, all alive at once. VideoToolbox made it worse by pinning every frame
(`GCHandle.Alloc(…, Pinned)` for `CVPixelBufferCreateWithBytes`) until the end of the session. Rendering also ignored
the cancellation token, and every failure reached the status bar as the generic `deck.export.failed`.

**Fix (restructured).**
- `PulseOffice.Platform/VideoFrameSource.cs`: `IVideoFrameSource` (count, size, `RenderAsync(index, ct)`), the
  `VideoFrameException` carrying a diagnostic code, and `ListVideoFrameSource` for callers that already hold frames.
- `IVideoEncoder.EncodeAsync(IVideoFrameSource, fps, path, ct)`. The three encoders pull one frame at a time:
  ffmpeg writes each frame to stdin and drops it; VideoToolbox copies each frame into a `CVPixelBufferCreate` buffer
  it owns (lock, copy row by row honouring the real stride, unlock) so nothing managed stays pinned; Media Foundation
  copies into its own `IMFMediaBuffer` as before. All three turn a `VideoFrameException` into a failed
  `OperationResult` with the exception's code. Media Foundation compiles but cannot be run on this Mac.
- `VideoFrameFitter` is now a Decorator over the source: it crops odd sizes and validates length frame by frame
  (`VIDEO004`).
- Deck: `DeckVideoFrameSourceFactory` (injected as `IDeckVideoFrameSourceFactory`) renders the first slide to learn
  the size, keeps it only until the encoder asks for it, and renders the rest on demand.
  `DispatchedSlideFrameRenderer` marshals `RenderBgra` to the UI thread (`Dispatcher.UIThread.InvokeAsync` with the
  token), `GuardedSlideFrameRenderer` turns rendering exceptions into `VIDEO006`.
- The status bar now says `deck.export.video.failed` ("Video export failed: {0}") with the diagnostic described by
  `IDiagnosticTexts`; new keys `deck.export.video.failed` and `diagnostic.video006` in the five languages.

**Tests.** `Shell.Tests/VideoEncodingRegressionTests`: at most 3 frames alive at once while VideoToolbox encodes 40
(previously all 40 were pinned), cancellation stops rendering after the frame that cancelled and leaves no file, a
rendering error becomes `VIDEO006` with its message. `Presentations.UI.Tests/DeckVideoFrameSourceTests`: one render
at creation, then one per request, `VIDEO006` wrapping, cancellation renders nothing. The old tests (odd size,
wrong length) moved to the source API. `Desktop.Tests` deck export test still encodes a real mp4.

**Remaining.** VideoToolbox still collects the compressed samples and builds the mp4 in memory (`Mp4Writer.Build`):
compressed H.264 at one key frame per slide, a few MB per hundred slides, acceptable.

## Group 2 — `RichTextInsertion.Merge` was quadratic

**Defect (medium, performance).** `src/PulseOffice.Text.Rich/RichTextInsertion.cs` merged adjacent runs with the same
format by `merged[^1].Text + run.Text`, copying the growing string at every run: pasting a fragment of 200,000 runs
with one format took 17.8 s (same defect already fixed in `DocxInlineReader.Merge` and `ParagraphText.Normalize`).

**Fix.** The insertion now reuses `RunMerger` (the linear, `StringBuilder`-based merger the formatter already had),
which gained a `Merge(IEnumerable<TextRun>)` overload; the insertion only filters empty non-field runs first. One
merger for the whole library instead of two.

**Tests.** `RichTextInsertionTests.Molte_parti_con_lo_stesso_formato_si_fondono_in_tempo_lineare` (failed at 17,818 ms
before, now well under the 1 s ceiling) and `I_campi_restano_parti_a_se_e_il_formato_diverso_interrompe_la_fusione`
(fields never merge, a different format breaks the run, empty runs vanish).

## Group 3 — macro file writes followed a link swapped in after the check

**Defect (medium, security).** `src/PulseOffice.Scripting/MacroFiles.cs:71-87` (first-round code): `Resolve` refuses
links, but `WriteText`/`AppendText` then opened the path by name (`File.WriteAllText`, `File.AppendAllText`), which
follows a link put there between the check and the write. A JavaScript macro cannot make links, but an unsandboxed
Python macro or any other process can, and the grant then lets a sandboxed macro write outside `Macros`.

**Fix.** `WriteText` writes a fresh `CreateNew` temporary file (dot-prefixed, a name `Resolve` never accepts) in the
target directory and renames it over the target: a rename replaces a link instead of following it. `AppendText`
opens the file first, then checks the path is still not a link and measures the size on the open stream before
writing anything.

**Residual (declared).** Append still has a window if a link is placed for the open and removed again before the
re-check (two swaps by another process with write access to the user's Documents, which could write the target
anyway); swapping a parent directory for a link between check and rename is the same case.

**Tests.** `Un_collegamento_messo_dopo_il_controllo_non_porta_la_scrittura_fuori` (a folder decorator that swaps a link
in after `Resolve`: failed before, the outside file was overwritten with "macro") and
`Un_collegamento_a_un_file_che_non_esiste_ancora_e_rifiutato` (dangling link: already refused).

## Group 4 — review of the trusted timestamp code (first-round change)

`src/PulseOffice.Security/TrustedTimestamps.cs` and `CertificateJudge.cs` read in full.

- **Checked, no defect**: a token signed by a certificate that is trusted but not issued for time stamping does not
  count — `Rfc3161TimestampToken.VerifySignatureForData` already demands the `id-kp-timeStamping` usage and the ESS
  signing-certificate attribute. Regression test `Una_marca_firmata_da_un_certificato_fidato_ma_non_per_le_marche_non_conta`.
  The imprint is checked against the canonical `SignatureValue`; the authority chain is built at the stamp's date;
  a token that does not decode, does not match or does not chain falls back to judging at today's date (decision 22).
- **Minor, fixed**: the "stamp from the future" guard read `DateTimeOffset.Now` while the judge reads the injected
  `TimeProvider`, so the two could disagree (and tests could not pin the clock). The verifier now takes the same
  `TimeProvider` (DI picks the two-argument constructor; `TimeProvider.System` is already registered). Test
  `Una_marca_nel_futuro_secondo_l_orologio_iniettato_non_conta`.
- Performance: one `X509Chain` per signature and one per stamp, offline revocation: no regression.

## Group 5 — macro write grants: a failed save crashed the editor and left the memory out of step

`MacroWritePermission.cs`, `MacroWriteAccess.cs`, `MacroWriteAccessButton.cs`, `PermissionScopedScriptEngine.cs` read in full.

**Defect (medium).** `FileMacroWriteGrants.Grant`/`Revoke` (`MacroWritePermission.cs:48-69` before) added to the
in-memory set and then saved: an `IOException` or `UnauthorizedAccessException` (settings folder read-only, full
disk) escaped through `MacroWriteAccessButton.ToggleAsync`, an `async` click handler, so it reached the dispatcher as an
unhandled exception; and the in-memory set already said "granted" while the file did not (or "revoked" while the file
still granted, which came back at the next start). The 4096-grant cap threw the same way.

**Fix.** `Grant` and `Revoke` return whether they took effect; they build the new set, save it and only then replace
the cached one. I/O failures and the cap are logged in English and return `false`; the button refreshes from the real
state, so a failed revoke keeps showing "granted". `IMacroWriteAccess.Revoke` returns the outcome as well.

**Tests.** `Se_il_permesso_non_si_salva_non_resta_concesso_e_non_scoppia` and
`Se_la_revoca_non_si_salva_il_permesso_resta_e_lo_dice` (did not compile before: `void`; the old code threw).

**Checked, no defect.** The fingerprint (SHA-256 of language + LF-normalised source) is recomputed on every keystroke
by the editor's button: negligible for macro-sized sources. `PermissionScopedScriptEngine` restores the previous
`AsyncLocal` scope after the run. The grant is never readable or writable by the agent (`MacroWritePermissionAgentTests`).

## Group 6 — temporary files and atomic writes (first-round code)

`TemporaryFiles.cs` and `AtomicFileWriter.cs` read in full.

- **Medium, fixed** `TemporaryFiles.cs:69-76`: the start-up sweep deleted every `session-*` folder whose folder date
  was older than a day. A folder's date changes only when entries are added or removed directly in it, so a second
  instance started next to one running for more than a day deleted the first instance's live files (prints, opened
  embedded objects). The sweep now skips a session whose process id (in the folder name) is still running.
  Test `LaSessioneVecchiaDiUnIstanzaAncoraVivaNonSiCancella` (failed before). The existing sweep test used pid 1,
  which is always alive; it now uses an id no process has.
- **Minor, fixed** `TemporaryFiles.cs:45`: a root that is a symbolic link (on a shared `/tmp`, planted by another user
  towards a folder of the victim's) was accepted because the permission check reads the target. The root is now
  refused when it is a link, and a fresh private folder is used. Test
  `UnaRadiceCheEUnCollegamentoNonOspitaIFileDellaSessione` (failed before).
- **Checked, declared** `AtomicFileWriter.CommitAsync`: replacing by rename turns a destination that is a symbolic
  link into a regular file (the link is replaced, not followed) and on Windows does not keep the destination's ACLs;
  Unix permissions are copied. This is the usual trade-off of atomic saves and is left as is.

## Group 7 — first-round leftovers

- **Minor, fixed** `src/PulseOffice.Agent/Security/AgentOperationLog.cs` (first round: "grows without rotation"): every
  agent call appended a line with the full detail, so a chatty or hostile client could fill the disk. The detail is now
  cut at 4096 characters and the file rotates to `operations.jsonl.1` at 4 MB (two files at most, both 0600).
  Test `Il_registro_delle_operazioni_ruota_e_non_cresce_senza_limite` (1200 calls with a 100 KB detail: 120 MB
  before, now at most 8 MB).
- **Re-read, still declared** `ConnectionSecretSplitter` (only `Password`/`Pwd` go to the keychain; `SSL Password`,
  `Proxy Password` stay in the 0600 connections file): moving several secrets needs a structured keychain entry and a
  change in the five providers' injection, outside a minor fix; left declared.
- **Still declared**: B29 (graphemes over 64 code units), `TableEditorView`/`DataTableRenderer` performance,
  `ViewTools` page height, `ScriptOutput` routing with macro and agent at once, `MacroEditorView.Save` overwrite.

## Regression review of first-round files (read in full)

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Scripting/MacroFiles.cs | 102/102 | link race fixed (group 3) |
| src/PulseOffice.Scripting/MacroWritePermission.cs | 120/120 | failed save fixed (group 5) |
| src/PulseOffice.Scripting/ScriptExecutionPermissions.cs | 69/69 | ok: `AsyncLocal` scope restored, `MacroFolderWrite` implied only by full file write |
| src/PulseOffice.Scripting/PermissionScopedScriptEngine.cs | 30/30 | ok |
| src/PulseOffice.Scripting.Editor/MacroWriteAccess.cs | 66/66 | outcome returned (group 5); modal with veil, decline is default and Esc |
| src/PulseOffice.Scripting.Editor/MacroWriteAccessButton.cs | 69/69 | ok after group 5; re-entry guarded |
| src/PulseOffice.Security/TrustedTimestamps.cs | 120/120 | injected clock (group 4) |
| src/PulseOffice.Security/CertificateJudge.cs | 43/43 | ok: decision 22 respected, key usage checked |
| src/PulseOffice.Platform/TemporaryFiles.cs | 88/88 | live sessions, link root (group 6) |
| src/PulseOffice.Platform/AtomicFileWriter.cs | 144/144 | ok, trade-off declared (group 6) |
| src/PulseOffice.Platform/VideoEncoding.cs, VideoToolbox.cs, MediaFoundationVideo.cs, VideoFrameFitter.cs | all | restructured (group 1) |
| src/PulseOffice.Settings/JsonSettingsStore.cs | 170/170 | ok; minor declared: `Set` with `NaN` throws from the serializer, a symlinked settings file becomes a regular file on save |
| src/PulseOffice.Agent/Transport/AgentHttpServer.cs | 261/261 | ok: loopback address, Host and Origin checked before the code, constant-time code comparison, body capped before and while reading |
| src/PulseOffice.Agent/Security/AgentOperationLog.cs | 79/79 | rotation (group 7) |
| src/PulseOffice.Text.Rich/RichTextInsertion.cs | 61/61 | linear merge (group 2) |
| src/PulseOffice.Data/ConnectionSecrets.cs | 50/50 | declared (group 7) |

## Outcome

Defects: medium 5 (video export memory/cancellation/message, quadratic insertion merge, macro write link race, grant
save failure, sweep of live sessions), minor 3 (timestamp clock, temporary root link, operation log growth). All fixed
with tests that failed first (the video and grant ones failed to compile against the old API, which could not express
them). Tests run one project at a time, all green: Shell.Tests 183, Presentations.UI.Tests (video source) 3,
Text.Rich.Tests 95, Scripting.Tests 137, Security.Tests (timestamps) 9, Agent.Tests 64, Desktop.Tests (deck export,
macro) 4.

## Live checks after the merge

- Deck on macOS: export to mp4 a deck of 100+ slides at the default width: memory must stay flat (Activity Monitor),
  the window must stay responsive, and Cancel must stop it leaving no file.
- Deck: export video to a read-only folder: the status bar must say "Video export failed: …" with the reason, in the
  interface language.
- Deck on Linux (ffmpeg) and Windows (Media Foundation): the same export, never run on those systems.
- Macro editor: make `~/Library/Application Support/PulseOffice` (or the config folder) read-only, press the file-write
  button and accept: no crash, the button stays "off".
- Two instances of the app, the first left open more than a day with a print preview: starting the second must not
  break the first's preview files.
