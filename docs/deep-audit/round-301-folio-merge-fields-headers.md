# Round 301: Folio mail merge, fields and headers leftovers (2026-11-20)

Area: the Folio mail merge and fields items queued by rounds 291 and 296, then Folio headers and footers (first, odd
and even, linked to previous, per section, fields inside, DOCX and ODT round trips). No builds or tests were run in
this round, per the audit cycle rule.

## Defects

Medium
- Excluded mail merge records were saved by record number, so a reordered, grown or shrunk source excluded other
  records. `MergeSelection.ExcludedRecords` now carries a content identity per excluded record (SHA-256 of the trimmed
  values, 12 bytes in hex), written as `excludedRecords` in the stored selection JSON (ODT settings item and DOCX
  document variable alike). `IMergeRecipients.Keyed` fills it from the wizard, `ExcludedRows` resolves it against the
  current source and falls back to the record numbers for selections saved before. Tests: `Round301MergeTests`.
- ODF date styles with `number:quarter` or `number:week-of-year` were not read (the field lost its format). The model's
  date pictures gain `Q`/`QQ` (quarter, short or long) and `W` (week of year by the culture's calendar rule), rendered
  by `FieldClockFormatter` and mapped both ways by `OdtDatePictures`; the DOCX picture keeps them unquoted so our
  reader takes them back. Tests: `Round301DatePartTests`.
- `STYLEREF` naming a localized heading style ("Überschrift 2", "Titolo 3", "Titre 1", "Título 1" and a few others
  Word writes in localized installs) was preserved as a foreign field instead of becoming a chapter field. Test rows in
  `Round296StyleReferenceTests`.

Minor
- A hidden blank ODF `style:header-left` or `style:header-first` (how producers say "same content on both sides")
  was read as an empty variant, so turning on different even or first pages later showed a blank header instead of
  the default one. It is now read as no variant; hidden variants with content (our own files keep them) still load.
  Test: `Round301HeaderVariantTests`.

## Decisions
- ODF page fields: `style:num-format="1"` keeps forcing Arabic and an absent attribute follows the page style, which is
  what ODF 1.3 says about the field attribute overriding the page style's format. Checking producers' files stays
  queued.

## Checked without defects
- DOCX section inheritance of header and footer references (linked to previous), first page through `w:titlePg`,
  even pages through `w:evenAndOddHeaders` with blank even variants for sections that lack them, watermark-only
  headers, ODT header, left and first variants written with `pt:same-as-default`.

## Open (queued)
- Content-hash identity misses records edited in the source; a chosen key column is not offered yet.
- Word shows `Q` and `W` date picture letters literally.
- `SECTIONPAGES` and `SECTION` header fields are preserved, not live.
