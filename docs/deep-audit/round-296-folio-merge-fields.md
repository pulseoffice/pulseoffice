# Round 296: Folio mail merge and fields leftovers

Area: the Folio mail merge (291) and Folio fields (291) queue items. Files read: `MailMerge.cs`, `MergeRecipients.cs`,
`MergeValueFormat.cs`, `FolioMergeWizard.cs`, `FolioMailMergeCommands.cs`, `FolioFieldOptions.cs`,
`DocxMailMergeCodec.cs`, `DocxFieldInstructions.cs`, `DocxReader.cs`/`DocxWriter.cs` settings, `OdtReader.cs`/`OdtWriter.cs`
settings, `OdtVocabulary.cs` field details, `PageFields.cs`, `FlowBuilder.cs`, `Paginator.cs`, `PaginationRun.cs`,
`LayoutEngine.cs`, `ListLabeling.cs`, `LayoutFieldFacts.cs`, `FieldSources.cs`.

## Defects

- **D1 (medium, mail merge UX)** The recipient filter held a single condition. Fixed: `MergeFilter` is a list of
  `MergeCondition`s, each joined to the previous one with and/or (and binds tighter, as in a SQL WHERE clause that Word
  builds from its filter dialog); new "less than" and "greater than" operators compare numbers, then dates, then text.
  The wizard's recipients step (`FolioMergeRecipientsStep`, split out of the wizard) edits up to six conditions.
  Tests `Round296MergeTests.Conditions_join_with_and_before_or`, `FolioMergeWizardTests`.
- **D2 (medium, mail merge UX)** Records could not be sorted. Fixed: `MergeSort` keys (two in the wizard, ascending or
  descending, numeric or date aware); `IMergeRecipients.Rows(data, selection)` returns the records in that order and
  the merge follows it. Test `Round296MergeTests.Records_sort_numerically_and_by_several_keys`.
- **D3 (medium, data loss)** The filter and the excluded records were lost when the document was closed. Fixed:
  `TextDocument.MergeSelection` (filter, sort, excluded record numbers), set by the merge command outside the undo
  history as a document setting; written as JSON by the shared `MergeSelectionText` into the ODT settings item
  `PulseMergeSelection` and the reserved DOCX document variable `PulseOffice.MergeSelection` (kept out of the user
  variables both ways). Research: ODF has no recipient-selection setting; Word keeps its filter and sort only inside
  the `w:query` SQL of `w:mailMerge` and its excluded records in an `odso` recipient data part bound to an Office data
  source, which our ODBC connections do not have; a document variable round-trips through Word untouched.
  Test `MergeParityTests.Recipient_selection_and_date_pictures_cross_odt_and_docx`.
- **D4 (medium, fidelity)** Merge fields had no date pictures: `MERGEFIELD \@` was dropped. Fixed:
  `FieldDetail.DatePicture`, read and written as `\@` through `IWordDatePictures`, kept in ODT as `pt:date-picture`,
  applied by the shared `IMergeValueFormat` (merge, preview, wizard letter), and editable in the field dialog ("Date
  format"). Tests `Round296MergeTests.Merge_values_take_a_date_picture`, `MergeParityTests`.
- **D5 (medium, layout)** Page fields with their own numerals in body text on roman or letter pages showed the
  rendered number. Fixed: the paginator passes the page's number value to `IFlowBuilder.PerPage`, which hands it to
  `IPageFieldText`, as headers and footers already did. Test `Round296BodyPageFieldTests`.
- **D6 (minor, UX/fidelity)** The page field dialog could not force Arabic numerals on a roman page. Fixed: the
  numerals choice starts with "As the page style" (no detail) and an explicit Arabic choice is kept as `1`; DOCX
  `PAGE \* Arabic` and ODT `style:num-format="1"` now read as explicit Arabic. Tests
  `Round296FieldOptionsTests`, `MergeParityTests.A_page_field_forced_to_arabic_stays_arabic_in_odt_and_docx`.
- **D7 (minor, fields)** A full-context reference number showed the rendered label when the list shows one level.
  Fixed: `ReferenceFieldSource` composes the innermost parts of the parent items of the same list (or of the outline
  numbering) joined with dots; a label that already shows several levels is kept. Test `Round296ReferenceContextTests`.
- **D8 (minor, fidelity)** `STYLEREF` of a non-heading style, or with the `\l`, `\p`, `\r`, `\t`, `\w` or `\s`
  switches, became a level 1 chapter field showing another text. Fixed: only heading references (`Heading N` or a
  level number) with `\n` at most become chapter fields; the rest is a preserved field that keeps its instruction and
  result. Test `Round296StyleReferenceTests`.

## Remaining (queued)

- ODF date styles with quarter or week-of-year parts are still not read (the .NET pictures of the model have no such
  parts; a model extension is needed).
- Excluded records are saved by record number: if the data source changes order or size, the saved exclusions point
  at other records (Word has the same limit without a key column).
- `STYLEREF` with a localized heading style name (for example a German "Überschrift 1") is now preserved, not a
  chapter field.
- Reading DOCX `PAGE \* Arabic` as explicit Arabic follows Word; ODF files whose producer always writes
  `style:num-format="1"` on page fields now show Arabic on roman pages, as the ODF specification says.
- None of these changes opened in Word or the reference suite; the new recipients step not seen live.
