# Deep audit round 53: Ledger locale, whole-column recalculation, Excel regular expressions

Items left by round 52 ([round-52-functions.md](round-52-functions.md)).

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets/Formatting/SpreadsheetLocale.cs | all | the only locale was `SystemSpreadsheetLocale` (operating system culture): the root cause of item 1 |
| src/PulseOffice.Sheets/Formatting/NumberFormatter.cs | 1-60 | culture fixed at construction; Desktop built it with `CultureInfo.CurrentCulture` |
| src/PulseOffice.Sheets.Engine/Operations/ConditionalFormattingEvaluator.cs | 1-40, 300-450 | operands parsed with the system culture, fixed |
| src/PulseOffice.Sheets.Engine/Operations/SheetClipboard.cs | 1-110, 500-535 | plain-text paste parsed with the system culture, fixed (culture only, the rest belongs to the Exchange audit) |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/NumericText.cs | 1-80 | already behind `ISpreadsheetLocale`, but that locale was the system one |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/FunctionProviderRegistration.cs | all | registers `SystemSpreadsheetLocale` with `TryAdd`; Desktop now registers the interface locale first |
| src/PulseOffice.Desktop/Composition/LedgerCoreComposition.cs | 45-66 | number formatter and locale wired to the interface language |
| src/PulseOffice.Localization/TextCatalog.cs, UiLanguage.cs | 1-60 | source of the interface language |
| src/PulseOffice.Sheets.Engine/Recalculation/CalculationEngine.cs | 1-130, 160-560, 600-700, 820-840, 990-1075 | recalculation path, see performance |
| src/PulseOffice.Sheets.Engine/Recalculation/RangeDependencyIndex.cs | all | dependencies are already range-precise: an edit in an unrelated column never reaches a whole-column formula |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/ExtendedFunctions.cs | 1-215 | REGEX, its timeout and bounded replace moved into the shared `GuardedRegex` |
| src/PulseOffice.Sheets.Engine/Evaluation/FunctionInventory.cs | 1-30, 100-235 | three new names in `Required` |
| src/PulseOffice.Sheets/Workbook.cs | 45-140 | `Cleared` of a sheet was not relayed; now `SheetCleared` |
| src/PulseOffice.Sheets.UI/SheetValueWrites.cs, LedgerFillCommands.cs | parts | per-cell recalculation in loops, now batched |

## Defects

| Severity | Where | Defect | Status |
|---|---|---|---|
| Medium | SpreadsheetLocale.cs / Desktop composition | The spreadsheet locale used by VALUE, NUMBERVALUE, DOLLAR, TEXT, cell input, cell display, text to columns and CSV values was the operating system culture, not the interface language (decision 17): English interface on an Italian system displayed `1,5` while formulas used `1.5` | Fixed: `InterfaceSpreadsheetLocale` (Sheets.UI; the system culture is kept when its language is the interface language, so the region survives; otherwise it-IT, en-US, de-DE, es-ES, fr-FR) and `LocaleNumberFormatter` (one formatter per culture, follows a language change at run time) |
| Medium | ConditionalFormattingEvaluator.cs, `RuleOperands.TryNumber` | Operands parsed with the system culture | Fixed: `RuleOperandsReader(ISpreadsheetLocale)` |
| Medium | SheetClipboard.cs, `Interpret` | Plain text pasted from outside parsed with the system culture | Fixed: `ISpreadsheetLocale` |
| Minor | ArrayFunctions.cs, UNIQUE line equality | Case-insensitive comparer built on the system culture (results depended on the machine) | Fixed: invariant culture |
| Medium | CalculationEngine | With SUM/COUNT/COUNTA/SUMIFS/COUNTIFS over whole columns, filling N rows one cell at a time cost O(N²) | Fixed: `WholeColumnAggregates`, see below |
| Medium | SheetValueWrites, LedgerFillCommands | Every written cell started its own recalculation | Fixed: `CalculationEngine.Batch` collects the changed cells and recalculates once |
| Missing | Functions | Excel REGEXTEST, REGEXEXTRACT, REGEXREPLACE absent | Added |

## Whole-column aggregates

`WholeColumnAggregates` (Recalculation) recognises a formula that is exactly `SUM`, `COUNT`, `COUNTA` over whole-column ranges,
or `SUMIFS`/`COUNTIFS` over whole-column ranges with literal criteria, on one sheet. It evaluates the same function on blocks of
256 rows (through the real function, so semantics are unchanged), caches each block's partial result and adds the partials in
row order; the first error in row order wins, as in a sequential scan. A cell content change (`Workbook.CellContentChanged`,
which also fires for formula results written back) drops the block holding it; `RecalculateAll`, a cleared sheet
(`Workbook.SheetCleared`, new) and a changed formula drop the plan. COUNTIFS also evaluates the empty tail below the used area,
because a blank criterion counts empty cells. An edit now costs one block plus one addition per block. Sums are added block by
block, so the last bit of a long floating-point sum can differ from a strictly sequential sum. AVERAGEIFS is not cached: its
partials cannot be combined without re-deriving the numeric-cell count of the average range.

## Excel regular expressions

`RegularExpressionFunctions` (category Text): REGEXTEST(text, pattern, [case]), REGEXEXTRACT(text, pattern, [mode], [case]) with
mode 0 first match, 1 every match as a column, 2 the groups of the first match as a row (unmatched group empty, no group #N/A),
REGEXREPLACE(text, pattern, replacement, [occurrence], [case]) with 0 all, n the n-th, -n the n-th from the end. Case 0 sensitive,
1 insensitive, anything else #VALUE!; an invalid pattern or a timeout gives #VALUE!; no match in REGEXEXTRACT gives #N/A. They
share `GuardedRegex` with REGEX (one-second match timeout, at most 32,768 matches, replaced text capped at 32,767 characters).
XLSX writes `_xlfn.REGEXTEST`/`_xlfn.REGEXEXTRACT`/`_xlfn.REGEXREPLACE`; ODS writes `COM.MICROSOFT.REGEX*` as for the other
Excel-only functions. Localized names: it REGEX.TEST/REGEX.ESTRAI/REGEX.SOSTITUISCI, de REGEXTEST/REGEXEXTRAHIEREN/REGEXERSETZEN,
es REGEX.PRUEBA/REGEX.EXTRAER/REGEX.REEMPLAZAR, fr REGEX.TEST/REGEX.EXTRAIRE/REGEX.REMPLACER. The wizard has no per-function
descriptions for any function (it shows the category), so no new interface texts.

## Remaining

- Collation-only uses of `CurrentCulture` (sort, filter lists, sheet-name comparisons, TextPattern, Lookup sorted search, pivot
  order) are not number or date parsing; left as they are.
- `FormControlBinding.CellDisplay` and the Folio and Deck field renderers (`FieldSources`, `TextFields`, `OdtBodyWriting` table
  values) format with the system culture; Folio and Deck should follow the document language rather than the interface, a
  separate decision.
- Paste from the clipboard (`LedgerExchange` → `SheetClipboard.Paste`) still recalculates per cell through `CellContentChanged`;
  the Exchange audit owns that path and can wrap it in `CalculationEngine.Batch`.
