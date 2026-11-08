# Deep audit round 28: UI test projects and parallel execution

Round 27 (`flaky-tests-round27.md`) found that `PulseOffice.Sheets.UI.Tests` built Avalonia controls from several
threads at once; Avalonia's process-wide pools (`AvaloniaPropertyDictionaryPool`, styling and value-store pools)
assume a single UI thread, so the result was an intermittent `InvalidOperationException: Stack empty`.
This round checks every other test project for the same exposure and for shared mutable static state.

## Coverage (files read)
| File | Outcome |
|---|---|
| /Volumes/Dati/develop/regole-audit-profondo.txt | rules |
| docs/deep-audit/flaky-tests-round27.md | starting point |
| tests/Shared/UiThreadSession.cs | shared headless session wrapper, no defect |
| tests/PulseOffice.Sheets.UI.Tests/TestParallelization.cs | assembly-only fix of round 27 |
| tests/Directory.Build.props | shared test build settings |
| tests/PulseOffice.Desktop.Tests/HeadlessDesktopSession.cs, HeadlessIsolationTests.cs | shared collection + guard, no defect |
| tests/PulseOffice.Documents.UI.Tests/FolioScene.cs (session, `RunAsync`) | shared session, but non-session tests in the same assembly |
| tests/PulseOffice.UI.Controls.Tests/MacroEventsViewTests.cs (session owner) and the 5 files without session (`ObjectFrameChromeTests`, `AdjustHandleChromeTests`, `BlockDComponentsTests`, `SharedObjectViewTests`, `AuditSixteenTests`) | at risk: brushes and pens built on pool threads while the session thread builds controls |
| tests/PulseOffice.Presentations.UI.Tests/SlideSorterStripTests.cs, SlideBackgroundLayerTests.cs, DeckTableBordersTests.cs, DeckRound8AuditTests.cs | at risk: `Border`, `Panel` children, brushes built on pool threads, no session |
| tests/PulseOffice.Shell.Tests/DocumentTabsViewTests.cs, QuitGateTests.cs, ChartColoursTests.cs | at risk: `DocumentTabsView`, `Panel` built on pool threads |
| tests/PulseOffice.UI.Tests/IconCatalogTests.cs, ContrastTests.cs | at risk: geometries and brushes from the icon catalog and palette |
| tests/PulseOffice.Remote.Tests/*.csproj and sources | reaches Avalonia through Remote.UI, builds no Avalonia object |
| tests/PulseOffice.Agent.Tests, Scripting.Tests, UI.Commands.Tests (csproj and usings) | do not reach Avalonia, not at risk |
| tests/PulseOffice.Architecture.Tests/ProjectGraphTests.cs | model for the new guard |

## Findings

### 1. Avalonia objects built from several threads (test defect, medium)
Same mechanism as round 27 in five more assemblies: `Presentations.UI.Tests`, `Shell.Tests`, `UI.Tests`,
`UI.Controls.Tests` and `Documents.UI.Tests`. The last two own a headless session, but only part of their tests go
through it: tests that build brushes, pens, geometries or controls directly run on xUnit pool threads, in parallel with
the session thread, and share the same process-wide Avalonia pools. `Desktop.Tests` and `Desktop.Sweep.Tests` are safe:
every class is in the `Headless desktop` collection, guarded by `HeadlessIsolationTests`.
Not reproduced as a failure in this round (the race is rare: round 27 saw 2 in 7 runs in Sheets.UI); the fix is preventive
and structural.

Fix (structural, shared):
- `tests/Shared/SingleUiThreadAssembly.cs` (moved from `Sheets.UI.Tests/TestParallelization.cs`) holds the single
  `[assembly: CollectionBehavior(DisableTestParallelization = true)]`.
- `tests/Directory.Build.props` compiles it into every test project that declares
  `<AvaloniaTestThreading>SerialAssembly</AvaloniaTestThreading>`.
- Every test project whose project/package closure reaches an `Avalonia*` package must declare one of
  `SerialAssembly` (Sheets.UI, Presentations.UI, Shell, UI, UI.Controls, Documents.UI), `HeadlessCollection`
  (Desktop, Desktop.Sweep) or `NoAvaloniaObjects` (Architecture, Remote).
- Guard: `PulseOffice.Architecture.Tests/TestThreadingRulesTests` fails when a test project reaching Avalonia declares
  nothing (verified failing by removing the declaration from Remote.Tests) and when an assembly-level
  `CollectionBehavior` appears anywhere other than the shared file.

### 2. Shared mutable static state
Searched all test projects for static non-readonly fields, static mutable collections, settable static properties,
culture changes and environment variables.
- `Remote.Tests/WebDavLiveTests._next`: port counter, updated with `Interlocked`; fine.
- `Robustness.Tests/Fuzzing/SeedCorpus.Families`: static dictionary, read only after initialisation; fine.
- `Desktop.Tests`: `UserDataPaths.DataHomeVariable` / `LogPaths.DataHomeVariable` are process-wide environment
  variables set per class, and `LedgerEditorTests` switches `CultureInfo.CurrentCulture` (restored in `finally`); all
  these classes are in the single `Headless desktop` collection, so they never run concurrently. No defect.
- Static session holders (`MacroEventsViewTests.Session`, `FolioScene.Session`) are `static readonly` and intended.
No other mutable static state found outside `src/PulseOffice.Formats*` tests (not in scope).

## Verification
Each touched project built with `-m:2`, then run five times in a row, one project at a time:
- `Sheets.UI.Tests` 422 tests, 5/5 green; `Presentations.UI.Tests` 66, 5/5; `Shell.Tests` 213, 5/5;
  `UI.Tests` 26, 5/5; `UI.Controls.Tests` 345, 5/5; `Documents.UI.Tests` 302, 5/5.
- `Architecture.Tests` 40 green, including the new guard; the guard was seen failing with the declaration removed.
- Desktop, Desktop.Sweep and Remote only gained a declaration property (no compiled change): not run, per the rules.
- Runtime cost of serialisation is negligible (each project still completes in seconds).
