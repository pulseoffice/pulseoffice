# Round 179: Folio lists and numbering

Area: bulleted, numbered and multi-level lists, list styles, restarts and continuation, number formats, outline
numbering, and their DOCX and ODT round trip.

## Defects

1. **Medium: tenth-level list items wrote invalid DOCX levels.** Folio lists have ten levels, Word nine. A tenth-level
   item wrote `w:ilvl` 9 and, when restarted, a `w:lvlOverride` on level 9, which Word does not define; tenth-level
   headings wrote `w:outlineLvl` 9, which Word reads as body text. Nothing was declared. The level index is now clamped to
   the ninth where it is written (`DocxParagraphCodec`, `DocxListMap.Restart`), and `DOCX243` declares the loss. ODT keeps
   ten levels. Test: `Round179FolioListTests`.
2. **Medium: Word number formats without a model format were read as Arabic silently.** `decimalZero`, `ordinal`,
   spelled-out and non-Latin `w:numFmt` values became Arabic numbers with no diagnostic. `DOCX244` now declares them
   (`DocxNumberingCodec.LosesFormats`). Test: `Round179FolioListTests`.
3. **Minor: the same for ODT.** An unknown `style:num-format` token of a list or outline level was read as Arabic
   silently; `ODT183` declares it (`OdtNumberFormats.Knows`). Test: `Round179OdtListFormatTests`.

All three codes are translated in the five language files; `DOCX243` and `DOCX244` are listed in
`tools/corpus-audit/declared-features.json`.

## Researched before declaring

The model has seven number formats; adding zero-padded, ordinal and non-Latin formats would touch the model, the
labeler, both codecs and the list dialog, which is a feature block, not an audit fix. Word has no tenth list level in
ECMA-376 (`ST_DecimalNumber` levels 0 to 8 for `w:ilvl` and `w:lvl`), so the tenth level cannot be mapped natively.

## Open items

- A level text with a separator other than the period between placeholders (`%1-%2`) is read with periods: the model
  has no per-level separator.
- Editing paths (Tab and Shift+Tab, Enter on an empty item, Backspace at the start, paste of list items) and PDF
  numbering were not reviewed in depth in this round.
