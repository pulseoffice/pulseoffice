# Deep audit round 23: the rest of PulseOffice.Platform*

Scope: the files of `src/PulseOffice.Platform*` that round 22 did not read, plus its declared point on `SecretToolVault`.
Notifications, system theme detection and locale do not live in the platform projects (no such type there): they were
out of this round's files.

## Coverage

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Platform/ClipboardFormats.cs | 54/54 | ok: BOM-aware decode, package format names sanitised |
| src/PulseOffice.Platform.Linux/LinuxClipboardFormats.cs | 5/5 | ok |
| src/PulseOffice.Platform.MacOS/MacClipboardFormats.cs | 5/5 | ok |
| src/PulseOffice.Platform.Windows/WindowsClipboardFormats.cs | 50/50 | defect 3 fixed |
| src/PulseOffice.Platform/WindowChrome.cs + three implementations | 26+16×3 | ok |
| (log updated here after 5 files) | | |
| src/PulseOffice.Platform/CloudFolders.cs | 27/27 | ok: iCloud only on macOS by nature, failure declared |
| src/PulseOffice.Platform/FontDirectories.cs | 40/40 | defect 2 fixed |
| src/PulseOffice.Platform/DictionaryDirectories.cs | 40/40 | defect 2 fixed |
| src/PulseOffice.Platform/NativeConstants.cs | 43/43 | ok: framework handles are process-lifetime singletons, `dlopen` failure handled |
| src/PulseOffice.Platform/Scanning.cs | 114/114 | minor 4 fixed |
| (log updated here after 10 files) | | |
| src/PulseOffice.Platform.Windows/WiaScannerService.cs | 100/100 | ok: script travels as `-EncodedCommand`, values single-quoted with `''` escaping |
| src/PulseOffice.Platform.Windows/CredentialManagerVault.cs | 109/109 | ok: `CredFree` in `finally`, blob zeroed before `FreeHGlobal`; target `service:account` is unambiguous because every caller uses the constant service `PulseOffice` |
| src/PulseOffice.Platform/ApplicationVisibility.cs | 29/29 | ok |
| src/PulseOffice.Platform.MacOS/MacApplicationVisibility.cs | 36/36 | ok |
| src/PulseOffice.Platform.MacOS/MacActiveDisplayProbe.cs | 23/23 | ok: display link released |
| (log updated here after 15 files) | | |
| src/PulseOffice.Platform/DisplayReadiness.cs | 50/50 | ok: the clock is created right before the wait in `Program` |
| src/PulseOffice.Platform/FileDialogs.cs | 56/56 | ok |
| src/PulseOffice.Platform/TemporaryFiles.cs | 107/107 | ok: owner-only folders, symlinked roots refused |
| src/PulseOffice.Platform/Secrets.cs (again) | 126/126 | restructured for defect 1 |
| src/PulseOffice.Platform.Linux/SecretToolVault.cs (again) | 36/36 | defect 1 fixed |
| src/PulseOffice.Platform.MacOS/KeychainSecretVault.cs (again) | 71/71 | ok, follows the new base |

## Defects

1. **Medium** `SecretToolVault.cs:13,23,32` (round 22's declared point 4): `secret-tool` parses its arguments with GLib
   options, so a service or account starting with `-` was read as an option and the call failed or did something else.
   Fixed: attributes now follow `--`, the label is passed as `--label=…`. The base `CommandLineSecretVault` duplicated
   the process code of `ExternalProcess`; it now takes `IExternalProcess` and a new `IExecutableResolver` (implemented
   by `PathExecutableLocator`), so the arguments are testable with fakes and secret-tool is never executed in tests.
2. **Medium** `FontDirectories.cs:34-38`, `DictionaryDirectories.cs:31-38`: on Linux only fixed `/usr/share` paths were
   searched, ignoring `XDG_DATA_DIRS`; in the Flatpak package (`/app/share`, host fonts at `/run/host/fonts`,
   `/run/host/local-fonts`, `/run/host/user-fonts`) no system font or dictionary was found. Fixed with
   `XdgDataDirectories` over an injected `IEnvironmentVariables` and a `PlatformFamily` strategy chosen by
   `RunningPlatform`; relative entries are ignored as the XDG specification requires.
3. **Minor** `WindowsClipboardFormats.cs:16-24`: when the HTML contained an `<!--EndFragment-->` without a start marker,
   the wrapped document's fragment offsets pointed at that stray marker and the paste was cut. Fixed: offsets of the
   wrapper are computed from the wrapper itself.
4. **Minor** `Scanning.cs:69,87`: `SaneScannerService` built `new ExternalProcess()` inline (not injectable) and
   formatted the resolution with the current culture. Fixed: `IExternalProcess` injected, invariant culture.

## Tests

`tests/PulseOffice.Shell.Tests/PlatformRound23Tests.cs` (6 tests). Defect 3's test was run against the original file
and failed (1 of 6), then passes. Defects 1, 2 and 4 needed the new seams to be observable, so their tests do not
compile against the original code. Ran one at a time: Shell.Tests 211/211, Architecture.Tests 38/38,
Text.Spelling.Tests 160/160; Desktop builds.

## To try live

- Linux: save a database password with a connection name starting with `-`; inside the Flatpak, check that system and
  user fonts appear in the font list and that installed hunspell dictionaries are found.
- Windows: paste HTML containing a stray `<!--EndFragment-->` into Word.
