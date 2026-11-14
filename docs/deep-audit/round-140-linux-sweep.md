# Round 140: the command sweep and the platform layer on Linux

First audit round run on Linux (Fedora, GNOME on Wayland through XWayland). It starts from the open items of the deep
audit closed on 2026-11-14 (work log entry of that date) and from the full suite, which on this machine was not green.

## Method

- Full suite and command sweep (`PulseOffice.Desktop.Sweep.Tests`) run on this machine, then the same sweep on clean
  `main` in a separate worktree, to tell regressions from defects that were already there.
- Sweep cases rerun with `PULSEOFFICE_SWEEP_OUT`, which writes each command's outcome and effect, to find the commands
  that run without effect and without a reason.
- Every file of `src/PulseOffice.Platform.Linux` read in full; the external tools the Linux code relies on checked on
  this machine (`secret-tool`, `lp`, `lpstat`, `xdg-open`, `xdg-mime`, `update-desktop-database`, `notify-send`,
  `zenity`, `ffmpeg`, `scanimage` present; `kdialog`, only used on KDE, absent).
- Timing tests failing only under the full run measured alone and against clean `main`.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Platform.Linux/AppImageUpdateInstaller.cs | 77/77 | medium defect fixed |
| src/PulseOffice.Platform.Linux/SecretToolVault.cs | 46/46 | minor fixed (constructor bypassing DI) |
| src/PulseOffice.Platform.Linux/AppImageDesktopIntegration.cs | 161/161 | clean; minor notes below |
| src/PulseOffice.Platform.Linux/AppImageLauncherScript.cs | 70/70 | clean (path quoted for the shell) |
| src/PulseOffice.Platform.Linux/XdgMailComposer.cs | 48/48 | clean (addresses filtered, paths made absolute) |
| src/PulseOffice.Platform.Linux/LinuxWindowChrome.cs | 18/18 | clean |
| src/PulseOffice.Platform.Linux/LinuxClipboardFormats.cs | 5/5 | clean |
| src/PulseOffice.Desktop/Composition/SecretVaultProvider.cs | 25/25 | minor fixed (vaults built outside the container) |
| src/PulseOffice.Platform/Printing.cs | 176/176 | minor fixed (services built in a field and with their own process runner) |
| src/PulseOffice.Platform/Scanning.cs | 119/119 | minor fixed (constructor building its own process runner) |
| src/PulseOffice.Platform/ProcessSharing.cs | 31/31 | minor fixed (same) |

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | medium | `AppImageUpdateInstaller.InstallAfterExit` | The swap helper was started with its output redirected to pipes the app never read; they close when the app exits, so an error message from `mv` would stop the helper before it restarts the app. | The script sends its own output to `/dev/null`; the helper is started without redirection. Test: the helper swaps the image and starts the new one after the app's process has ended. |
| 2 | medium | `LedgerShellViewCommands` | Formula bar, sheet tabs and headers toggles had no checked state: menus could not show whether the part is visible, and hiding the formula bar was a silent command for the sweep. | `CheckedWhen` from the view options. Test seen red without the fix. |
| 3 | medium | `FolioObjectCommands` (order front, forward, backward, back) | Enabled when the object could not move (single object, already on top or at the bottom): the command ran and did nothing. | `IObjectArrangement.Moves` decides the enabled state. Test on the model. |
| 4 | medium | `FolioListCommands` (`folio.list.continue`) | Enabled where continuing changes nothing. | `IListEditing.CanContinue`, computed by the same code as `Continue`. Test on the model. |
| 5 | medium | `FolioFieldRefresh` and the update commands | The refresh always reported a change, so updating fields or everything in a document with nothing to update was silent. | The refresh returns whether a pass changed anything; the commands say "Everything is already up to date" otherwise (five languages). |
| 6 | minor | `folio.tools.updateCharts` | Enabled in documents without charts. | Enabled only when the document holds a chart. |
| 7 | minor | `deck.layouts.gallery` | Without a chosen layout (palette, keyboard) it did nothing. | Opens the master and layouts view, as other galleries open their dialog without a choice; also the gallery's "more" entry. |
| 8 | minor | `SecretVaultProvider`, `SecretToolVault`, `KeychainSecretVault` | Vaults built with parameterless constructors that create their own process runner and resolver (rule 1). | The provider receives `IExternalProcess` and `IExecutableResolver` from the container; parameterless constructors removed; `PathExecutableLocator` registered once for both interfaces. |
| 9 | minor | `DeepAuditSheetsEngineCoreTests` (row insertion over many formulas) | An absolute 5 s limit that only a fast machine met: here a full recalculation alone takes 5.4 s and the insertion 6.1 s, the cost of one recalculation and not one per cell. | The test compares the insertion with a full recalculation measured on the same machine. |
| 10 | minor | `PrintServiceProvider`, `CupsPrintService`, `WindowsPrintService` | The provider built the services in a field initializer from `OperatingSystem`, and each built its own `ExternalProcess` (rule 1); the CUPS arguments could not be tested without a printer. | The provider receives `IHostSystem` and `IExternalProcess`; tests check the `lpstat` parsing, the `lp` arguments (printer, copies, collation, sides, job name, absolute path) and the message of a refused job. |
| 11 | minor | `SaneScannerService`, `WiaScannerService`, `SharingProcessRunner`, `SystemFontDirectories`, `SystemDictionaryDirectories` | Same pattern as defect 10: constructors that build their own process runner, environment or platform probe (`RunningPlatform`, which duplicated `IHostSystem`); with two constructors the container chose the parameterless one. | Everything comes from the container; `IHostSystem.Family` replaces `RunningPlatform`, which is removed; no `new ExternalProcess()` is left in `src`. |
| 12 | serious | `ledger.shapeText.leftToRight/rightToLeft`, the Drawing shape model | The commands, described as done in round 94, changed nothing: the shape model had nowhere to keep a paragraph's direction, `ShapeRichText` dropped it both ways, and no format wrote it. Seen by the sweep on Linux, where no incidental frame change hid it. | `ShapeObject.Directions` (one entry per paragraph, kept aligned by `Spliced` like `Lists`, cleared with the text); `ShapeRichText` and `ShapeTextParagraphs` carry it; XLSX/PPTX shapes write and read `a:pPr/@rtl`; ODS/ODP shapes write and read `style:writing-mode` in the paragraph style (the reader now resolves styles by name and family); `ShapeListText` draws directed shapes through the rich typesetter, which sets `FlowDirection`. Tests: model, XLSX ↔ ODS crossed round trip, the Ledger command with undo. |
| 13 | medium | sweep harness | The update check reached the real release feed, and its answer, posted as a notification, was not seen as an effect. | The sweep uses an update service that is always up to date and counts notification changes. |
| 14 | medium | `StatusBarService`, `StatusBarView`, `ThemeService` | Every closed shell window stayed alive with its whole container: the transient message timer was never stopped, a queued status refresh was never cancelled, and the theme service never left the application-wide theme event. Harmless in the app, which builds one window, but the command sweep, which builds one per case, grew to 5.4 GB. Found with heap dumps (`gcroot` on a closed `ShellWindow`). | The status service is disposable and stops its timer; the status bar coalesces refreshes into one pending operation, cancelled when it leaves the visual tree, and subscribes only while attached; the theme service leaves the event on dispose. `ShellWindowReleaseTests` (seen red on the old code) checks that closed windows are collected. |

Commands that legitimately do nothing on the sweep's plain Latin text (`folio.case.katakana`, `folio.case.hiragana`,
`folio.case.halfWidth`, `folio.format.clear`, `folio.charStyle.none`) were added to the sweep's list of commands
already in their state, with the reason, as the Ledger equivalents already were.

Why the sweep passed on macOS and not here: the silent commands were accepted there through incidental changes of the
rendered frame; on this machine the frame did not change, which showed that they had no effect of their own.

## Minor notes, left

- `AppImageDesktopIntegration.Remove` does not undo the `xdg-mime` defaults; a default pointing to a removed desktop
  entry is ignored by the desktop, so nothing breaks.
- `AppImageDesktopIntegration.Run` waits synchronously for `update-desktop-database`, `gtk-update-icon-cache` and
  `xdg-mime` on the UI thread; each takes well under a second.

## Not verified here

- Live checks: `uiprobe` and `windowshot` are macOS tools, so no click or key was sent to the app on this machine.
