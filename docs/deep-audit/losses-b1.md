# Block B1: hidden first-page and even-page headers (DOCX045, ODT007)

Date: 2026-11-05. Plan: `docs/declared-losses-analysis.md`, block B1.

## Specifications checked

- ODF 1.3, `schema/odf1.3/OpenDocument-v1.3-schema.rng` in the reference clone: `style:header-first` (line 11798) and
  `style:header-left` (line 11844), with the footer twins, all carry `common-style-header-footer-attlist`
  (line 2449), whose only attribute is the optional boolean `style:display` (part 1 §16.11-16.13, §19.480). A variant
  with `display="false"` keeps its content without showing it; the reference suite writes shared first/left
  headers exactly so.
- ECMA-376 Part 1 §17.10.5 `headerReference` / `footerReference`: `type` first/even/default is allowed in any `sectPr`
  regardless of the switches. Whether they show is ruled by `w:titlePg` (§17.10.6, per section) and by the
  document-wide `w:evenAndOddHeaders` (§17.15.1.35). Corpus check: a corpus file has
  first and even references with neither switch.

## Model

`PageStyle` already kept `HeaderFooterContent.First`/`Even` apart from `FirstPageDifferent`/`EvenPagesDifferent`, and
the DOCX reader already filled them from references without the switches. Only the writers dropped them.

## Changes

- `OdtStylesWriting`: writes `-first`/`-left` variants whenever the content exists; `style:display="false"` when the
  switch is off or the region is off.
- `OdtStylesReading`: a variant with `style:display="false"` keeps its content but no longer turns the switch on
  (this also fixed reading of foreign files with shared headers, which turned "different first/even" on).
- `DocxSectionCodec`: writes `first` references without `titlePg` and `even` references without
  `evenAndOddHeaders`; hidden variants are written even when the header itself is off.
- `UnusedHeaderVariants`: `EvenPagesShownInDocument`, `HiddenEvenShownByDocumentSetting`. The only remaining DOCX
  loss: a page style with a hidden even header in a document where another page style shows even headers (Word's
  switch is document-wide), still declared as DOCX045 with a new text. ODT007 removed.

## Tests

`tests/PulseOffice.Formats.OpenXml.Tests/UnusedHeaderVariantFidelityTests.cs`: no declaration; DOCX->DOCX, ODT->ODT,
DOCX->ODT->DOCX round trips keep hidden first header and even footer with the switches off; switch on survives;
DOCX045 remains for the mixed case; ODT writes `display="false"`. Before the change the round trips lost the
variants (the writers emitted them only with the switch on). Formats.OpenXml.Tests: 2183 passed, 1 failed
(`DocxManyRunsTests` timing on a loaded machine, unrelated).

## Corpus

`corpus-audit one`: three corpus files, a corpus file: LOSS 0,
no DOCX045/ODT007. Estimated rows removed: about 68 (rows carrying only these codes) of 102.
