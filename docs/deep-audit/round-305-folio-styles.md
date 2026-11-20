# Round 305: Folio merge keys, section fields, date pictures and styles

Audit round of the four-audit cycle (2026-11-20). Area: three Folio leftovers from round 301 and a fresh audit of
Folio paragraph and character styles. No builds or tests were run in the round, per the audit cycle rule; the tests
written here run at the join.

## Leftovers

- **Mail merge excluded records (medium).** Exclusions were keyed only by a hash of the whole record, so editing any
  value in the source forgot the exclusion. `IMergeRecipients.UniqueColumns` lists the columns whose values are
  distinct and non-empty in every record; the recipients step offers them in "Identify records by" (first unique
  column by default, "Whole record" as the alternative, hidden when no column qualifies). `MergeSelection.KeyColumn`
  is saved with the selection (`keyColumn` in its JSON); the exclusion list keeps both `key:<value>` entries and the
  record hashes, and `ExcludedRows` matches by key while the key column exists, by hash otherwise. A key column that is
  no longer unique when the selection is saved falls back to the hash. Texts in the five languages.
- **SECTION and SECTIONPAGES (medium).** New `TextFieldKind.SectionNumber` and `SectionPageCount`. DOCX reads and
  writes `SECTION` and `SECTIONPAGES` with their `\*` numeral switch, RTF the same keywords. The layout numbers sections
  as runs of consecutive pages with one page style (`ISectionPageRuns`), which is how DOCX sections come in, and the
  header and footer stories resolve the two fields per page (`IPageFieldText.Resolve` takes a `SectionPages`). ODF has
  no section fields: research found nothing in ODF 1.3 or the reference suite's extensions. `SECTIONPAGES` is written as
  `text:page-count` with our `pt:field="section-page-count"` marker, degrading to the document page count; `SECTION`
  is our own `pt:section-number` element, whose stored text other readers show as foreign-element content. Both keep
  `style:num-format`.
- **Quarter and week date pictures in DOCX (medium).** Word has no quarter or week-of-year picture letters; we wrote
  `Q` and `W` bare. The Word picture now quotes them (`'QQ' yyyy 'W'`), so Word shows letters by design, and our full
  picture travels in the field's `w:fldData` as `<pt:date-picture pt:format="…"/>` under the existing native marker;
  the reader restores it. A bare `Q` or `W` in a Word picture is now read as the literal letter Word shows.

## Styles audit

Checked: inheritance and cycle breaking on reading, new and updated styles from the selection, delete and rename with
references (parent, next, linked partner, list levels, notes, indexes), loading styles from another document, linked
styles applied to part of a paragraph, next style on Enter, list styles and the outline on heading styles, the DOCX
style map (ids, Word names, `basedOn`, `next`, `link`, aliases, semi-hidden), the ODT style writer and reader, and the
session snapshot converters.

Defects found and fixed:

- **Medium: linked styles drift apart.** Changing the character formatting of a linked paragraph style (style dialog
  or update from the selection) left its character partner unchanged, and the reverse; text formatted with the partner
  kept the old look. `StyleEditing.Change` and `StyleFromSelection.UpdateParagraphStyle`/`UpdateCharacterStyle` now copy
  the character part to the partner in the same undo step.
- **Medium: ODT linked styles unknown to other readers.** Only our `pt:linked-style-name` was written. The reference
  suite reads the link from its own extension attribute; we now write both and read either.
- **Minor: Word's "Default Paragraph Font".** The default character style of every Word file was imported as a custom
  character style and written back as such, and character styles based on it got it as parent. It is no longer
  imported; styles based on it have no parent.
- **Minor: dangling links.** A link to a style that does not exist, or whose partner links elsewhere, survived reading.
  Both readers now drop them.

Open (queued in docs/audit-queue.md): DOCX `customStyle`/`qFormat`/`uiPriority` on built-in names, a second numbering
list on heading styles, section fields for continuous sections, no insert command for section fields, MERGEFIELD date
pictures with quarter or week parts, Word verification of all of the above.
