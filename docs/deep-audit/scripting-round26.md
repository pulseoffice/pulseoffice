# Deep audit round 26: scripting (remaining Ledger facades, Python provisioning, macro write access)

Area: what round 25 did not reach. Nothing committed.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Scripting.CPython/StandalonePython.cs | 236/236 | **defect 1** (origin not fixed); otherwise ok: SHA-256 from the release's `SHA256SUMS` checked before extraction, nothing runs before, download capped at the announced size, `TarFile` refuses entries and links outside the staging folder |
| Scripting.Editor/PythonDownloadConsent.cs | 58/58 | ok: modal with backdrop, decline is the cancel choice |
| Scripting.CPython/PythonDiscovery.cs | 340/340 | minor, not fixed: relative `PATH` entries are searched (see Remaining) |
| Scripting.Editor/MacroWriteAccess.cs | 65/65 | ok: grant only after the modal, decline is the default |
| Scripting.Editor/MacroWriteAccessButton.cs | 110/110 | ok: re-entrancy guarded, the fingerprint is taken at click time |
| Sheets.UI/Scripting/SheetFacade.cs | 719/719 | **defect 2** |
| Sheets.UI/Scripting/DocumentFacade.cs | 253/253 | ok: properties, sheets and names through the history; structure protection checked |
| Sheets.UI/Scripting/FilterFacade.cs | 109/109 | ok: AutoFilter permission checked, snapshots in the history |
| Sheets.UI/Scripting/FormatRuleFacade.cs | 186/186 | minor: `AddFormula` does not parse the formula (see Remaining) |
| Sheets.UI/Scripting/PivotFacade.cs | 169/169 | ok: refused on a protected sheet, checkpointed |
| Sheets.UI/Scripting/PrintFacade.cs | 197/197 | **defect 3** |
| Sheets.UI/Scripting/ScenarioFacade.cs | 64/64 | ok |
| Sheets.UI/Scripting/ValidationFacade.cs | 153/153 | ok (formula not parsed, as in FormatRuleFacade) |
| Sheets.UI/Scripting/ScriptSheetEdits.cs | 53/53 | ok |
| Sheets.UI/Scripting/ScriptProtection.cs | 51/51 | ok |
| Scripting.Editor/DocumentationPanel.cs | 192/192 | ok |
| Scripting.Editor/ScriptApiComposition.cs | 121/121 | ok |
| Agent/Tools/ScriptCatalogTools.cs | 80/173 (tool source) | ok: `app` and `files` are Shell roots and never become tools (`ToolTests` already asserts it) |
| Agent/Hosting/ModuleAgentScripts.cs | 35/35 | ok |

Deck and Folio facades (`DeckScripting.cs`, `FolioScripting.cs`) were read in full in round 25; not reread.

## Defects

### 1. Serious: the standalone Python download accepted any HTTPS origin
`StandalonePythonCatalog.Pick` and `ChecksumsAsset` (StandalonePython.cs:50, 69) took `browser_download_url` from the
release JSON and only checked the scheme. The archive and its `SHA256SUMS` were both trusted from whatever host the
JSON named, so the hash proved nothing about the origin. Fix: both addresses must be HTTPS on the default port, without
user info, on the release host under `/astral-sh/python-build-standalone/releases/download/` (`ReleaseDownloads`).
Test: `StandalonePythonCatalogTests.Downloads_from_any_origin_other_than_the_project_releases_are_refused` (verified
failing before); `StandalonePythonStoreTests` now uses the real origin, so it still reaches the checksum check.

### 2. Minor: macro cell texts, replacements and comments without a cap
`SheetFacade.Write`, `WriteRange`, `Replace` and `Comment` (SheetFacade.cs:72, 139, 389, 420) stored any length; a cell
holds at most 32,767 characters (`ResultLimits.MaximumTextLength`, the limit formulas already use). Fix: refused with an
`ArgumentException`; `Replace` now computes every result first and changes nothing when one does not fit.
Test: `ScriptFacadeTests.Texts_longer_than_a_cell_holds_are_refused_and_change_nothing` (verified failing before).

### 3. Minor: page margins and repeated rows beyond what the dialog allows
`PrintFacade.Margins` (PrintFacade.cs:52) accepted any positive millimetres (1e9 overflows the twip value, 150 mm
leaves no printable area on A4); `RepeatRows` accepted up to 1,048,576. Fix: margins must leave at least 10 mm of
printable page on the shorter side, as the page setup dialog does; repeated rows 0-100, the dialog's range.
Test: contract cases `page.Margins` and `page.RepeatRows` in `LedgerFacadeContractTests` (verified failing before).

## Checked and sound
- Every changing member of the Ledger facades goes through the read-only guarded dispatcher and pushes an undo step;
  protection is checked before the change (cells, format, AutoFilter, structure, unprotected-only for pivots,
  scenarios, validation). None of them changes state outside the history.
- The macro write grant is given only by the editor's modal (decline is the default) and bound to the fingerprint.
- The agent never sees `files` tools: Shell roots are not turned into tools. Round 25's remaining item was wrong.

## Remaining
- `FormatRuleFacade.AddFormula` and `ValidationFacade.AddFormula` do not parse the formula (minor).
- `ExecutableSearchPath` (PythonDiscovery.cs:80) also searches relative `PATH` entries such as `.`; CPython is located
  only after an explicit Run in the editor. Not fixed: the type is internal and has no test seam.
- `LedgerRound7AuditTests.IlModuloDatiNonScriveUnNuovoRecordSopraUnaRigaOccupata` failed once and passed on two reruns
  (the same intermittent failure round 25 saw).

## Tests run
`PulseOffice.Sheets.UI.Tests` 421/421 (one intermittent failure on the first run, see above),
`PulseOffice.Scripting.Tests` 147/147, `PulseOffice.Architecture.Tests` 38/38.

## To try live
- Macro editor, Ledger: `sheet.Write("A1", "x".repeat(40000))` gives an error and A1 stays empty.
- `page.Margins(150)` on A4 gives an error; `page.Margins(20)` works and undo reverts it.
- Python download (macro editor, CPython without a system Python): the offer names the release host and the install works.
