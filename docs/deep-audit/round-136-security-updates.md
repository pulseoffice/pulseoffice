# Round 136: security review of updates, Linux desktop integration and the agent server

Date: 2026-11-10. The code was read as an attacker would read it: hostile release feed, hostile download hosts,
another process of the same user tampering with files between verification and use, hostile file names in the
user's folders, hostile web pages talking to the local agent server.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Updates/Downloads.cs | 125/125 | sound (see notes) |
| src/PulseOffice.Updates/Verification.cs | 98/98 | sound |
| src/PulseOffice.Updates/UpdateService.cs | 123/123 | sound |
| src/PulseOffice.Updates/ReleaseFeed.cs | 84/84 | sound |
| src/PulseOffice.Updates/ReleaseVersion.cs | 42/42 | sound |
| src/PulseOffice.Updates/UpdatePackages.cs | 47/47 | sound |
| src/PulseOffice.Updates/Releases.cs | 28/28 | sound |
| src/PulseOffice.Updates/InstalledVersion.cs | 26/26 | sound |
| src/PulseOffice.Updates/Preferences.cs | 74/74 | sound |
| src/PulseOffice.Shell/Updates/UpdateCoordinator.cs | 98/98 | sound |
| src/PulseOffice.Shell/Updates/UpdateOfferView.cs | 138/138 | sound |
| src/PulseOffice.Platform/UpdateInstallation.cs | 23/23 | changed (checksum carried by the staged update) |
| src/PulseOffice.Platform.MacOS/MacUpdateInstaller.cs | 130/130 | sound for its threat model (notes) |
| src/PulseOffice.Platform.Windows/WindowsUpdateInstaller.cs | 50/50 | defect D3, fixed |
| src/PulseOffice.Platform.Linux/AppImageUpdateInstaller.cs | 69/69 | defect D4, fixed |
| src/PulseOffice.Platform.Linux/AppImageLauncherScript.cs | 71/71 | defect D1, fixed |
| src/PulseOffice.Platform.Linux/AppImageDesktopIntegration.cs | 145/145 | defect D2, fixed |
| src/PulseOffice.Shell/Desktop/DesktopIntegrationCoordinator.cs | 212/212 | sound |
| src/PulseOffice.Agent/Transport/AgentHttpServer.cs | 300/300 | sound, no regression |
| src/PulseOffice.Agent/Security/AgentAccessCode.cs | 69/69 | sound |
| src/PulseOffice.Agent/Security/AgentSessions.cs | 145/145 | sound |
| src/PulseOffice.Agent/Security/AgentToolInvoker.cs | 90/90 | sound |
| src/PulseOffice.Agent/Tools/DocumentTools.cs | 211/211 | sound (note on overwrite race) |
| src/PulseOffice.Agent.UI/AgentOperationGate.cs | 58/58 | sound, modal gate intact |

The other agent files (protocol, catalogue tools, hosting adapters, settings editor) were not re-read in this round.

## Defects

- **D1, grave: the AppImage launcher ran any downloaded file named like the app.** When the registered image was
  missing, `AppImageLauncherScript` took the newest `PulseOffice*.AppImage` from Downloads, Desktop, home and other
  folders, made it executable and ran it; it also made the registered path executable if it had lost the bit. A web page
  that makes the browser save a file with that name in Downloads got it executed from the applications menu, bypassing
  the fact that downloads are not executable. Fix: the launcher runs only files that are already executable (the user
  gave the bit, as the app needed it to run in the first place), never adds the bit by itself outside the explicit file
  chooser, and only considers names made of safe characters. Tests:
  `The_launcher_never_runs_a_downloaded_image_that_is_not_executable`,
  `The_launcher_never_runs_a_registered_path_replaced_by_a_file_that_is_not_executable`.
- **D2, medium: the menu entry's Exec line was escaped wrongly.** Only `"` was escaped, and as `\"`, which after the
  Desktop Entry string unescaping is an invalid escape; `\`, `$`, `` ` `` were not escaped inside the quoted argument,
  `%` was not doubled, and a line break in the data folder would have written extra keys into the entry. Fix:
  `ExecArgument` applies the quoting rules (backslash before `"`, `` ` ``, `$`, `\`), doubles `%`, then applies the
  string-level backslash escaping; control characters are refused before anything is written. Tests:
  `The_menu_entry_escapes_the_launcher_path_as_the_desktop_entry_specification_requires`,
  `A_data_folder_with_a_line_break_is_refused`.
- **D3, medium: Windows ran the installer elevated without checking it again.** The verified installer sat in the
  user-writable cache from the moment of download until the app quit (possibly hours later); any process of the user
  could replace it, for example with an older installer signed by the same publisher, and the UAC prompt would look
  legitimate. Fix: staging records the SHA-256 of the verified package (`StagedUpdate.Sha256`), the helper receives it
  and runs the installer only if `Get-FileHash` still matches; otherwise it just reopens the app. Test:
  `The_windows_installer_is_staged_with_the_hash_its_helper_checks_before_elevating`.
- **D4, minor: Linux staging wrote through a planted link.** `File.Copy(..., overwrite: true)` onto
  `.<image>.update` followed a symbolic link left there, overwriting its target. Fix: the old entry is deleted and the
  staged file created with `CreateNew` and its mode set at creation. Test:
  `Staging_an_appimage_does_not_write_through_a_planted_link`.

## Verified sound

- Downgrade: the package name is derived from the feed's version and must appear in the signed checksum list, so an
  older signed list cannot vouch for a package announced as newer; checksum, size cap and length are checked while
  streaming; the signature is checked before the list is parsed.
- Redirects: the first and the final URL must be HTTPS on a GitHub host; intermediate hops cannot change content,
  which the signature binds.
- Feed parser: depth limit, 2 MB cap, notes truncated, non-HTTPS assets dropped, drafts and pre-releases ignored.
- Agent server: loopback socket, Host header and Origin must be loopback (`Origin: null` is refused), bearer code
  compared in constant time before the body is read, body cap, session and request caps; mutating calls refused
  under an open modal.

## Remaining (not fixed, low)

- macOS and Linux swap scripts do not re-verify the staged bundle; the target is user-writable anyway, so a same-user
  attacker gains nothing. A pid reused after exit would delay the swap.
- The Windows checksum is computed at staging, right after verification, not passed from the signed list; the window
  is milliseconds.
- If the app is killed between the two `mv` of the macOS swap, the bundle is left as `.previous` with no automatic
  recovery.
- `save_document_as` and `export_document` check existence at confirmation time; a file created in between is
  overwritten without a second question.

## To test live

- Linux: menu entry with a home or data folder containing spaces, `$` and `%`; launcher after moving the image and
  after deleting it with a non-executable copy in Downloads.
- Windows: a full update, and an update after replacing the downloaded installer (the helper must only reopen the app).
