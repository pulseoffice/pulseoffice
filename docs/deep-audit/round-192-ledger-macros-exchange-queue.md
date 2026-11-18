# Round 192: Ledger macros, script time cap, Exchange and the spreadsheet spool (queued items)

Six items from the audit queue (rounds 177, 180, 185 and 188), fixed in one round. Nothing was built or run in this
round: the coordinator builds and tests after the join.

## Fixed

1. **Pivot dialog preview in dark mode** (177). `PivotPromptView` drew the preview grid with fixed white, black and
   light grey and green brushes. Cells now take `PulseSurface`, `PulseBorder` and `PulseTextPrimary`, the header row
   `PulseAccentSubtle`, the frame `PulseBorderStrong`. Test: `Round192PivotPreviewThemeTests` (no fixed colours left in
   the file).
2. **Find and Replace case in the macro API** (185). Ledger `sheet.Find` ignored case and `sheet.Replace` matched it;
   Folio's `Find` and `Replace` did the same. All four now take an explicit last parameter `matchCase`, false by default
   in both modules, which is also the default of the find dialog: with the defaults Find counts exactly what Replace
   changes. Folio's `Replace` therefore now ignores case unless asked; that is a behaviour change for macros that relied
   on exact matching, accepted for consistency. The help texts (`api.sheet.Find/Replace.summary`,
   `api.textdocument.Find/Replace.summary`, the new `api.param.matchCase`) say so in the five languages. Tests:
   `Round192LedgerMacroTests`, and the Find and Replace cases of `FolioFacadeContractTests`.
3. **`sheet.Freeze` on a read-only workbook** (185). Chosen: it is **view state**, not refused. The Freeze Panes
   commands are enabled on read-only workbooks and add no undo step, and Folio and Deck run their view calls (select,
   go to) through the same `Show` channel of the script dispatcher, which the read-only guard does not refuse. Refusing
   only the macro would make the API stricter than the interface. The help text of `api.sheet.Freeze.summary` says it
   in the five languages. Test: `Round192LedgerMacroTests`.
4. **Script time cap inside a host call** (185). `PermissionScopedScriptEngine` now enters the run with a cancellation
   token that fires at the time cap (`IScriptExecutionPermissions.Enter(permissions, cancellation)`, exposed as
   `Cancellation`). The new `CancellableScriptDispatcher` (wired in the desktop composition around the read-only
   guard) refuses a host call once the token has fired, and checks it again when the call reaches the UI thread, so a
   call queued before the cap does not start after it. When an engine gives up at its hard limit while its thread is
   still inside a host call, `ScriptResult.Stopped` is the task of that thread; `ModuleAgentScripts` awaits it before
   returning, so the agent's undo step closes only after the script has really stopped. Tests:
   `Round192HostCallCancellationTests`, `Round192ScriptStopTests` (the engine case waits for the two-second hard
   limit).
5. **Exchange of charts with error bars and pivot links, groups and drag and drop** (188). Writing the tests through
   the shared `ExchangeSuite` found two defects in `LedgerExchange`:
   - custom error bars whose values are a cell reference left their cached values empty in the neutral content, so the
     chart pasted into Folio or Deck drew no bars. The portable chart now reads them through `IChartErrorValueRefresh`
     (now registered in the container) before the inline data is taken;
   - a pivot chart kept its pivot link everywhere: in Folio and Deck it named a pivot that does not exist there, and
     pasted into Ledger it repeated the pivot format id of the original. The neutral content now has no pivot link; a
     native Ledger paste keeps it only when the target workbook has a pivot of that name, with the next free format id.
   Groups dropped from Deck into Ledger and Folio, and a refused drop on a read-only document, are covered too.
   Test: `Round192ExchangeTests` (one suite for the whole class).
6. **Spool of very large spreadsheet bodies** (180). `OverflowContentSpool` (Formats.Odf) keeps the ODS body in memory
   up to 64 MB and then moves it to a stream from the new `IPrivateSpool` of `PulseOffice.Platform`: `PrivateFileSpool`
   creates a delete-on-close file in the session's private temporary folder (`ITemporaryFiles`), owner-only on Unix
   at creation and restricted through `IPrivateFiles` on every system. The desktop composition gives it to the three
   ODS writers. Tests: `Round192ContentSpoolTests`, `Round192PrivateSpoolTests`.

## Open items

- The Windows side of `PrivateFileSpool` (access control set on an open delete-on-close file) has not run on a real
  Windows.
- Items 2 and 3 share one commit.
