# Deep audit round 52: database functions, INFO, REGEX, locale-dependent text functions, whole-column performance

Continues rounds 49 and 51 ("Not covered"). Method: documented Excel/OpenFormula results evaluated on the real engine,
fixes at the root, tests in `tests/PulseOffice.Sheets.Engine.Tests/DeepAuditLedgerFunctionsRound52Tests.cs` and
`WholeColumnMillionRowPerformanceTests.cs` (serial collection).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/DatabaseFunctions.cs | 1-198 | blank criteria row matched nothing, fixed |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/CriteriaMatcher.cs | 1-111 | correct |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/TextPattern.cs | 1-76 | correct (1 s regex timeout, non-backtracking option) |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/NumericText.cs | 1-81 | no thousands separators in VALUE, fixed |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/ExtendedFunctions.cs | REGEX | correct, catastrophic patterns end on the timeout with `#VALUE!` |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/ReferenceFunctions.cs | CELL, INFO | INFO texts, fixed |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/MiscellaneousFunctions.cs | DOLLAR, NUMBERVALUE | operating-system culture instead of the interface language, fixed |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/TextFunctions.cs | VALUE, FIXED | VALUE fixed; FIXED correct |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/CompatibilityFunctions.cs | TEXT | localized date tokens ignored, fixed |
| src/PulseOffice.Sheets.Engine/Recalculation/CalculationEngine.cs | 477-700 | recalculation path, see performance |

## Defects

| Severity | Where | Defect | Status |
|---|---|---|---|
| Medium | DatabaseFunctions.cs, criteria compilation | A criteria row left entirely blank matched no record; Excel documents that a blank criteria row matches every record | Fixed |
| Medium | MiscellaneousFunctions.cs, DOLLAR and NUMBERVALUE | Used `CultureInfo.CurrentCulture` (operating system) instead of the spreadsheet locale: in an English interface on an Italian system DOLLAR gave `1.234,57 €` and NUMBERVALUE read `1.234,5` | Fixed: `NumericText.Culture` |
| Medium | NumericText.cs, VALUE | Thousands separators were refused (`VALUE("1,234.5")` in English, `VALUE("1.234,5")` in Italian, `VALUE("1 234,5")` in French gave `#VALUE!`) | Fixed: `TryReadGrouped`, strict groups of three digits, spaces accepted when the group separator is a space; the invariant locale stays strict; plain coercion and text to columns unchanged |
| Medium | CompatibilityFunctions.cs, TEXT | Date tokens of the interface language were not understood (`gg/mm/aaaa`, `TT.MM.JJJJ`, `jj/mm/aaaa`, `dd/mm/aaaa`) | Fixed: `ILocalizedFormatTokens` (it, de, fr, es letter tables as data); a section that already uses the canonical `y` is left alone, quoted text, brackets, escapes, `AM/PM`, `A/P`, `General`, `Standard` are never translated |
| Minor | ReferenceFunctions.cs, INFO | `INFO("recalc")` returned the Italian `Automatico`; `INFO("origin")` lacked the documented `$A:` prefix | Fixed: `Automatic`, `$A:$A$1` |

## Checked with no defect

DSUM, DAVERAGE, DCOUNT (with and without field), DCOUNTA, DMAX, DMIN, DPRODUCT, DSTDEV, DSTDEVP, DVAR, DVARP on the
documented orchard example (OR rows, AND columns, two conditions on the same field), computed criteria (header that is not a
field, formula relative to the first record), wildcards `?ear` and `Ch*`, DGET with several matches → `#NUM!`. REGEX with
`^(a+)+$` on 40 `a` plus `!` ends within the timeout. FIXED in five languages.

## Performance

SUMIFS, COUNTIFS (two pairs), XLOOKUP (found and not found), FILTER and SUMPRODUCT over whole columns with 1,000,000 filled
rows: each evaluation is linear (the six cases including filling the sheet take about 3 s each). A recalculation after a
single edit re-evaluates every whole-column dependent over the used area, so filling N rows one `SetValue` at a time with
such a formula present costs O(N²) (1,000 rows 0.36 s, 4,000 rows 5.7 s). This is the same model as Excel (each edit
recalculates its dependents); bulk operations must go through `RecalculateFrom` with the whole set of changed cells.
Not changed; noted as a candidate for incremental conditional aggregates.

## Decisions

- `LEN` keeps counting code points. OpenFormula defines LEN as the number of characters, and MID, LEFT, RIGHT, FIND and
  SEARCH positions in Ledger are code points too: changing LEN alone to UTF-16 units would make `MID(t,1,LEN(t))`
  inconsistent. The difference with Excel shows only for characters outside the BMP and stays declared.
- Database text criteria without an operator match the whole cell (ODF `search-criteria-must-apply-to-whole-cell`, default
  true); Excel matches a prefix. Declared difference.
- `INFO("recalc")` always says `Automatic`: the evaluation context does not expose `CalculationEngine.AutomaticCalculation`.
- Excel's REGEXTEST, REGEXEXTRACT and REGEXREPLACE are not present (only the OpenFormula REGEX): not in scope here.
- `ConditionalFormattingEvaluator` (line 351) and `SheetClipboard` (line 528) still parse with `CultureInfo.CurrentCulture`;
  outside this area, reported.
