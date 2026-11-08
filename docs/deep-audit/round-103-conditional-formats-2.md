# Deep audit round 103: Ledger conditional formatting and data validation, part 2

Continues [round-102-conditional-formats.md](round-102-conditional-formats.md).

## Coverage (read in full)

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.UI/Scripting/FormatRuleFacade.cs | 186/186 | round 102 leftover: no standard deviations (fixed) |
| src/PulseOffice.Sheets.UI/Conditional/ConditionalRuleEditorView.cs | 413/413 | no standard deviations, no stop-if-true (fixed) |
| src/PulseOffice.Sheets.UI/Conditional/ConditionalRuleCompletion.cs | 56/56 | keeps the deviations, clean |
| src/PulseOffice.Formats.OpenXml/Conditional/X14DataBars.cs | 163/163 | clean |
| src/PulseOffice.Formats.OpenXml/Conditional/X14RuleReaders.cs | 168/168 | comparison logic moved to a shared service |
| src/PulseOffice.Formats.OpenXml/Conditional/XlsxConditionalExtension.cs | 108/108 | clean |
| src/PulseOffice.Formats.OpenXml/XlsxValidationExtension.cs | 38/38 | clean |
| src/PulseOffice.Formats.Odf/Conditional/ConditionForms.cs | 147/147 | clean |
| src/PulseOffice.Formats.Odf/Conditional/RuleCodecs.cs | 333/333 | clean (reverse icons, bar border and direction already declared) |
| src/PulseOffice.Formats.OpenXml/Conditional/RuleCodecs.cs | 443/443 | defect 1 |
| src/PulseOffice.Formats.OpenXml/XlsxConditionalMapper.cs | 90/90 | clean |
| src/PulseOffice.Formats.Odf/Validation/ValidationForms.cs | 267/267 | clean |
| src/PulseOffice.Formats.OpenXml/XlsxMetadataMapper.cs | 262/262 | defect 2 |
| src/PulseOffice.Sheets.UI/GridParts/Painting/GridCellPainter.cs | 231/231 | visible cells only, range snapshots cached per sheet revision; note 1 |
| src/PulseOffice.Sheets.Engine/Operations/ConditionalRangeSnapshots.cs | 71/71 | clean |
| src/PulseOffice.Sheets.UI/Conditional/ConditionalRulesManagerView.cs | 274/274 | clean |
| src/PulseOffice.Sheets.UI/Conditional/ConditionalRuleCatalog.cs | 135-167 | description now names the deviations |
| src/PulseOffice.Sheets.UI/LedgerConditionCommands.cs | 80-123 | one undo step per manager or editor change, clean |
| src/PulseOffice.Sheets.UI/Dialogs/ValidationDraft.cs | 167/167 | clean |
| src/PulseOffice.Sheets.UI/Dialogs/ValidationCriteriaField.cs | 263/263 | clean |
| src/PulseOffice.Sheets.UI/LedgerToolCommands.cs | 200-290 | defect 3 |
| src/PulseOffice.Sheets.UI/Scripting/ValidationFacade.cs | 153/153 | defect 3 |

## Defects

1. **Medium** - XLSX `cellIs` rules: operands were read with `Trim('"')`, so a reference or expression operand
   (`$D$1*2`, very common in Excel files) became the literal text "$D$1*2" and the rule compared against text, and a
   quoted string kept its doubled quotes (`a""b`). The x14 reader already handled both. Fixed at the root with one shared
   service, `XlsxComparisonOperands` (literal unquoting, otherwise an equivalent formula anchored on the rule's first
   cell), used by both `XlsxOperatorCodec` and `X14StyledRuleReader`.
2. **Medium** - XLSX validation lists typed in the rule longer than 255 characters: Excel refuses them and drops the
   validation on open, silently. Declared as `XLSX103` in `XlsxSpreadsheetWriter.CheckFidelity` (five languages).
3. **Medium** - Data validation on part of an existing rule: the dialog and the macro facade removed only rules with
   exactly the same range, so editing A3:A4 inside an A1:A10 rule left two overlapping rules, and choosing "any value"
   on part of a rule cleared nothing while the status bar said it did; the macro `Clear("A2")` removed the whole A1:A3
   rule. Fixed with the engine service `IValidationPlacement` (cuts existing rules around the area, re-anchors their
   relative formulas, then adds the new rule), used by `LedgerToolCommands` and `ValidationFacade`; one undo step.

## Leftover from round 102, done

- The rule editor offers the number of standard deviations (none, 1, 2, 3) for average rules and a stop-if-true
  check box for every highlighting rule; the macro `AddCondition(range, "aboveAverage", "2")` takes the deviations
  (0 to 3, empty is 0); the API documentation and the rule description name them. Five languages.
- Population or sample: Microsoft's documentation (`stdDev` in ECMA-376, the Office.js preset criteria) does not
  state which standard deviation Excel uses, and no Excel is available here. The sample deviation of round 102 is kept;
  to be checked against a real Excel (a range whose cutoff differs between `STDEV.S` and `STDEV.P`).

## Notes (not changed)

1. `GridCellPainter` evaluates the conditional rules of a visible cell twice per frame (fill pass and text pass);
   the range snapshots are cached, so the cost is per visible cell, not per range. Minor.

## Tests

- `ScriptFacadeTests.LeMacroImpostanoLeDeviazioniStandardDellaMedia`, `UnaConvalidaSuUnaParteSostituisceSoloQuellaParte`
- `ConditionalRulesTests.LeDeviazioniStandardSopravvivonoAlCompletamentoESiDescrivono`
- `LedgerFormatSurfacesTests.L_editor_imposta_deviazioni_standard_e_interruzione`
- `RangeConditionRoundTripTests`: `LeVirgoletteRaddoppiateDiUnConfrontoSiTolgonoInLettura`,
  `UnConfrontoConUnRiferimentoDiExcelDiventaUnaFormula` (both failed before the fix), `UnElencoDiConvalidaOltre255CaratteriEDichiaratoInXlsx`
- `ValidationPlacementTests` (Sheets.Engine)
- `LedgerFacadeContractTests`: `validation.Clear` now expects the rest of the rule to stay.

Passed: Sheets.Engine.Tests (filtered), Sheets.UI.Tests (filtered, 175), Formats.OpenXml.Tests (conditional, parity and
round trip filter, 1597), Desktop.Tests (filtered, 48), Architecture.Tests (43), Localization.Tests (32).
