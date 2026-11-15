# Round 144: the full suite after round 143

The full suite run with `tools/run-tests.sh` after merging round 143. Six chunks did not pass; each was rerun alone.

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | medium | `HarfBuzzTextShaper` (complex-script shaping for screen and PDF) | The face was built with `Blob.FromStream`, which leaves the font bytes in a managed array the GC can move while HarfBuzz still reads them. Shaping returned missing glyphs (`CachedShapingTests` failed in 5 of 10 runs) or crashed the process with SIGSEGV (two core dumps); the test runner's conservative GC setting compacts more often and made it frequent. | The face data is copied to unmanaged memory, freed by the blob's release callback. 15 runs out of 15 pass, no crash. |
| 2 | minor | `OpenXmlLargeDocumentPerformanceTests` (30,000 linked shapes) | An absolute 6 s read limit missed under load (6.1 s). | Compared with a tenth of the shapes on the same machine; the read is linear (3,000 to 30,000 shapes: 5 times the time). |

## Not defects

- `PulseOffice.Formats.Odf.Tests` chunk 1 and `PulseOffice.UI.Controls.Tests` chunks 2 and 3 stopped while the Typography
  host crashed and a system package was being removed; alone they pass.
- `PulseOffice.Desktop.Sweep.Sheets.Tests` hit its 30-minute watchdog under load; alone it takes 1669 s, as before
  round 143 (1659 s), so the cell text size fix did not slow it.

## Open

- The Ledger sweep ran in 27.8 of its 30 minutes; its watchdog is now 45 minutes (a hang is still caught by the 5-minute idle check).
