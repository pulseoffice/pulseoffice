# Round 111: intermittent exchange-matrix failures in Desktop.Tests

## Symptom
In two full runs of `tests/PulseOffice.Desktop.Tests` one exchange-matrix case failed in under 1 ms, a different
one each time (`ExchangeTests...(formula, Presentations, Sheets)`, `ExchangeMatrixTests...(control, Presentations)`);
alone they pass.

## Reproduction attempts
Two full runs of the project before the fix (detailed console logger): 344 passed, 1 skipped, no failure. The failure
did not reproduce on an idle machine, so the cause was found by analysis.

## Cause (medium, production)
Every exchange test builds a full service provider whose user settings live under a per-test
`PULSEOFFICE_DATA_HOME`; its `Dispose` clears the variable and deletes that folder recursively. `JsonSettingsStore`
debounces writes with a `System.Threading.Timer` (600 ms). `Dispose` disposed the timer and flushed, but
`Timer.Dispose()` does not wait for a callback already queued on the thread pool: that callback ran `Flush()` after
disposal, recreating the configuration folder and writing `settings.json(.tmp)` while the test was deleting it. The
recursive delete then fails ("directory not empty"), or the folder is left behind; the failure lands on whichever
test is disposing, and since the test body had already passed it is reported with a tiny duration. Under load (other
agents building) the window between timer expiry and disposal is wide enough to hit. For users the same race means a
settings write after shutdown, possibly after another instance or the user changed the file.

## Fix
- `JsonSettingsStore` takes a `TimeProvider` (registered `TimeProvider.System` in `SettingsComposition`) and creates
  its timer through it.
- The timer callback goes through `FlushScheduled`, which takes the store lock and does nothing once disposed;
  `Dispose` still performs the final flush under the same lock, so a write in progress finishes before `Dispose`
  returns and none starts after it.

## Test
`JsonSettingsStoreTests.A_scheduled_write_that_fires_after_disposal_touches_nothing`: a `TimeProvider` that holds the
callback, fired after `Dispose` and after deleting the folder; the folder must not reappear. Fails before the fix
(verified), passes after.

## Runs
Before: 2 full runs of Desktop.Tests, 344/345 passed (1 skipped), no failure reproduced. After: Settings.Tests 31/31;
1 full run of Desktop.Tests, 344/345 passed (1 skipped).

## Files
- `src/PulseOffice.Settings/JsonSettingsStore.cs`
- `src/PulseOffice.Desktop/Composition/SettingsComposition.cs`
- `tests/PulseOffice.Settings.Tests/JsonSettingsStoreTests.cs`
