# Round 222: test run time

Starting point: the join run of 2026-11-18 (`artifacts/test-runs/20261118-130822`) listed 111 tests over 5 s, 84 of
them command-sweep slices (Deck shape text up to 27 s, Ledger typical up to 20 s). No build or test was run in this
round; the coordinator measures after the join.

## Command sweeps

- Slice counts are no longer fixed (twelve for the large contexts, four for the others). `RecordedSeconds` in
  `tests/PulseOffice.Desktop.Sweep.Shared/CommandSweepTests.cs` holds the summed slice time of every module and
  context from the join run; `PartsOf` turns it into `ceil(seconds * 1.25 / 6)` slices (headroom of 25 % for new
  commands, 6 s of work per slice), four for a context with no record. Resulting slices: Ledger typical 39,
  protected sheet 24, read-only 10, shape text 8, object 5, cell edit 2; Folio typical 29, protected section 20,
  read-only 7, object 4; Deck typical 21, shape text 21, read-only 5, object 5, presenting 1 (about 200 in all,
  from 92).
- Shards go from six to ten classes (`CommandSweepShard1..10`), so each chunk carries about 50 s instead of 70 s.
- The table is refreshed from the summed `whole` times of the `*.timings.tsv` files (`PULSEOFFICE_SWEEP_OUT`) or the
  xUnit result times when commands grow.
- Harness: the remaining per-command costs (fingerprint with enabled state of every command, frame hash, content
  fingerprint for undo and redo) are each a check named in decision 19, and the restore path already reuses the
  settled frame; none was cut, so no check is weaker.
- Expected effect: a slice near 6 s of work, at most about 11 s for the first slice of a process (JIT warm-up of
  about 5 s); the per-slice set-up (composition and prepare, about 1-1.5 s) adds roughly 45 s of wall time over five
  parallel jobs.

## Other slow tests

- `LargeWorkbookRoundTripTests.Many_cells_cross_xlsx_and_ods_in_linear_time`: 200 and 2,000 rows instead of 500 and
  5,000; still a tenfold size step under the same 40x bound. Expected about 3.5 s instead of 8.5 s.
- `PptxMalformedInputTests` and `XlsxMalformedInputTests`: the package entries are decompressed once per test instead
  of once per mutated copy; the XLSX garbage-attribute theory runs its attributes four at a time like the PPTX one
  (a fresh reader per read). Expected roughly half the time for the `styles.xml` and `slide1.xml` cases.
- Left as they are: the DOCX and HTML table-cap tests must build more cells than the constant caps of
  `ImportedTableLimits` (one and two million) and are already at the minimum above them; making them cheaper needs
  injectable caps in the readers. The Desktop exchange tests (5-10 s) are dominated by opening the composed suite and
  crossing every module; without per-phase timings they were not changed blind. The Folio 500-page and memory tests
  prove a property of their size.
