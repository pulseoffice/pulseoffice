# Round 159: Ledger printing and page setup

Area: repeated print rows and columns, print ranges, manual page breaks, header and footer codes, scaling and
fit-to-pages, print titles and areas in the ODS and XLSX round trip. Closes the three minors left open by round 154.

## Defects fixed

| Severity | Defect | Fix | Test |
|---|---|---|---|
| Minor | Repeated print rows and columns are counts from the top: inserting rows or columns inside them did not grow them, deleting inside did not shrink them | `PrintTitlesPart` in the structure pipeline; the leading-band arithmetic shared with frozen panes moved to `LeadingBand` | `Round159PrintStructureTests` |
| Minor | Print ranges did not follow a range move (drag of cells) that contains them | `MovedPrintRangesPart` in the range-move pipeline (same sheet only) | `Round159PrintStructureTests` |
| Minor | Hyperlinks on shapes inside a group (and nested groups) were not rewritten on structure edits | `ChartReferenceRewrite` recurses into `ShapeGroupObject` children | `Round159PrintStructureTests` |
| Medium | XLSX: with `pageSetUpPr fitToPage` set, an absent `fitToWidth`/`fitToHeight` means one page (schema default 1); the reader took it as unconstrained, so Excel's "fit sheet on one page", which omits both, was lost entirely | The reader applies the schema default 1; an explicit 0 stays unconstrained | `Round159XlsxPrintTests` |
| Medium | XLSX: whole-column or whole-row print areas (`Sheet!$B:$C`, `Sheet!$3:$4`) were silently dropped on read | `XlsxMetadataMapper.PrintArea` reads whole-line references as full-height or full-width ranges | `Round159XlsxPrintTests` |

## Checked without defects

- Manual page breaks under row and column insertion and deletion (`BreaksPart`), in undo snapshots, in sheet copy, and
  in the ODS (break column and row styles) and XLSX (`rowBreaks`/`colBreaks`) round trip.
- Header and footer codes: the XLSX reader keeps `&&` and unknown codes, regions `&L`, `&C`, `&R`; ODS declares the
  loss of Excel formatting codes (`ODF273`).
- Scaling (10–400 %), page order, first page number, centring, grid lines and headings in XLSX.

## Open items

- XLSX print titles that do not start at the first row or column (`$3:$4`) are read as a count from the top (four
  rows), because the model holds counts; the same holds for ODS header rows. Changing the model to a band
  (start and count) is a larger change left for a later round.
