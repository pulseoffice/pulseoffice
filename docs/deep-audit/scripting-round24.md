# Deep audit round 24: macros and scripting (security)

Area: `src/PulseOffice.Scripting*`, macro file-write permission (decision 13), macro event gate. Nothing committed.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Scripting/MacroFiles.cs | 123/123 | ok: case-aware prefix check per system, `..` refused, hidden names refused, extension allow-list, directory and file links refused, temp file + move on write |
| Scripting/MacroWritePermission.cs | 147/147 | **defect 1** |
| Scripting/ScriptExecutionPermissions.cs | 69/69 | ok: AsyncLocal scope, sandboxed by default |
| Scripting/PermissionScopedScriptEngine.cs | 30/30 | ok |
| Scripting.JavaScript/JavaScriptEngine.cs | 206/206 | ok: no CLR access, string compilation off, recursion, memory and time limits, allocation guard (covered by `JavaScriptSandboxTests`) |
| Scripting.Python/IronPythonScriptEngine.cs | 180/180 | ok: refused unless `AllowsUnsandboxedEngines` (declared: not a sandbox) |
| UI.Controls/Macros/DocumentMacroGate.cs | 135/135 | ok: a broken signature blocks, an unsigned macro asks according to the level |
| Sheets.UI/FormEventRunner.cs | 75/75 | ok: event macros pass the gate before running |

Not reached in this round: the CPython engine files, `ScriptApiCatalog` and the module facades, the macro IDE, and the
Deck and Folio event sources.

## Defects

### 1. Medium: different code could share the fingerprint of a granted macro
`Sha256MacroFingerprints.Of` (MacroWritePermission.cs:20) normalised line endings with `ReplaceLineEndings`, which also
folds U+0085, U+2028, U+2029 and form feed into `\n`. Those characters are not line breaks inside JavaScript template
literals or inside Python strings and comments. So two programs that behave differently (for example a `# note<NEL>call()`
comment versus the same text with a real newline, or a different file name in a template literal) had the same
identity, and the file-write grant survived a change to the code. Fix: only CRLF and a lone CR, which are line breaks
in both languages, are folded. Grants for code that contains the other characters lapse once, which is the safe direction.
Tests: `MacroWritePermissionTests.Code_that_differs_only_in_characters_the_languages_do_not_treat_as_line_breaks_has_its_own_fingerprint`
(three cases, verified failing before the fix) and `Windows_and_classic_line_endings_keep_the_fingerprint`.

## Remaining
- Minor: `AppendText` writes through a hard link that is already in the macro folder, because `LinkTarget` does not see
  hard links. Planting one needs local access, so it is not fixed.

## Tests run
`PulseOffice.Scripting.Tests`: 141/141.

## To try live
Grant file writing to a macro, then change only its line endings (CRLF or LF): the grant stays. Any other change,
including invisible separators, revokes it.
