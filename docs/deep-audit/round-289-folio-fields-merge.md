# Round 289: Folio fields, cross-references and mail merge

Area: fields (page number and count, date and time with formats, fixed and updating, document properties, file name,
chapter, sequences and captions, conditional and input fields, user variables), cross-references to headings,
bookmarks, captions, notes and pages, and mail merge (sources, merge fields, preview, output), with their DOCX and ODT
round trips. Files read: `src/PulseOffice.Documents/Fields/*`, `DocxFieldInstructions`, the ODT field table
(`OdtVocabulary`), `PageFields`, `FolioFieldRefresh`, `FolioMailMergeCommands`, `FolioMergeOutput`. Earlier field rounds
(105, 106, 167) were taken as the baseline.

No build or test was run by this audit (coordinator rule for the cycle); the tests below run at the join.

## Defects

- **D1 (serious, mail merge)** Conditions on merge columns were never evaluated with the record: `IF {MERGEFIELD
  Gender} = "F" "Ms" "Mr"` (read from DOCX as a conditional field on `Gender`) produced "Mr" for every letter, in the
  merged output and in the preview, because merge columns were not variables of the condition. Fixed:
  `IMergeVariables` adds the record's columns (also as `database.table.column`) to the condition variables; the merge
  engine resolves conditional text, hidden text and hidden paragraphs whose condition names a column into plain text
  (or a literal condition) for each record; `ConditionalFieldSource` uses the current record while previewing, and
  moving between preview records now refreshes those fields too. Tests `Round289MergeConditionTests`,
  `Round289MergePreviewTests`.
- **D2 (medium, mail merge)** Merging into one document pasted each record's body after the first, but headers and
  footers live on page styles: a merge field in a header showed the first record on every letter. Fixed:
  `IMergePageStyles` clones, for each further record, the page styles whose header or footer holds merge fields (and
  the styles chaining to them), with that record's values, and starts the record on the cloned style. Test
  `Round289MergeConditionTests.Each_merged_record_keeps_its_own_header`.
- **D3 (medium, fidelity)** Sequence fields always numbered in Arabic: ODT `style:num-format` on `text:sequence` was
  dropped on reading and always written as `1`, and DOCX `SEQ name \* ROMAN` (or `roman`, `ALPHABETIC`, `alphabetic`)
  became an opaque field that no longer counted. Fixed: the format is kept in the field (`SequenceFormats`, evaluated
  by `ISequenceNumerals`), written to and read from both formats. Tests `Round289SequenceFormatTests`.
- **D4 (medium, fidelity)** A page number field's own numerals (`PAGE \* roman`, ODT `text:page-number
  style:num-format`) were dropped on reading. Fixed: kept in the field, written back to both formats, and shown by the
  page layout when the page style numbers in Arabic. Tests `Round289SequenceFormatTests`,
  `Round289PageFieldShapeTests`.
- **D5 (medium, ODF interop)** Date and time field formats were written only as our own `pt:format`, without a
  `style:data-style-name`: other ODF applications refreshing the field showed their default format; and a date field
  from another application lost its data style, so our next field update changed its look. Fixed: `OdtDatePictures`
  turns the field's picture into a `number:date-style` or `number:time-style` (common styles in `styles.xml`, visible
  from content and headers) and back. Test `Round289DateFieldStyleTests`.
- **D6 (medium, template content)** `MERGEFIELD name \b "text" \f "text"` lost its text before and after the value,
  so merged letters lost words. Fixed: kept in the field, written back to DOCX, kept in ODT as our
  `pt:text-before`/`pt:text-after`, and applied by the merge and the preview only around a non-empty value. Tests
  `Round289MergeFieldAffixTests`, `Round289MergeConditionTests.Merge_field_affixes_appear_only_around_a_value`.

## Remaining (queued)

- The merge wizard offers a record range only: no recipient list with excluded records or a filter (medium UX gap).
- A merged record whose body paragraphs switch to a page style with merge fields in its header keep the template
  style name (only the record's first paragraph is moved to the cloned style).
- A page field's own numerals apply only on pages numbered in Arabic by their page style.
- `REF \n`, `\w` and ODF `number-no-context` / `number-all-superior` collapse to one "number" format (`\r`).
- Chapter fields keep level 1 only (ODT `text:outline-level`, `STYLEREF` of another heading style).
- `MERGEFIELD` case switches (`\* Upper`, `\* Caps`) and numeric pictures (`\#`) are still dropped.
- Date pictures: `/` and `:` are written as literal ODF text; era, quarter and week parts of a foreign data style are not
  read (the field falls back to the default format).
- No dialog control yet to choose the numerals of a sequence or page number field.
- The mail merge source is still declared lost in DOCX (DOCX028); `w:mailMerge` with `w:query` was not attempted.
