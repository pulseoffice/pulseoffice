# Deep audit, round 56: Ledger commands left unread

## Scope check

The old note ("round 6b left ~30 Ledger*Commands, 22 DataDialogs views, 3 Conditional files unread") is stale:
`ledger-round7.md` read all of them in full (coverage table there). The only command file never listed with full
coverage in any `docs/deep-audit/*.md` was `LedgerShapeListCommands.cs` (added in round 41). This round read it,
`Conditional/ConditionalRuleCompletion.cs`, and re-read the command files changed since round 7 (commit 1090e6a).

## Coverage

| File | Lines | Outcome |
|---|---|---|
| LedgerShapeListCommands.cs | 39/39 | fixed (availability not rechecked after the dialog) |
| Conditional/ConditionalRuleCompletion.cs | 56/56 | fixed (infinite top/bottom count accepted) |
| LedgerPivotCommands.cs | 151/151 | fixed (structure protection and source sheet not rechecked after the dialog) |
| LedgerSheetCommands.cs | 395/395 | fixed (rename, tab colour, show hidden: structure protection not rechecked after the dialog) |
| LedgerChartCommands.cs | diff since round 7 | clean (protection checked before and after the editor) |
| LedgerShapeCommands.cs | diff since round 7 | clean |
| LedgerPartialCommands.cs | diff since round 7 | clean |
| LedgerFinalCommands.cs | diff since round 7 | clean |

## Defects

### Medium

1. **Structure protection bypassed after an await.** `LedgerSheetCommands.RenameAsync`, `ColourAsync`,
   `ShowHiddenAsync` and `LedgerPivotCommands.CreateAsync` checked `RefuseStructureChange` only before the dialog;
   `LedgerProtectionGuard` does not net workbook structure, so a structure protection applied while the dialog was
   open (agent, macro) was ignored, and a sheet removed meanwhile could still be renamed or coloured. They now recheck
   the workbook, the sheet's presence and the protection after the dialog (`StillEditable`).

### Minor

2. **Shape text list after the dialog.** `LedgerShapeListCommands.BulletsAsync` applied the list without rechecking
   that the shape text was still active and the sheet unprotected.
3. **Top/bottom count "1e400".** `ConditionalRuleCompletion` accepted an infinite count (non-percent) and wrote
   the operand "∞"; non-finite counts now leave the rule incomplete.

## Tests

`tests/PulseOffice.Sheets.UI.Tests/LedgerRound56AuditTests.cs` (headless, `UiThreadSession`): tab colour and show
hidden refused when structure is protected during the dialog; infinite top count rejected.
