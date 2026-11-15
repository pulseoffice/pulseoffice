# Round 142: the full suite on Linux

The full suite run with `tools/run-tests.sh` (one project at a time, memory-capped chunks) after rounds 140 and 141.
Every chunk passed except two.

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | medium | `DeckShapeCodec.Read` (ODP custom shapes) | Every shape scanned the whole drawing style table for list styles. A slide with one automatic style per shape read in quadratic time: 10,000 shapes 1.1 s, 20,000 shapes 3.5 s. Found by timing each page reader and each step of the object reader. | The list source is computed once per style table (cached by the table instance). 20,000 shapes now read in about 0.55 s. |
| 2 | medium | `UiThreadSession.Dispatch(Func<Task>)` (test harness) | The overload bound to Avalonia's `Dispatch<TResult>(Func<TResult>)`, which returns the task without running the dispatcher until it completes. A continuation coming from the thread pool (async file reads) could wait forever: `TemplateSaveTests` hung in two of six runs alone and stopped the Desktop chunk in the full run. | Routed through the `Func<Task<TResult>>` overload, which runs the dispatcher until the body completes. Ten runs out of ten pass. |
| 3 | minor | `OdfLargeDocumentPerformanceTests` (20,000 shapes, grouped and not) | An absolute 4 s read limit that this machine missed under load (5.6 s), and that did not detect the quadratic read either. | The tests compare the read of 20,000 shapes with the read of 2,000 on the same machine, after a warm-up. A quadratic read fails this comparison: it was red before the fix for defect 1. |

## Open

- The areas left by round 141: conditional formatting, pivot table, filters and sort, dark theme and language switch,
  crash recovery, recent files, ODT/ODP save and PDF export, live.
- The other absolute limits in `OdfLargeDocumentPerformanceTests` and `OpenXmlLargeDocumentPerformanceTests` have
  not failed alone on this machine; they stay until one does.
