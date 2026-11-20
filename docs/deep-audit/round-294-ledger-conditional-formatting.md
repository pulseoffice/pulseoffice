# Round 294: Ledger conditional formatting, second pass

Date: 2026-11-20. Area: the parts round 293 left open (ODS codecs, x14 extensions, dialog, range following,
XLSX `dxf` number format and colours, top/bottom percent rounding).

## Defects found and fixed

| Severity | Defect | Fix |
|---|---|---|
| Medium | XLSX `dxf` number format neither written nor read: a rule that changes the number format lost it. | `DifferentialFormatTable` writes `numFmt` (built-in code or the workbook's custom code) and reads it back, registering custom codes in the workbook catalogue. |
| Medium | Theme and indexed colours (and tint) in `dxf` fonts and fills, colour scales and data bars were ignored on read: Excel files using theme colours lost their conditional colours. | `XlsxColourPalette`/`XlsxColourPalettes`: the workbook theme (Excel order lt1, dk1, lt2, dk2, accents, links), the legacy indexed palette or the stylesheet's `indexedColors`, and HLS tint. Shared through `SpreadsheetColours.Read`. |
| Medium | Copy and paste (all or formats) did not carry conditional formatting, unlike Excel and the reference suite. | `ClipboardSnapshot.ConditionalRules`; pasting styles adds the clipped rules with formulas translated to the target and replaces rules wholly inside the pasted area, in one undo step. |
| Medium | ODS dropped stop if true, reversed icon order, standard deviations of average rules and data bar border colours and direction (only declared). ODF 1.3 and the calcext extension have no attribute for them. | Written and read as attributes in the `urn:pulseoffice:spreadsheet` namespace (`OdfConditionalExtras`); the fidelity notices remain, reworded, because other applications ignore them. |
| Minor | Top/bottom N% counted with ceiling; Excel takes the integer part with at least one item. | `RankTest` rounds down, minimum one when the percentage is positive. |

## Read without defects found
- `RulesPart` (insert/delete rows and columns): ranges shift, shrink or drop, formulas follow the surviving start.
- ODS `DateIsCodec` periods and `calcext:condition` base cell translation.

## Not covered (queued)
- Manage rules dialog and rule editor.
- Transposed paste of conditional rules.
- Theme colours outside conditional formatting (cell styles, borders).

## Tests
- `Round294ConditionalDxfTests` (OpenXml), `Round294ConditionalExtrasTests` (Odf),
  `Round294ConditionalPasteTests` and a percent rank case in `RangeConditionEvaluationTests` (Engine).
