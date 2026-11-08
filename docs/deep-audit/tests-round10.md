# Deep audit round 10: tests that check nothing

## Problem
Avalonia's `HeadlessUnitTestSession.Dispatch` has overloads for `Action`, `Func<TResult>` and `Func<Task<TResult>>`, but none
for `Func<Task>`. An `async () => { … }` body therefore binds to `Func<TResult>` with `TResult = Task` and returns a
`Task<Task>`: the outer task completes at the first `await`, so every assertion after it never runs and the test passes.
Round 9 worked around it with `.Unwrap()` at each call site, which leaves the trap open for every new test.

## Root fix
- `tests/Shared/UiThreadSession.cs`: `UiThreadSession` wraps `HeadlessUnitTestSession` and adds `Dispatch(Func<Task>)`, which
  awaits the inner task. Overload resolution picks it for every `async` body without a value, so a `Task<Task>` can no longer
  be produced by accident. It is linked into Desktop.Tests, Desktop.Sweep.Tests, Documents.UI.Tests and UI.Controls.Tests.
- All 54 declarations of `HeadlessUnitTestSession` in those projects now use `UiThreadSession`; the 169 `.Unwrap()` calls are gone.
- `tests/PulseOffice.UI.Controls.Tests/UiThreadSessionTests.cs` guards the wrapper: an exception thrown after an `await`
  reaches the test, and an async body's value is returned.

## Other forms searched
- `async void` in tests: none.
- `Assert.ThrowsAsync` not awaited: none.
- `Task.Run` discarded: only `WebDavTestServer` (listener loop, intentional).
- Discarded task: `FolioCommandTests.cs:251` `_ = PublishAsync(...)` on the in-memory exchange; the following paste reads what
  was published and the test passes, so it is left as is (minor).
- `Dispatch(() => SomethingAsync())` without `async`: none.

## Findings
At the starting commit every `Dispatch(async …)` (168) already carried `.Unwrap()`, so on this base no assertion had stayed
unexecuted and no product defect emerged.

## Test runs (one project at a time)
| Project | Result |
|---|---|
| PulseOffice.Desktop.Tests | 272 passed, 1 skipped |
| PulseOffice.UI.Controls.Tests | 331 passed (2 new) |
| PulseOffice.Documents.UI.Tests | 279 passed |
| PulseOffice.Desktop.Sweep.Tests | built only, not run |
