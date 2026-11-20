# Round 313: Folio list leftovers, section fields, columns and section breaks

## Fixed
- Medium: list number alignment (Word `w:lvlJc`, ODF `fo:text-align` on `style:list-level-properties`) was written as
  left and dropped on read. The model now carries `ListLevel.Alignment` (left, centre, right); layout shifts the label
  so a right aligned number ends, and a centred one is centred, at the label position. Both formats map it natively.
- Medium: Word `w:lvlRestart` was ignored. `ListLevel.RestartAfter` (null: after any higher level; 0: never; n: only
  after levels 1..n) drives the counters in `ListLabeling`; DOCX maps it natively, ODT through `pt:restart-after` (ODF 1.3
  has no per-level restart attribute).
- Medium: levels an `abstractNum` does not define read back as Arabic numbers; Word shows no number for them, so they
  now read as `NumberFormat.None`.
- Minor: `w:isLgl` was inserted before `w:lvlRestart`/`w:pStyle` positions out of schema order; it now sits before
  `w:suff`/`w:lvlText`.
- Medium: the shared list level editor gained "Followed by" (tab, space, nothing), "Number alignment" and "Restart
  numbering" (always, never, after level n), for Folio lists and outline numbering, in the five languages.
- Medium: insert commands for the SECTION and SECTIONPAGES fields (`folio.insert.field.sectionNumber`,
  `folio.insert.field.sectionPages`), with icons; the menu layout picks them up through the `folio.insert.field.` prefix.
- Medium: a Word `nextColumn` section break opened a new page with a converted page style; it now reads as a
  continuous break with a column break before the section's first paragraph.

## Audited without defects
Column count, equal and uneven widths, gaps and the separator flag in DOCX and ODT; unbalanced columns declared for
DOCX; odd and even page section starts in DOCX.

## Open
Queued in docs/audit-queue.md under Folio with "(313)".
