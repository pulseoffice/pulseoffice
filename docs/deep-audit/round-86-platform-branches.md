# Deep audit round 86: operating-system branches behind the platform, agent server drain, properties titles

Leftovers of round 84 ([round-84-agent-server.md](round-84-agent-server.md)). Date: 2026-11-06.

## Scope
1. `FileConnectionDirectory` (Data) and `DocumentRecovery` (Shell) made their files private with `OperatingSystem`
   branches; then every `OperatingSystem.Is*`, `RuntimeInformation.IsOSPlatform` and `Environment.OSVersion` outside
   `PulseOffice.Platform*` (rule 15).
2. `AgentHttpServer.StopAsync` did not wait for requests being served.
3. The read-only `*_properties` agent tools showed their technical name in the operations list and confirmations.

## Changes

### 1. Host system and private files behind `PulseOffice.Platform`
- New `IHostSystem` (`src/PulseOffice.Platform/HostSystem.cs`): `Kind` (MacOS, Windows, Linux, OtherUnix),
  `VersionString`, `IsMacOS`/`IsWindows`/`IsLinux` (marked `SupportedOSPlatformGuard`, so the platform analyzer accepts
  them as guards), `PathComparison` (ignore case on macOS and Windows, ordinal elsewhere), `ExecutableName`.
  `RuntimeHostSystem` is the only implementation that asks the runtime; `FixedHostSystem` serves tests.
- `FileConnectionDirectory` and `DocumentRecovery` take an `IPrivateFiles` (optional, defaulting to the factory's
  choice); Desktop registers `IHostSystem`, `IPrivateFilesFactory`, `IPrivateFiles` in `AddPlatformIntegration` and
  passes the store to both. On Windows the recovery folder and the connections file now get the owner-only DACL
  (before: inherited ACL).
- Every other site moved to `IHostSystem`, injected where the type is built by DI:
  Desktop providers (`SecretVaultProvider`, `RichClipboardFormatsProvider`, `VideoEncoderProvider`,
  `ScannerServiceProvider`, `WindowChromeProvider`, `MailComposerProvider`, `FileSharingProvider`), application
  visibility, gesture notation, reserved shortcuts, shortcut capture, `ShellCommands` (redo gesture), `Program`
  (display probe), `DocumentOpenRouter`, `RecentDocuments`, `MacroFileFolder`, `ICloudDriveStorage` (`Combined` is now
  an instance method), `PythonDiscovery` and its sources/locators, `StandalonePythonCatalog`, `StandalonePythonStore`,
  `UserDataPaths`, `LogPaths`, `ReferenceFunctions` (`INFO("system")`, `INFO("osversion")`; registered by
  `AddSheetsEngine`). `SheetGridView` and `GridKeyboardInput` reuse `ShortcutGesture.Primary` instead of their own
  branch. `Settings`, `Diagnostics` and `Sheets.Engine` now reference `PulseOffice.Platform` (Foundation only, no
  Avalonia).
- Path containment where the system is not Windows, macOS or Linux (BSD) is now ordinal, the safe choice: before,
  `MacroFileFolder`, `DocumentOpenRouter` and `RecentDocuments` compared ignoring case there.
- Kept as a process-wide instance (no DI reaches them): `ShortcutGesture` (a value type with a static parser) and
  `LogPaths` (initialized before the container, decision 14) hold a `RuntimeHostSystem` in a static readonly field.
- Architecture test `SourceRulesTests.Operating_system_checks_live_only_in_the_platform_projects` forbids the three
  calls in `src` outside `PulseOffice.Platform*`. Tests keep their own OS skips (outside `src`).

### 2. Agent server drain
`AgentHttpServer` tracks the requests it is serving. `StopAsync` marks the server as draining (new requests get 503),
waits for the running ones up to `DrainTimeout` (5 s) on the injected `TimeProvider`, logs how many were still
running if the time runs out, then cancels them and closes the listener. `TimeProvider` is a constructor dependency
(`TryAddSingleton(TimeProvider.System)` in `AddAgent`).

### 3. Titles of the properties tools
`ApiRootFactory` gives every root a `Title` from `api.{type}.readProperties.title` (twelve roots, five languages);
`ScriptPropertiesTool.Title` uses it, so the operations list and logs say «Leggi proprietà del foglio» instead of
`ledger_sheet_properties`.

## Defects
1. Medium, rule 15 and Windows privacy: recovery copies and the connections file were private only on Unix; on Windows
   they inherited the folder ACL. Fixed through `IPrivateFiles`. Tests
   `DocumentRecoveryTests.Recovery_copies_go_through_the_private_file_store`,
   `ConnectionDirectoryTests.The_connections_file_is_written_through_the_private_file_store`; Unix mode tests still hold.
2. Medium, rule 15: about forty operating-system checks in twenty files outside the platform projects. Moved; guarded by the new
   architecture test (it fails on the old tree).
3. Minor, security on other Unix systems: case-insensitive containment checks for macro files, open documents and
   recents. Now ordinal. Test `MacroFolderHostSystemTests` (Linux kind refuses `MACROS/` next to `Macros/`).
4. Minor, agent: stopping the server cut the requests in flight (their clients got a reset). Tests
   `AgentServerDrainTests.Stopping_waits_for_a_request_already_being_served_and_its_client_gets_the_answer` (failed
   before: stop completed at once) and `Stopping_gives_up_on_a_stuck_request_once_the_drain_timeout_has_passed`.
5. Minor, agent UI: `*_properties` tools named technically. Tests
   `AgentServerHardeningTests.The_properties_tool_is_named_by_the_localized_title_of_its_object` (failed before) and
   `ScriptApiCatalogTests.Every_object_has_a_localized_title_for_its_properties_tool`.

Other tests: `HostSystemTests` (Shell.Tests: case rules, executable names, runtime kind),
`InfoHostSystemTests` (Sheets.Engine.Tests: `INFO` follows the injected host).

## Test runs
Agent.Tests 93/93, Architecture.Tests 43/43, Data.Tests 62 (25 skipped as before), Scripting.Tests 158/158,
Shell.Tests 222/222, Remote.Tests 99/99, Sheets.Engine.Tests (INFO subset) 28/28, Desktop.Tests (composition, shell
file commands, open path case) 31/31; Desktop builds without warnings.

## Remaining
- Windows ACLs for recovery and connections not run on a real Windows.
- Live checks (coordinator): quitting with an agent call in progress returns its answer; the agent settings card lists
  «Leggi proprietà del foglio» after a `ledger_sheet_properties` call.
