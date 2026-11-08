# Deep audit round 105: Folio fields end to end

Scope: every `TextFieldKind` in Folio, the update path (`FieldUpdater`, value sources, condition evaluator), cross-references,
and the DOCX/ODT field codecs. Deck masters were out of scope (another agent).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Documents/Fields/FieldCondition.cs | 287/287 | safe: depth cap 256, text cap 64 KiB, division by zero gives 0, no recursion between variables (evaluation is sequential, a variable sees only values set before it) |
| src/PulseOffice.Documents/Fields/FieldUpdater.cs | 172/172 | defect F1 (marks inside a shrinking field) fixed |
| src/PulseOffice.Documents/Fields/FieldSources.cs | 244/244 | ok; per-field statistics recount noted (P1) |
| src/PulseOffice.Documents/Fields/FieldContext.cs | 90/90 | ok |
| src/PulseOffice.Documents/Fields/ReferenceTargets.cs | 97/97 | ok |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxFieldInstructions.cs | 444/444 | defects F2 (date pictures) and F3 (sequence switches) fixed |
| src/PulseOffice.Formats.Odf/Odt/OdtVocabulary.cs | 297/297 | ok; sequence `style:num-format` always "1", consistent with the model |
| src/PulseOffice.Documents.Layout/Flow/PageFields.cs | 74/74 | ok |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxWriter.cs | 95-150 (fidelity rules) | field losses DOCX021/022/023/128/195 declared |

## Defects

- **F1 (medium)** `FieldRunRewriter.Rewrite` moved only marks at or after the end of a changed field: a bookmark or comment
  anchor inside a field whose text shrank kept its old offset, past the new field end and possibly past the paragraph
  end. Marks inside a field now map into the new text, clamped to the field and to the paragraph.
  Test: `FieldRoundOneHundredFiveTests`.
- **F2 (serious)** The model's date and time formats are .NET format strings, but DOCX wrote them verbatim after `\@` and
  read Word pictures verbatim: `D` and `T` (long date/time) became Word's day-of-month and a literal, `h:mm tt` showed
  `tt` literally in Word, and Word's `AM/PM`, `A/P`, a single-letter picture such as `d` (which .NET reads as the
  short-date standard format) and upper-case `D`/`Y` came back wrong. New `IWordDatePictures` (`WordDatePictures`)
  converts both ways: standard formats expand to the invariant pattern, `tt`/`t` become `AM/PM`/`A/P`, quoted and
  escaped literals stay literals, single letters gain `%`. Test: `DocxDatePictureTests`.
- **F3 (serious)** `SEQ` fields were read as plain sequences whatever their switches: `\c` (repeat), `\h` (hidden),
  `\r n` / `\s n` (restart, chapter reset), `\n` and number shapes other than Arabic were dropped, so the next field
  update renumbered or showed hidden numbers. Such sequences are now kept as preserved Word fields (instruction and
  result written back verbatim); plain `SEQ name` with `\* ARABIC` / `\* MERGEFORMAT` stays a live sequence.

## Noted, not changed

- P1 (minor, performance): `StatisticFieldSource` recounts tables, images and objects per field; the word statistics are
  cached per update. With 10,000 count fields this is O(fields x blocks) only for those three kinds.
- The `/` and `:` of a format are the culture's separators in the model and literal in Word; kept as they are so that the
  formats offered by the field dialog round-trip unchanged.
