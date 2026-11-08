# Deep audit: Ledger's engine (sheets-engine)

## Coverage (Evaluation/Functions/Providers and Numerics)

| File | Lines read/total | Outcome |
|---|---|---|
| Providers/ArithmeticFunctions.cs | 62/62 | GCD/LCM: integers beyond 2^53 and an overflowing LCM silently gave wrong results (serious) — fixed |
| Providers/CombinatoricsFunctions.cs | 40/40 | no defects |
| Providers/TextFunctions.cs | 243/243 | VALUE of negatives in parentheses (medium) — fixed; CODE returns the UTF-16 unit, not Excel's code page code (minor, open) |
| Providers/RoundingFunctions.cs | 116/116 | ROUNDUP/ROUNDDOWN/TRUNC with huge digit counts gave 0 (serious) — fixed |
| Providers/InformationFunctions.cs | 69/69 | ISEVEN/ISODD on values beyond long saturate (minor, open) |
| Providers/EngineeringFunctions.cs | 252/252 | IMARGUMENT(0) → #NUM! (medium) — fixed; Bessel with huge orders (serious, in BesselFunctions) — fixed |
| Providers/ArrayFunctions.cs | 729/729 | SEQUENCE/MUNIT/RANDARRAY/EXPAND/WRAPROWS without a size cap (serious) — fixed; integer bounds of RANDARRAY (minor) — fixed; SORT accepts orders other than ±1 (minor, open) |
| Providers/RandomFunctions.cs | 49/49 | RANDBETWEEN truncated the bounds instead of rounding them up (minor) — fixed |
| Providers/CompatibilityFunctions.cs | 259/259 | cumulative NEGBINOM.DIST with a loop as long as the failures (serious) — fixed; RANDBETWEEN.NV (minor) — fixed |
| Providers/ConditionalFunctions.cs | 143/143 | SUMIF/SUMIFS/AVERAGEIF(S)/MAXIFS/MINIFS ignored the errors of the selected cells (medium) — fixed |
| Providers/DatabaseFunctions.cs | 159/159 | no defects; headers compared with the current culture (minor, open) |
| Providers/DateTimeFunctions.cs | 304/304 | TIME overflows with many hours (medium, in DateSystem) — fixed; DATE(100–1899) follows Excel (year+1900), the reference suite gives #VALUE! (a choice, not a defect) |
| Providers/DistributionFunctions.cs | 415/415 | cumulative BINOM.DIST/POISSON/CRITBINOM with loops as long as the arguments (serious) — fixed; BINOM.DIST mass beyond ~1000 trials gave #NUM! (medium) — fixed; cumulative HYPGEOM.DIST and NEGBINOMDIST mass with huge arguments (minor, open) |
| Numerics/SpreadsheetMath.cs | 389/389 | incomplete beta and gamma stopped at 300 iterations: CHISQDIST(100000;100000) and GAMMA.DIST silently wrong (serious) — fixed (cap 100,000); `Combinations` looped over k instead of min(k, n−k) (medium) — fixed |
| Numerics/BesselFunctions.cs | 183/183 | BESSELJ/Y/K with huge orders: loops of billions of steps (serious) — now #NUM! immediately like the reference suite; BESSELJ with argument beyond 5e6 uses the asymptotic form |
| Providers/ExoticFinanceFunctions.cs | 148/148 | no defects |
| Providers/FinancialFunctions.cs | 361/361 | CUMIPMT/CUMPRINC with one loop per period (79 s for 2e9 periods, serious) — now closed form by telescoping, verified against the reference suite also for type 1; ISPMT with zero periods gives #NUM! instead of #DIV/0!, XNPV accepts dates before the first (minor, open) |
| Providers/ForecastFunctions.cs | 130/130 | no defects |
| Providers/IntegrationFunctions.cs | 272/272 | FILTERXML expanded the entities of an internal DTD (expansion bomb, medium, security) — fixed with `DtdProcessing.Prohibit` |
| Providers/InverseDistributionFunctions.cs | 245/245 | Z.TEST with deviation ≤ 0 gives #DIV/0! instead of #NUM! (minor, open) |
| Numerics/DistributionInverse.cs | 74/74 | no defects; with the new cap on continued fractions an inverse with huge parameters stays bounded (≤ 2000 bisections) |
| Providers/ExtendedFunctions.cs | 249/249 | B() with a loop over the range and Combinations at every step (serious) — fixed with `BinomialDistribution.Range` |
| Numerics/DiscreteFourier.cs | 45→80 | FOURIER O(n²): a whole column froze the calculation (serious) — now Bluestein O(n log n) beyond 64 samples; removed `Transform`, never used, which overflowed in `k*t` beyond 46341 samples |
| Numerics/BinomialDistribution.cs | new | binomial mass, cumulative and range shared by BINOM.DIST, CRITBINOM and B |
| Providers/LogicalFunctions.cs | 156/156 | no defects |
| Providers/LookupFunctions.cs | 387/387 | no defects; CHOOSE out of range gives #NUM! (the reference suite's Err:502), Excel gives #VALUE! (choice already made) |
| Providers/MiscellaneousFunctions.cs | 174/174 | DOLLAR with very negative decimals wrote «NaN» (minor) — fixed; DOLLAR uses the process culture instead of the sheet language, NUMBERVALUE("") gives #VALUE! instead of 0 (minor, open) |
| Providers/ModernLookupFunctions.cs | 109/109 | XLOOKUP/XMATCH accepted non-existent match and search modes (minor) — now #VALUE! like Excel |
| Providers/NumberSystemFunctions.cs | 75/75 | BASE with an uncapped minimum length: BASE(5;2;1E+9) allocated 2 GB (serious) — now #NUM! from 65535 like the reference suite |
| Numerics/RadixConverter.cs | 33/33 | no defects |
| Numerics/RomanNumerals.cs | 86/86 | no defects; ARABIC accepts non-canonical forms («IIII») like the reference suite |
| Providers/ReferenceFunctions.cs | 169/169 | no defects; CELL("address") without the sheet name (minor, open) |
| Providers/RegressionFunctions.cs | 216/216 | CORREL/PEARSON/RSQ with a constant first series gave 0 instead of #DIV/0! (medium) — fixed |
| Numerics/LinearRegression.cs | 87/87 | undefined correlation now NaN instead of 0 |
| Numerics/PercentRanking.cs | 23/23 | no defects (PERCENTRANK with 400 digits gives #NUM! like the reference suite) |
| Providers/SecuritiesFunctions.cs | 528/528 | DB with one loop per period (8 s for 1E+9 periods, medium) — closed form; AMORDEGRC with depreciation rounded to zero ran up to the requested period (medium) — exits immediately |
| Providers/StatisticsFunctions.cs | 98/98 | LARGE/SMALL truncated k; the reference suite rounds LARGE up and SMALL down (minor) — fixed |
| Providers/SummaryFunctions.cs | 134/134 | SUBTOTAL and AGGREGATE (options 0–3) counted nested SUBTOTAL/AGGREGATE: the grand total of the «Subtotals» command doubled (serious) — fixed; hidden or filtered rows are not excluded because the evaluation context does not expose them (medium, open) |
| Providers/TextualAggregateFunctions.cs | 51/51 | no defects |
| Providers/TrigonometryFunctions.cs | 43/43 | no defects (ATAN2(0;0)=0 and COT(0)=#NUM! like the reference suite) |
| Numerics/ComplexNotation.cs | 77/77 | no defects; non-finite results now stop in EngineeringFunctions (IMPOWER("0";-1) gave "0", IMPRODUCT/IMSUB beyond the range wrote «∞», medium — fixed) |
| Numerics/DayCount.cs | 107/107 | no defects |
| Numerics/HolidayCalendar.cs | 18/18 | no defects |
| Numerics/MatrixAlgebra.cs | 103/103 | absolute singularity threshold 1e-12: MDETERM/MINVERSE of matrices with small elements gave 0 and #NUM! (medium) — now relative to the largest element like the reference suite; MINVERSE O(n³) without a cap on huge matrices (minor, open) |
| Numerics/FinancialMath.cs | 253/253 | DDB went through `DecliningBalance`, one loop per period that with a fractional period returned 0 (DDB(2400;300;10;2,5) = 0 instead of 343.46) and with 1E+9 periods ran 8 s (serious) — now the closed form `DoubleDecliningPeriod`, the same as the reference suite's; DDB with salvage above cost gives #NUM!; removed `DecliningBalance`. VDB keeps the reference suite's loops (slow beyond ~1E+8 periods, minor, open) |
| Numerics/ExponentialSmoothing.cs | 472/472 | FORECAST.ETS.PI: the seasonal simulation allocated steps × 1000 doubles (100,000 steps = 800 MB and 18 s, 1E+7 steps = 80 GB) and the analytical one a vector as long as the steps (serious) — the analytical one computes only the two steps needed, the seasonal one stops with #NUM! beyond 10,000 steps (the reference suite allocates the same way); `FillGaps` inserts into a list (quadratic on huge series, minor, open) |
| Numerics/LeastSquares.cs | 108/108 | no defects (Householder with relative threshold) |
| Numerics/OddCouponMath.cs | 85/85 | no defects |
| Numerics/OptionPricing.cs | 85/85 | no defects |
| Numerics/ThaiBahtText.cs | 53/53 | BAHTTEXT beyond ~9.2e16 saturates the `long` and writes a wrong amount without error (minor, open) |
| Numerics/UnitConversion.cs | 278/278 | «shweight» and «hweight» appear twice with different factors: the first, less precise, wins (minor, open) |
| Numerics/DateSystem.cs | 50/50 | `TimeToFraction` in integer arithmetic: TIME(1193047;0;0) gave 0.022 instead of 7:00 — fixed. The 1904 system is converted when reading XLSX (Xlsx1904DateShift) |

## Coverage (core: Parsing, Recalculation, Operations, Evaluation, Localization)

| File | Lines read | Outcome |
|---|---|---|
| Parsing/Parser.cs | 364/364 | no nesting limit: `=((((…1))))`, `=----…1`, `=ABS(ABS(…))` and long `1+1+…` chains crashed the process with a stack overflow (blocking) — now `MaxDepth` (2048, with cost 8 per parenthesis or function level) and a syntax error |
| Parsing/Lexer.cs | 244/244 | whole rows and columns of a sheet with an unquoted Unicode name (`Città.A:A`) were not read back (medium) — Unicode letter class in the expression |
| Parsing/FormulaPrinting.cs | 244/244 | no defects |
| Parsing/Ast.cs | 137/137 | no defects |
| Parsing/SheetNameQuoting.cs | 18/18 | no defects (see Lexer) |
| Recalculation/DependencyGraph.cs | 201/201 | recursive Tarjan: a column of 100,000 chained formulas crashed the recalculation with a stack overflow (blocking) — now iterative; `DirectDependents` walked all formulas with ranges and `PendingPrecedents` the whole pending set: quadratic recalculation, 20,000 `SUM` = 33 s (serious) — now `RangeDependencyIndex` (buckets of 64 columns × 1024 rows, whole columns and rows in dedicated buckets) and in-degrees counted from the dependents; `DirectPrecedents` enumerates every cell of the range (slow with `A:A` in tracing, minor, open) |
| Recalculation/CalculationEngine.cs | 1078/1078 | `DependencyCollector.VisitName` expanded mutually referencing names without a guard: stack overflow (blocking) — now a set of names being expanded; new `IEvaluationContext.RowVisibilityAt` (see SUBTOTAL); `SUBTOTAL` and `AGGREGATE` volatile; `GetRangeValues` produces an empty value for every row beyond the used area (minor, open) |
| Recalculation/CompactCellSet.cs | 56/56 | no defects |
| Recalculation/CalculationEngineFactory.cs | 18/18 | no defects |
| Recalculation/ExternalAccess.cs | 16/16 | no defects |

Defects of this round:
- Blocking — Parser without a nesting limit (stack overflow, process terminated).
- Blocking — recursive DependencyGraph.FindCircular on long chains.
- Blocking — mutually recursive defined names in DependencyCollector.
- Serious — quadratic recalculation with many range formulas.
- Medium (open from the previous round, closed) — SUBTOTAL/AGGREGATE did not exclude hidden or filtered rows: now `IEvaluationContext.RowVisibilityAt` distinguishes `Hidden` and `Filtered` (row hidden inside an autofilter with criteria); SUBTOTAL 1–11 skips the filtered ones, 101–111 also the hidden ones, AGGREGATE with odd options skips all hidden ones, like the reference suite. Left outside the area: hiding a row from the interface does not trigger a recalculation (the next change does, because the two functions are now volatile).
- Medium — whole rows and columns on sheets with a Unicode name.

## Defects

- Serious — RoundingFunctions.cs `Truncate`: infinite `10^digits` → 0. Now the value stays if the digits exceed the precision.
- Serious — ArithmeticFunctions.cs `Fold` / SpreadsheetMath `LeastCommonMultiple`: silent overflow; now #NUM! beyond 2^53 like Excel.
- Medium — TextFunctions.cs VALUE("(12)") = -12 like Excel and the reference suite (updated the old test that said the opposite).
- Medium — EngineeringFunctions.cs IMARGUMENT("0") = #NUM!.
- Serious — ArrayFunctions.cs SEQUENCE/MUNIT/RANDARRAY/EXPAND/WRAPROWS: no cap, `SEQUENCE(100000;100000)` exhausted memory. Now #NUM! beyond the sheet grid or 2^24 cells (the reference suite: Err:538 beyond ~85 million elements; our element weighs more than theirs).
- Serious — CompatibilityFunctions.cs cumulative NEGBINOM.DIST: loop of `failures` iterations (1E+12 = hours); now regularized incomplete beta I_p(s, f+1), verified against the reference suite (3;5;0,5 → 0.36328125).
- Medium — ConditionalFunctions.cs `SelectedNumbers`: error cells chosen by the criterion were ignored; the reference suite gives the error (verified headless).
- Medium — DateSystem.cs `TimeToFraction`: integer overflow of `hours*3600`; now in double like the reference suite.
- Minor — RANDBETWEEN, RANDBETWEEN.NV and integer RANDARRAY: bounds truncated, now rounded up like the reference suite (RANDBETWEEN(0,5;0,7) = 1).
- Serious — DistributionFunctions.cs cumulatives of BINOM.DIST, POISSON and CRITBINOM: O(k) and O(k²) loops; now incomplete beta/gamma, binary search and normal with continuity correction beyond variance 1e8 (the reference suite itself gives 0.50006 for BINOMDIST(5E+11;1E+12;0,5;1), the exact value is 0.5000004).
- Serious — SpreadsheetMath.cs: incomplete beta and gamma with a cap of 300 iterations gave wrong values without error with large parameters.
- Serious — BesselFunctions.cs: huge orders ran for seconds (BESSELY(1;1E+9) 4 s); now #NUM! like the reference suite (Err:502/523).
- Serious — FinancialFunctions.cs CUMIPMT/CUMPRINC: loop per period; closed form `A_e − A_{s−1}` on the balances.
- Serious — ExtendedFunctions.cs B() and DiscreteFourier.cs FOURIER: long loops (B(1E+12;0,5;0;5E+11) exceeded the watchdog's 5 minutes; FOURIER on 20,000 samples 4·10⁸ steps).
- Medium (security) — IntegrationFunctions.cs FILTERXML: internal DTDs and entities now rejected with #VALUE!; the external resolver was already null.
- Serious — SummaryFunctions.cs SUBTOTAL: nested subtotals entered the total; now cells whose formula contains SUBTOTAL or AGGREGATE are skipped (Excel and the reference suite do the same).
- Verification requested: VALUE("(12)") = -12 and VALUE("(5)") = -5 in the reference suite, headless; Excel too reads parentheses as a minus sign. The line removed from the old test was therefore wrong; the change stays.
- Minor, open: SORT with an order other than 1/-1; DATABASE with headers compared according to the current culture; CODE on non-ASCII characters; ISEVEN/ISODD beyond 2^63.

## Still to read
None: the whole Evaluation/Functions/Providers and Numerics area is read in full.

## Suggested live tests
- «Subtotals» command with grand total: the total must no longer double.
- A cell with `=SEQUENCE(100000;100000)` or `=BASE(5;2;1E+9)`: immediate #NUM!, no freeze.
- `=FOURIER(A1:A100000;1)` on a long column: result in under a second.

## Coverage: Operations, Evaluation root, Localization (round 3)

| File | Lines read | Outcome |
|---|---|---|
| Operations/AutoInput.cs | 71/71 | no defects |
| Operations/AutomaticRefreshHistory.cs | 43/43 | no defects |
| Operations/CellEditText.cs | 68/68 | no defects |
| Operations/CellInputRecognizer.cs | 160/160 | `1/2/99999999999` (a date part beyond `int`) threw `OverflowException` on entry (serious) — now date parts have at most 4 digits and the rest is text |
| Operations/ChartDataExtractor.cs | 173/173 | no defects (a whole-column range walks all rows: minor, open) |
| Operations/ConditionalFormattingEvaluator.cs | 497/497 | `RangeExtremes.Stops`, `DuplicateTest`, `RankTest`, `AverageTest` re-read and re-sort the whole range of the rule **for every cell** drawn: with bars or scales on 100,000 cells drawing is O(visible cells × n log n) (serious, performance, open: needs a cache invalidated by a sheet revision counter, which `Worksheet` does not have; touches Sheets and Sheets.UI) |
| Operations/ConditionalRuleRemoval.cs | 39/39 | no defects |
| Operations/DataValidator.cs | 156/156 | no defects (numeric list choices are compared with invariant `ToString`: minor, open) |
| Operations/DefinedNameRules.cs | 63/63 | no defects |
| Operations/DependencyTracer.cs | 79/79 | no defects |
| Operations/EngineConditionalAppearance.cs | 14/14 | no defects |
| Operations/ExternalDataWriter.cs | 68/68 | no defects (one-million-cell cap present) |
| Operations/FormControlRelinker.cs | 61/61 | no defects |
| Operations/FormulaRewriter.cs | 94/94 | no defects (whole rows and columns have the other dimension absolute and do not break) |
| Operations/FormulaTranslator.cs | 13/13 | no defects |
| Operations/GoalSeek.cs | 120/120 | no defects (the variable is always restored in `finally`) |
| Operations/MultipleOperations.cs | 142/142 | no defects |
| Operations/ReferenceCycler.cs | 112/112 | no defects |
| Operations/ScenarioManager.cs | 89/89 | no defects |
| Operations/SheetCellShift.cs | 130/130 | when inserting cells, a cell pushed beyond the last row or column stays where it is instead of disappearing or making the insertion be refused as in the reference suite (minor, open); merges, validations and conditional rules do not follow a cells-only shift (medium, open: to be looked at with SheetStructure) |
| Operations/SheetClipboard.cs | 471/471 | `Copy` enumerated every cell of the range: copying a whole column created a million entries, copying the whole sheet (1.7·10¹⁰ cells) froze the app (serious) — now only cells up to the used area are copied and paste separately empties the occupied cells of the destination that the copy does not carry; pasted text «NaN»/«Infinity» became a non-finite number (medium) — now stays text. Transaction label «Incolla» in Italian in the engine (minor, open); empty cells beyond the used area do not carry the source's column style (minor, open) |
| Operations/SheetConsolidation.cs | 211/211 | no defects |
| Operations/SheetDataTools.cs | 180/180 | no defects |
| Operations/SheetDuplicator.cs | 89/89 | no defects |
| Operations/SheetFill.cs | 243/243 | dragging «Lotto 123456789012345678901234» threw `OverflowException` and «Voce ٣» (non-ASCII digits, Unicode `\d`) `FormatException` (serious) — now only ASCII digits, at most 18, otherwise the text is repeated |
| Operations/SheetFillAcross.cs | 111/111 | no defects |
| Operations/SheetFilter.cs | 255/255 | no defects (the text criterion is re-parsed for every row: minor, open) |
| Operations/SheetMerge.cs | 60/60 | no defects |
| Operations/SheetOutline.cs | 165/165 | closing a group nested inside one already closed saved a zero height: «show level 0» and then «level 2» left the inner group's rows hidden forever (medium) — now the size is taken from the closed group that already keeps it |
| Operations/SheetRangeReference.cs | 79/79 | no defects |

## Still to read (round 3)
Operations: SheetRegions, SheetRenaming, SheetSearch, SheetSnapshot, SheetSort, SheetStructure, SheetSubtotals, Solver, SparklineData, StandardFilter, StatisticsProcedures, StructureRewriter, WorkbookCheckpoint, WorkbookSearch; Evaluation root (ArrayModeContext, ArrayParameterClasses, EvalValue, EvaluationScope, Evaluator, Formats/FormulaFormatInference, FunctionInventory, LabelReferences, NameExpansion, RowVisibility, SubstitutingContext); Recalculation/RangeDependencyIndex (new); Localization (5 files); then PulseOffice.Formulas and PulseOffice.Formulas.Editor.

## Coverage: Operations (round 4)

Fixes of the points left open in round 3:
- Medium — SheetCellShift.cs: inserting or deleting cells only did not shift merges, validations, conditional rules and notes. Now `ISheetStructure.ShiftBand` applies to the band the same parts of the structure (rules, validations, notes, merges); a range crossing the edge of the band stays where it is (the reference suite refuses the shift).
- Medium — SheetStructure.cs: validations did not follow inserted or deleted rows and columns (no `StructurePart` touched them); now `ValidationsPart` shifts them, removes them if deleted and rewrites the formulas.
- Medium — SheetClipboard.cs `PasteLinks`: «Paste link» pushed one history step per cell (one ⌘Z per cell); now one transaction.
- Minor — «Incolla», «Sostituisci», «Ordina» in Italian in the engine: the name comes from the caller (`PasteRequest.HistoryLabel`, `SearchOptions.HistoryLabel`, parameter `historyLabel` of `ISheetSort.Sort`), which takes it from the translated catalogue (new key `history.ledger.sort` in the five languages).

| File | Lines read | Outcome |
|---|---|---|
| Operations/SheetRegions.cs | 66/66 | the current region was quadratic: every step grew by one row and re-read the side columns over the whole height (40,000 rows: 13 s) (serious); and it did not include diagonal neighbours as the reference suite and Excel do (medium). Now each side grows in blocks and the corners are checked too |
| Operations/SheetRenaming.cs | 139/139 | no defects (sheet names compared with the current culture: minor, open) |
| Operations/SheetSearch.cs | 256/256 | history label in Italian (minor, fixed); regular expressions already have the 1 s cap |
| Operations/SheetSnapshot.cs | 140/140 | no defects (autofilter and protection are not in the snapshot: minor, open) |
| Operations/SheetSort.cs | 215/215 | sorting a whole column enumerated a million rows per column (17·10⁹ cells for the whole sheet) (serious) — now the range is limited to the used area (empty rows still go to the bottom) |
| Operations/SheetStructure.cs | 384/384 | validations not shifted (medium, fixed, see above) |
| Operations/StructureRewriter.cs | 93/93 | no defects |
| Operations/WorkbookSearch.cs | 80/80 | label in Italian (minor, fixed) |
| Operations/SheetSubtotals.cs | 103/103 | no defects |
| Operations/Solver.cs | 248/248 | no defects (variables are restored in `finally`; non-numeric constraints give an infinite score without exceptions) |
| Operations/SparklineData.cs | 57/57 | a whole-column source read a million cells on every draw, and with «same scale» multiplied by every sparkline of the group (medium, performance) — beyond 65,536 cells the source is limited to the used area (below the cap it stays whole, so as not to change the horizontal scale). Axis always from zero also for lines, where Excel starts from the data minimum (minor, open: the model has no axis options); with «same scale» the group's values are re-read for every sparkline (minor, open: needs a cache by revision) |
| Operations/StandardFilter.cs | 57/57 | no defects (AND/OR evaluated left to right like the reference suite; `*` and `?` typed by the user in «contains» act as wildcards: minor, open) |
| Operations/WorkbookCheckpoint.cs | 62/62 | the restore point keeps neither defined names nor sheet properties (name, colour, visibility, protection): a «checkpointed» command that changes them does not undo them (minor, open); `Restore` recalculates everything once per sheet (minor, open) |
| Operations/StatisticsProcedures.cs | 599/599 | one-factor ANOVA recomputed the group mean for every value (O(n²): 60,000 values, 4 s) (serious) — mean computed once; the «Fourier analysis» procedure was a direct O(n²) DFT (20,000 samples: 4 s) (serious) — now uses the Bluestein transform already used by FOURIER |
| Evaluation/Evaluator.cs | 441/441 | operations between matrices of different sizes repeated the last row or column of the smaller one, inventing values (medium) — now like the reference suite (verified headless: `SUMPRODUCT(A1:A3+B1:B2)` = 33, `A1:A3+B1` = 36): single rows or columns are extended, otherwise the smaller size applies |
| Evaluation/ArrayModeContext.cs | 58/58 | no defects |
| Evaluation/ArrayParameterClasses.cs | 30/30 | no defects |
| Evaluation/EvalValue.cs | 141/141 | no defects |
| Evaluation/EvaluationScope.cs | 44/44 | no defects |
| Evaluation/SubstitutingContext.cs | 111/111 | in multiple operations (and wherever evaluation runs with substituted inputs) nested dependent formulas were evaluated with the cell and sheet of the outer formula: references without a sheet read from the wrong sheet and wrong `ROW()` (serious, wrong values: 14 instead of 107, 21 instead of 7) — now every dependent cell has its own context (`SubstitutionSources.ContextFor`); a chain of about 1000 dependent formulas exhausted the stack and brought the process down (serious) — now substituted evaluation runs on a thread with a 512 MB stack and the chain has a cap of 100,000 cells (#VALUE! beyond) |
| Evaluation/FunctionInventory.cs | 235/235 | no defects |
| Evaluation/LabelReferences.cs | 64/64 | no defects (search cap present) |
| Evaluation/NameExpansion.cs | 42/42 | no defects |
| Evaluation/RowVisibility.cs | 8/8 | no defects |
| Evaluation/Formats/FormulaFormatInference.cs | 236/236 | `-A1` with A1 in currency does not inherit the format (the reference suite does) (minor, open) |
| Recalculation/RangeDependencyIndex.cs | 118/118 | no defects (O(n) removal per bucket: minor) |
| Localization/FormulaLocalization.cs | 67/67 | no defects |
| Localization/FormulaPunctuation.cs | 33/33 | no defects |
| Localization/FormulaTextTranslator.cs | 119/119 | no defects (`,5` in Italian without a leading zero becomes a separator: minor, open) |
| Localization/FormulaWriter.cs | 20/20 | no defects |
| Localization/FunctionNameVocabulary.cs | 1843/1843 (tables checked programmatically: no duplicates) | in French MIRR is called TRIM, which is also the English name of SUPPRESPACE: typing `TRIM(` with the French interface gives MIRR (minor, open; it is also the reference suite's name) |

## Coverage: PulseOffice.Formulas and PulseOffice.Formulas.Editor

| File | Lines read | Outcome |
|---|---|---|
| Formulas/FormulaParser.cs | 531/531 | no defects (depth limited to 256) |
| Formulas/MathMlSyntax.cs | 195/195 | a deeply nested MathML (hostile ODF file) brought the process down with a stack overflow (serious, security) — now beyond 256 levels the formula reads as empty (`XmlDepthGuard`, iterative) |
| Formulas/OmmlSyntax.cs | 204/204 | same stack overflow with nested OMML (serious, security, fixed); normal text (`m:nor`) was re-parsed as formula syntax, so «a over b» became a fraction and the text did not round-trip (medium) — now `m:rPr/m:nor` is written and read back as text |
| Formulas/Formula.cs | 161/161 | no defects |
| Formulas/LinearFormulaWriter.cs | 87/87 | quotation marks in text become apostrophes (StarMath has no escape: minor, open) |
| Formulas/FormulaSpans.cs | 89/89 | no defects |
| Formulas/FormulaVocabulary.cs | 101/101 | no defects |
| Formulas/StarMathSource.cs | 111/111 | no defects |
| Formulas/FormulaDiagnostics.cs | 113/113 | no defects |
| Formulas/FormulaCatalog.cs | 114/114 | the Italian «testo» fragment ended up in documents in every language (minor) — now `""` with the cursor inside |
| Formulas/FormulaLayout.cs | 225/225 | nested rows are re-measured at every level (minor, open) |
| Formulas.Editor/FormulaSample.cs | 39/39 | no defects |
| Formulas.Editor/FormulaColorizer.cs | 75/75 | no defects |
| Formulas.Editor/FormulaEditorTexts.cs | 85/85 | no defects |
| Formulas.Editor/FormulaPreview.cs | 93/93 | no defects |
| Formulas.Editor/FormulaEditorView.cs | 392/392 | no defects |
| Formulas.Editor/FormulaDocumentationPanel.cs | 122/122 | no defects |

## Still to read
None of the assigned files. The open minors marked above remain.

## Suggested live tests (round 4)
- Insert cells shifting down over a merge, a validation and a note: they must move down together with the cells; ⌘Z brings them back up.
- Paste special → link on a 3×3 block: a single ⌘Z undoes everything; in the Edit menu the step name is in the interface language (also «Sort» and «Replace all»).
- Select a whole column and sort it: immediate.
- Ctrl+* (current region) on a column of 40,000 rows: immediate.
- Multiple operations with a formula using a cell of another sheet: correct value.
- Folio formula with text in quotes, saved to DOCX and reopened: the text stays text.
