# Deep audit round 40: old list of open items re-checked

Each item of an older list was first checked against the code and the audit logs.

| # | Item | Status | Where |
|---|---|---|---|
| 1 | Ledger `SheetLayout` and `MergedRegions` as intervals | `SheetLayout`: already done (sheets-round2.md §1, `RunLengthMap.cs`). `MergedRegions`: **fixed now** | see below |
| 2 | Protection guard for async commands after the first `await` | already done: `CommandExecutor.ExecuteAsync` awaits `command.Execute` before `checks.Finish()` (shell-round2.md group 1); **test added now** | `EffectGuardChannelTests` |
| 3 | `DeckSnapshotProvider` equivalent to Ledger's | already done | `src/PulseOffice.Presentations.UI/DeckSnapshotProvider.cs`, wired in `ShellWindowComposition.cs`, write outcome checked (deck-round2.md), `DeckSnapshotProviderTests` |
| 4 | Desktop tests with un-awaited `Session.Dispatch(async …)` | already done | shell-round2.md group 2, then tests-round10.md: `tests/Shared/UiThreadSession.cs` adds `Dispatch(Func<Task>)` |
| 5 | "Default" language in Folio's Character dialog | already done | shell-round2.md group 3, `FolioFormattingDialogTests.LaLinguaPredefinitaToglieLaLinguaDelTesto` |
| 6 | Ledger ⌘Space taken by macOS | already done: "Select row" is Shift+Space; the registry is the shared shortcut table | shell-round2.md group 4, `LedgerSelectionShortcutTests` |
| 7 | 512 MB thread stack for multiple operations | already done: no dedicated thread, topological preparation | sheets-round2.md §4, `SubstitutionContextTests.ALongChainEvaluatesOnASmallStack` |
| 8 | Sheet index in the ChangeLog | already done | sheets-round2.md §3, `ChangeLogSheetIndexTests`, `TrackedChangesStructureTests` |
| 9 | SUBTOTAL recalculation when hiding/unhiding rows | already done | sheets-round2.md §2, `SubtotalVisibilityRecalculationTests` |

## Item 1: MergedRegions

Read in full: `src/PulseOffice.Sheets/MergedRegions.cs` (140/140), `tests/PulseOffice.Sheets.Tests/MergedRegionsIndexTests.cs`.

- **Medium (performance)** `MergedRegions.cs:39` — the spatial index put every region into each 64-row band it spans: a
  merge over whole columns took 16,384 band entries, and so did its removal; a thousand whole-column merges were 16 million
  list entries. Fixed: regions taller than a band threshold go into a separate list of tall regions, scanned on every
  lookup (there are few of them by nature), while short regions keep the band index. Test
  `MilleUnioniSuColonneIntereNonOccupanoUnaVocePerBlocco` (fails before on time).

## Item 2: async commands

`CommandExecutor.ExecuteAsync` opens the effect checks, awaits the whole command and only then calls `Finish`, so changes
made after the first `await` are inside the check. New test
`Un_comando_asincrono_che_scrive_dopo_il_primo_await_in_una_sezione_protetta_si_rifiuta` documents it. Remaining
(declared in shell-round2.md group 6): fire-and-forget paths outside the executor must check at the source.
