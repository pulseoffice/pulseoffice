# Round 106: Folio fields, part 2

Scope: what round 105 did not read: the field parts of the ODT and DOCX body readers and writers, `OdtPreservedFields`,
`FolioFieldCommands`, `FolioFieldOptions`, `FolioFieldRefresh`, mail merge, input and drop-down fields, and the
per-field recount of tables, images and objects left over by round 105 (P1). Merged main after round 105 before editing.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Documents/Fields/MailMerge.cs | 185/185 | ok |
| src/PulseOffice.Documents.UI/Commands/FolioMailMergeCommands.cs | 195/195 | ok |
| src/PulseOffice.Documents.UI/Shell/FolioMergeOutput.cs | 142/142 | D1 |
| src/PulseOffice.Documents.UI/Shell/FolioMergeWizard.cs | 285/285 | ok |
| src/PulseOffice.Documents/Fields/FieldSources.cs | 60-130 (statistics) | P1 of round 105 fixed (D2) |
| src/PulseOffice.Formats.Odf/Odt/OdtPreservedFields.cs | 90/90 | D4, D5 |
| src/PulseOffice.Documents.UI/Editing/FolioFieldRefresh.cs | 77/77 | ok (merge preview stays outside the history by design) |
| src/PulseOffice.Documents.UI/Commands/FolioFieldCommands.cs | 301/301 | ok |
| src/PulseOffice.Documents.UI/Fields/FolioFieldOptions.cs | 248/248 | D3 |
| src/PulseOffice.Documents.UI/Fields/FolioFieldInsertion.cs | 94/94 | D3 |
| src/PulseOffice.Formats.Odf/Odt/OdtVocabulary.cs (OdtFieldTable) | 297/297 | D4 |
| src/PulseOffice.Formats.Odf/Odt/OdtBodyReading.cs | 330-375 (fields) | D4 |
| src/PulseOffice.Formats.Odf/Odt/OdtBodyWriting.cs | 185-310 (fields, field codes) | ok |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfParagraphText.cs | 199/199 | ok, reused for D4 |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxInlineWriter.cs | 180-290 (fields) | ok |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxInlineReader.cs | 455-545 (complex fields) | ok |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxFieldInstructions.cs | 452/452 | D5 |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxLegacyFormFields.cs | 38/38 | ok |
| src/PulseOffice.Formats.Odf/Odt/OdtLegacyFormFields.cs | 54/54 | ok |
| src/PulseOffice.Formats/PreservedFieldTable.cs | 56/56 | D5 |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxWriter.cs | 60-160 (fidelity list) | ok |

Field kinds checked both ways (ODT attribute / DOCX switch): page number and count, title, subject, description, keywords,
counts (DOCX022 declared), references with text/page/number/direction (chapter, caption, value declared DOCX023), note
references, user and document variables, variable set with `text:display="none"`, conditional text (nested IF parts),
hidden text (DOCX021), hidden paragraph (native data), custom property, mail merge column with `table-name`,
`table-type`, `database-name` and the connection in settings, sequence, chapter, author, file name, date and time with
fixed, `date-value`/`time-value`, picture `\@` and `w:fldLock`, created and modified dates, legacy form fields
(FORMTEXT, FORMCHECKBOX, FORMDROPDOWN, FORMDATE), input fields (FILLIN / `text:text-input`), drop-downs (`text:drop-down`,
native element through `w:fldData`), placeholders (MACROBUTTON).

## Defects

- **D1 (minor)** `MergeFileNames.For`: names taken from a data column used the host's invalid-character list (on macOS
  and Linux only `/` and NUL), so files merged on one system carried `? * < > | "` that Windows cannot open, kept trailing
  dots and had no length cap (a 400-character value failed the write). Fixed: portable character set, control characters
  replaced, trailing dots trimmed, names capped at 120 characters. Test `File_names_from_a_column_are_portable_and_bounded`.
- **D2 (minor, performance; round 105 P1)** `StatisticFieldSource` walked all blocks and objects for every table, image
  and object count field. Now counted once per update pass in `FieldContext.ObjectCounts` (`FieldObjectCounts`). Test
  `Tables_images_and_objects_are_counted_once_per_update_pass`.
- **D3 (medium)** Editing a fixed date or time field in the field dialog overwrote the date it was fixed on with today
  (`ClockFieldOptions.Compose`), and a new format was not applied to the shown text, since fixed fields are never
  recomputed (`FolioFieldInsertion.Replace` kept the old text). Fixed: the stored value is kept, and the text is
  reformatted from it on edit (left unchanged when no stored value exists, as in fields locked in DOCX). Tests
  `Changing_the_format_of_a_fixed_date_keeps_the_date_it_was_fixed_on` (failed before the fix),
  `Changing_the_format_of_a_fixed_date_reformats_its_stored_date`, `Fixing_a_live_date_stores_today`.
- **D4 (medium)** ODT field results were written as raw strings (`element.Add(shown)`) and read with `XElement.Value`:
  tabs, line breaks and runs of spaces in a field result (merge values, properties, preserved Word results) were written
  as literal whitespace that ODF readers collapse, and `text:s`, `text:tab`, `text:line-break` inside fields written by
  other suites were dropped on reading. Fixed at the root: `OdtFieldTable` and `OdtPreservedFields` encode and decode the
  shown text with the shared `IOdfParagraphText` codec. Tests `OdtFieldTextTests`.
- **D5 (minor, fidelity)** The prompt of an input field was lost between the formats: an ODF `text:text-input` became a
  bare ` FILLIN ` for Word, and a Word `FILLIN "prompt"` became a `text:text-input` without `text:description`. Fixed:
  the prompt crosses both ways (`IPreservedFieldTable.Prompt`). Tests
  `An_ODF_input_field_keeps_its_prompt_in_the_Word_FILLIN_instruction`,
  `A_Word_FILLIN_prompt_becomes_the_description_of_the_ODF_input_field`.

## Remaining

- A visible ODF variable-set field becomes a Word `SET`, which Word never shows; the value survives, the visibility in Word
  does not. Not declared yet; a declaration code from the coordinator's range is needed.
- `FolioFieldRefresh.Run` recomputes layout facts between passes from the editor's current layout, which is not
  re-laid-out between passes: page counts settle on the next update. Low impact, not changed.

## Live checks

Field dialog: edit a fixed date, change its format, check the date stays. Mail merge to separate files with a name column
containing `?` or a long value. Open an ODT with a field containing tabs in the reference suite.
