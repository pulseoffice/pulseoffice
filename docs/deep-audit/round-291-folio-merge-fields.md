# Round 291: Folio mail merge and fields leftovers

Area: the Folio mail merge (289) and Folio fields (289) queue items: recipient selection in the merge wizard, page
styles of merged records, the merge source in DOCX, `MERGEFIELD` switches, page field numerals, reference number
formats, chapter levels, date pictures in ODF data styles, and a dialog control for field numerals. Files read:
`MailMerge.cs`, `MergePageStyles.cs`, `FieldContext.cs`, `FieldSources.cs`, `FieldUpdater.cs`, `SequenceNumerals.cs`,
`PageFields.cs`, `PageFrames.cs`, `FolioMergeWizard.cs`, `FolioMailMergeCommands.cs`, `FolioFieldOptions.cs`,
`FolioFieldCommands.cs`, `DocxFieldInstructions.cs`, `DocxWriter.cs` and `DocxReader.cs` settings, `OdtVocabulary.cs`,
`OdtDatePictures.cs`.

No build or test was run by this audit (coordinator rule for the cycle); the tests below run at the join.

## Defects

- **D1 (medium, UX)** The merge wizard offered only a from/to record range: a single unwanted record in the middle, or
  all records without an e-mail address, could not be left out, as Word's recipient list and the reference suite's
  wizard allow. Fixed: a recipients step lists every record with a checkbox, filters on one column (contains, equals,
  is not, is empty, is not empty), selects all or clears the listed ones and counts the recipients; the plan carries
  the chosen rows (`IMergeRecipients`). Merging with no recipient left stops with a message. Tests
  `FolioMergeWizardTests.Recipients_can_be_excluded_and_filtered`, `Round291MergeTests`.
- **D2 (medium, mail merge)** A merged record whose body switches to a page style with merge fields in its header kept
  the template style name, so every record after the first showed the first record's header there. Fixed:
  `IMergePageStyles.Adopt` returns the whole map of cloned styles and the engine moves every pasted paragraph of the
  record onto its clone. Test `Round291MergeTests.A_record_paragraph_switching_to_a_personal_page_style_uses_the_record_clone`.
- **D3 (medium, fidelity)** The merge source was declared lost in DOCX (DOCX028). Research: ECMA-376 `w:settings/
  w:mailMerge` holds `w:mainDocumentType`, `w:linkToQuery`, `w:dataType`, `w:connectString` and `w:query`, which Word
  uses to reconnect (it asks before running the query). Fixed: `DocxMailMergeCodec` writes form letters over ODBC
  with `DSN=<database>` and the query (`SELECT * FROM` the table for a table source) and reads them back; our
  connection id, local to the machine, goes in a reserved document variable that never reaches the document's user
  variables. DOCX028 is retired. Test `MergeParityTests.LOrigineDeiDatiAttraversaOdtEDocx`.
- **D4 (medium, fidelity)** `MERGEFIELD \* Upper|Lower|Caps|FirstCap` and `\# "picture"` were dropped, so merged
  letters lost the requested case and number shape. Fixed: kept in the field (case in `Format`, picture in `Value`),
  written back to DOCX, kept in ODT as `pt:case` / `pt:number-picture`, and applied by the merge, the preview and the
  wizard letter through the shared `IMergeValueFormat` (with the field's or document's culture). Tests
  `Round291FieldFormatTests`, `Round291MergeTests`.
- **D5 (medium, layout)** A page field's own numerals were ignored on pages whose style numbers in roman or letters,
  because the field only received the rendered page number. Fixed: headers and footers pass the page's number value
  to `IPageFieldText`. Test `Round291PageFieldShapeTests`.
- **D6 (minor, fidelity)** `REF \n` and `\w` and the ODF `number-no-context` / `number-all-superior` formats collapsed
  to `\r` / `number`. Fixed: two more reference formats, written and read in both formats, offered in the
  cross-reference dialog; no-context shows the innermost part of the label. Test `Round291FieldFormatTests`.
- **D7 (minor, fidelity)** Chapter fields kept level 1 only. Fixed: the field keeps its outline level (ODT
  `text:outline-level`, DOCX `STYLEREF "Heading N"` or `STYLEREF N`), the updater tracks the current heading of every
  level and the field shows the chapter at its level. Test `Round291FieldFormatTests`.
- **D8 (minor, ODF interop)** Date pictures wrote the .NET separators `/` and `:` as literal `number:text` slashes
  and colons whatever the document language (a German date field showed `20/11/2026` in other ODF applications), and
  era parts were not read. Fixed: separators are written as the document language shows them, and `number:era` maps
  to `g`/`gg` both ways. Quarter and week parts have no .NET picture equivalent and stay queued. Test
  `Round291DateFieldStyleTests`.
- **D9 (minor, UX)** No dialog control chose sequence or page field numerals. Fixed: `NumeralFieldOptions` gives page
  number fields a numbering choice, a new "Number sequence" field its name and numbering, and chapter fields their
  level. Test `Round291NumeralFieldOptionsTests`.

## Remaining (queued)

- Recipient filter with one condition only, no sorting, excluded records and filter not saved with the document.
- Merge fields have no `\@` date pictures.
- Body text page fields on non-Arabic pages still see the rendered number; no explicit Arabic override.
- Full-context reference numbers are the rendered list label, not composed from parent levels.
- ODF quarter and week-of-year date parts are not read.
- `STYLEREF` of a non-heading style becomes a level 1 chapter field.
- None of this opened in Word or the reference suite; the wizard step not seen live.
