# Deep audit round 22: platform services

Scope: `src/PulseOffice.Platform*`, focused on external process launch, shell opening, mail, sharing, printing and
secret vaults. Previous rounds (`platform.md`, `platform-round2.md`) covered temporary files, atomic writing, video and
the settings store; they are not repeated.

## Coverage

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Platform/ShellOpening.cs | 62/62 | defect 1 fixed; launch extracted to `IShellLaunch` (`SystemShellLaunch`) |
| src/PulseOffice.Platform/ExternalProcess.cs | 75/75 | ok; `IExternalProcess` extracted so callers can be tested with fakes |
| src/PulseOffice.Platform/ExecutableFileTypes.cs | 30/30 | ok: trailing dots/spaces trimmed, ADS suffix caught by the extension |
| src/PulseOffice.Platform/ProcessSharing.cs | 32/32 | ok |
| src/PulseOffice.Platform/Sharing.cs | 158/158 | defect 2 fixed (`MailAddressFilter`); `ProcessMailComposer` takes `IExternalProcess` |
| src/PulseOffice.Platform/Printing.cs | 176/176 | ok: `lp` arguments in `ArgumentList`, path made absolute, `LC_ALL=C`; printer id quotes stripped for `printto` |
| src/PulseOffice.Platform/Secrets.cs | 126/126 | ok: secrets only on stdin, never on argv or in the log |
| src/PulseOffice.Platform.Linux/XdgMailComposer.cs | 46/46 | defects 2 and 3 fixed |
| src/PulseOffice.Platform.Linux/SecretToolVault.cs | 36/36 | minor declared (4) |
| src/PulseOffice.Platform.MacOS/MailAppComposer.cs | 82/82 | ok: AppleScript literals escaped; defect 3 fixed for Bluetooth and attachments |
| src/PulseOffice.Platform.MacOS/KeychainSecretVault.cs | 71/71 | ok: secret hex-encoded on `security -i` stdin |
| src/PulseOffice.Platform.Windows/MapiMailComposer.cs | 194/194 | defect 3 fixed for `fsquirt`; MAPI memory released in `finally` |
| callers: DeckMediaFiles, EmbeddedFileLauncher, ShellSendCommands, RasterImageOperations | read | ok: document links pass `CanRunCode` and the network share guard before `OpenFile`; addresses only http/https |

## Defects

1. **Minor** `ShellOpening.cs:31` (before): `OpenFile` passed a relative path unchanged to `open`/`xdg-open`; a file
   named `-a…` or `--…` became an option of the launcher. Now the path is made absolute before the existence check and
   the launch. Test: `ARelativeFileWhoseNameLooksLikeAnOptionReachesTheLauncherAsAFullPath`.
2. **Medium** `XdgMailComposer.cs:21,24` (before): recipients were filtered only for a leading `-`; `xdg-email` treats a
   `mailto:` argument as a full URI, so `mailto:x@y?attach=/etc/passwd` (or a Cc with `?attach=`) could attach any local
   file in clients that honour `attach`. Today the send commands pass no recipients, so it was latent at API level.
   `MailAddressFilter.IsPlain` now accepts only plain addresses. Test: `XdgEmailNeverReceivesARecipientThatIsAMailtoUriOrAnOption`.
3. **Minor** `XdgMailComposer.cs:22,44`, `MailAppComposer.cs:41,80`, `MapiMailComposer.cs` (`FsquirtSharing`):
   attachments and Bluetooth files were passed as given; a relative name starting with `-` became an option. All are
   made absolute. Test: `XdgEmailAttachmentsAndBluetoothFilesArePassedAsFullPaths`.

All three tests failed before the fix (verified by reverting the fix lines) and pass after.

## Declared, not fixed

4. **Minor** `SecretToolVault.cs`: `secret-tool` parses with GLib options, so a service or account value starting with
   `-` could be read as an option. Values come from our own code (data connection ids); not changed without a real
   Linux to verify that `--` is honoured.
5. `WindowsPrintService` still uses the `print`/`printto` shell verbs on the produced PDF: it runs the default PDF
   handler; untested on Windows (open point in CLAUDE.md).

## Files changed

`src/PulseOffice.Platform/ShellOpening.cs`, `ExternalProcess.cs`, `Sharing.cs`,
`src/PulseOffice.Platform.Linux/XdgMailComposer.cs`, `src/PulseOffice.Platform.MacOS/MailAppComposer.cs`,
`src/PulseOffice.Platform.Windows/MapiMailComposer.cs`, `src/PulseOffice.Desktop/Composition/SharingProviders.cs`,
`ObjectsAndScriptingComposition.cs`, `PlatformIntegrationComposition.cs` (registrations, outside the area, minimal),
`tests/PulseOffice.Shell.Tests/PlatformRound22AuditTests.cs` and its `.csproj` (reference to `Platform.Linux`).

## Tests run

Shell.Tests 205/205 green; Desktop project builds with no warnings.

## Live checks

- macOS: Send > Mail with an attachment; image external edit; Deck linked video play: each opens as before.
- Linux: Send > e-mail (`xdg-email`) and Bluetooth (`bluetooth-sendto`) with a document saved under a folder with
  spaces and Unicode characters.
- Windows: Bluetooth (`fsquirt`), mailto fallback and MAPI with an attachment; printing to a named printer.
