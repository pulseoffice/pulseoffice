# Declared losses in the corpus audit: analysis and remediation plan

Source: last `tools/corpus-audit` run over the external corpus (5016 files, 9948 copies written):
**LOSS 0, DECL 529**. A DECL row is one (file, target format, feature) whose element count fell and whose drop is
covered by a diagnostic (`DeclarationMatcher` matches the diagnostic message against keywords of the feature; one
row may carry several codes). Read-side codes appear **twice** (once per target, same format and twin format), so
the 529 rows correspond to about 290 distinct (file, cause) pairs.

Classification:

- **(a)** deliberate choice (user decision);
- **(b)** the target format cannot represent it, checked against ODF 1.3 (schema `OpenDocument-v1.3-schema.rng`
  in the reference clone) and ECMA-376 5th ed. Part 1 (WordprocessingML/DrawingML/SpreadsheetML) and Part 3
  (`mc:Ignorable`, `mc:AlternateContent`, `extLst`). A code is (b) only after excluding an equivalent element,
  a combination, an approximation without data loss, a known extension, or a `urn:pulseoffice` ignorable extension;
- **(c)** feature not yet supported by PulseOffice (one or both formats can carry it);
- **(d)** over-declared / tool false positive / misattribution (the count falls for another reason, or the
  content is still there in another form, or the source file is itself broken).

## 1. Totals by module and pair

| Module | Pairs | DECL rows |
|---|---|---|
| Folio | .docx->.docx 158, .docx->.odt 207, .odt->.docx 105, .odt->.odt 27 | 497 |
| Ledger | .ods->.ods 46, .ods->.xlsx 12, .xlsx->.xlsx 7, .xlsx->.ods 7 | 72 (of which 35 are ODF041) |
| Deck | .odp->.odp 5, .odp->.pptx 9 | 14 |

(Rows with several codes are counted once per pair; module totals overlap the 529 because Folio rows dominate.)

## 2. Codes

Counts are rows carrying the code (a row with two codes counts for both). "Emitted at" is the line that adds the
diagnostic; messages are defined next to it (the codes are not in `en.json`: format diagnostics are English-only
developer texts, so the `diagnostic.<code>` keys do not exist).

| Code | Rows | Pairs | Features | Emitted at | Message (short) | Class |
|---|---|---|---|---|---|---|
| DOCX102 | 124 | docx->docx 62, docx->odt 62 | shapes 100, images 20, frames 2, groups 2 | `Formats.OpenXml/Wordprocessing/DocxVmlReader.cs:20,40,57`; `DocxDrawingCodec.cs:527,654,688`; `DocxInlineReader.cs:503` | Some drawings or embedded objects could not be imported | (c) |
| DOCX123 | 57 | docx->docx 25, docx->odt 32 | fields | `DocxInlineReader.cs:179,421,635` | Unhandled Word fields imported as their last text | (c) |
| DOCX171 | 53 | odt->docx | fields 29, tables 13, images 6, links 5 | `DocxWriter.cs:155` | Unused page styles (and their headers/footers) not written to DOCX | (b) with alternative |
| ODF041 | 51 | ods->ods 35, odt->odt 16 | macros | `Formats.Odf/OdfMacroMapper.cs:91` | Basic macros discarded | (a) |
| DOCX045 | 51 | docx->docx | mixed (header content) | `DocxWriter.cs:153` | First/even headers that the page style does not show not written to DOCX | (c), not (b) |
| ODT007 | 51 | docx->odt | mixed (header content) | `Formats.Odf/Odt/OdtWriter.cs:444` | Same, not written to ODT | (c), not (b) |
| DOCX040 | 22 | odt->docx | images 10, frames 8, shapes 3, groups 1 | `DocxWriter.cs:149` | Frame/graphic styles lost in DOCX | (d) misattribution + (b) with alternative |
| DOCX039 | 20 | odt->docx | lists | `DocxWriter.cs:147` | Character style of list numbers not written | (d) tool false positive |
| ODF082 | 13 | odt->docx 7, odt->odt 6 | fields | `Formats.Odf/Odt/OdtBodyReading.cs:403` | Unhandled ODF fields imported as text | (c) |
| ODF081 | 10 | ods->ods 5, ods->xlsx 5 | filters | `Formats.Odf/OdsReader.cs:168` | Standard filters on database ranges / pivot sources not kept | (c) |
| DOCX175 | 10 | docx->docx 5, docx->odt 5 | images | `DocxInlineReader.cs:316` | Pictures inside tracked deletions not imported | (c) |
| DOCX126 | 8 | same files as DOCX175 | images | `DocxReader.cs:161` | Images inside tracked deletions not imported | (c) (duplicate of DOCX175) |
| DOCX113 | 10 | docx->docx 5, docx->odt 5 | comments | `DocxReader.cs:153` | Unanchored comments not imported | (d) invisible source content |
| DOCX151 | 10 | docx->docx 5, docx->odt 5 | fields | `DocxInlineReader.cs:634,636` | Fields spanning paragraphs/tables imported as text | (c) |
| DOCX125 | 10 | docx->docx 5, docx->odt 5 | shapes 4, comments 2, images 2, fields 2 | `DocxReader.cs` `Defer` | Headers of mid-page continuous sections not imported (B11: solved except sections holding only tables) | (c) |
| ODF176 | 8 | odt->docx 4, odt->odt 4 | images | `OdtBodyReading.cs:694,702` | Picture file missing from the package | (d) broken source |
| XLSX081 | 6 | xlsx->ods 3, xlsx->xlsx 3 | pivots | `Formats.OpenXml/XlsxReader.cs:209` | Pivot tables on external sources kept as values | (c) |
| XLSX180 | 6 | xlsx->ods 3, xlsx->xlsx 3 | filters | `XlsxReader.cs:112` (declared), applied in sheet reading | One filter per sheet | (c) |
| PPTX020 | 6 | odp->pptx | links 3, shapes 1, images 1, groups 1 | `Presentation/Writing/PresentationWriters.cs:288` | Graphic styles have no OOXML equivalent | (d) misattribution + (b) with alternative |
| ODP180 | 6 | odp->odp 3, odp->pptx 3 | animations 4, images 2 | `Formats.Odf/Presentation/Reading/OdpMissingObjects.cs:13` | Objects whose data is missing | (d) broken source |
| DOCX170 | 6 | docx->docx 3, docx->odt 3 | links | `DocxInlineReader.cs:160,435` | Hyperlinks without text not imported | (d) invisible source content |
| DOCX016 | 5 | odt->docx | lists | `DocxWriter.cs:93` | No unnumbered entries inside a Word list | (b) with alternative |
| ODF191 | 4 | ods->ods 2, ods->xlsx 2 | merges | `Formats.Odf/Spreadsheet/SheetPartReaders.cs:123` | Overlapping merges | (d) invalid source |
| ODF192 | 4 | ods->ods 2, ods->xlsx 2 | images | `Spreadsheet/OdsLinkedPictures.cs:11`, `OdsReader.cs:132` | Linked/missing pictures not imported | (c) (linked) / (d) (missing) |
| ODF193 | 4 | ods->ods 2, ods->xlsx 2 | groups 2, images 2 | `Spreadsheet/OdsGroupedObjects.cs:9`, `OdsReader.cs:133` | Pictures/charts inside groups not imported in Ledger | (c) |
| XLSX059 | 1 | ods->xlsx | images | `Formats.OpenXml/XlsxFormat.cs:131` | Non-shape objects in XLSX groups lost | (c), not (b) |
| ODP102 | 4 | odp->odp 2, odp->pptx 2 | animations 2, groups 2 | `Formats.Odf/OdpReader.cs:211` | 3D scenes not imported | (c) |
| DOCX121 | 4 | docx->docx 2, docx->odt 2 | tables | `DocxReader.cs:158` | Tracked row/cell changes imported as accepted | (c); **B9: rows read and written (`TableRow.ChangeId`), narrowed to single-cell changes** |
| DOCX180 | 4 | docx->docx 2, docx->odt 2 | charts | `DocxDrawingCodec.cs:495,681` | Charts in groups not imported | (c) |
| DOCX181 | 4 | docx->docx 2, docx->odt 2 | groups | `DocxDrawingCodec.cs:682` | Groups with tables in text boxes are ungrouped | (c) |
| DOCX124 | 2 | same file as DOCX181 | groups | `DocxDrawingCodec.cs:259` | Shapes holding tables become plain text boxes | (c) |
| DOCX025 | 3 | odt->docx 2, docx->docx 1 | fields | `DocxWriter.cs:109` | Index settings Word lacks | (b) partly, with alternative |
| ODF178 | 3 | odt->docx | lists | `Odt/OdtStylesReading.cs:309` | Picture bullets become character bullets | (c) |
| DOCX173 | 2 | docx->docx 1, docx->odt 1 | images | `DocxReader.cs:130` | Word picture bullets become character bullets | (c) |
| ODF080 | 2 | ods->ods, ods->xlsx | pivots | `OdsReader.cs:165` | Pivot on database/external source kept as values | (c) |
| XLSX089 | 2 | xlsx->ods, xlsx->xlsx | shapes | `XlsxReader.cs:182` | ActiveX controls dropped | (a) |
| ODF090 | 2 | odp->odp, odp->pptx | images | `OdpReader.cs:204` | Non-placeholder text boxes on masters not imported | (c) |
| ODF175 | 2 | odt->docx, odt->odt | links | `OdtBodyReading.cs:320` | Links without text or target | (d) invisible source content |
| DOCX161 | 2 | docx->docx, docx->odt | fields | `DocxInlineReader.cs:418` | Fields nested in a field result | (c) |
| DOCX160 | 2 | docx->docx, docx->odt | fields | `DocxInlineReader.cs:417` | Fields nested in an instruction | (c) |
| ODF190 | 2 | odt->docx, odt->odt | shapes | `Formats.Odf/OdfDimensionLines.cs:8-10` | Dimension lines become plain lines | (c) |
| DOCX176 | 2 | docx->docx, docx->odt | links | `DocxReader.cs:134` | Hyperlinks on grouped shapes | (c) |
| DOCX117 | 2 | docx->docx, docx->odt | revisions | `DocxReader.cs:154` | Tracked changes in field instructions | (c); B9: left declared, a field keeps one instruction string |
| DOCX122 | 2 | docx->docx, docx->odt | revisions | `DocxReader.cs:159` | Deleted paragraph marks imported as accepted | (c); **B9: removed** |
| DOCX182 | 2 | docx->docx, docx->odt | notes | `DocxInlineReader.cs:521` | Note reference whose note is missing | (d) broken source |
| DOCX001 | 1 | odt->docx | tables | `DocxWriter.cs:38` | Multi-paragraph tracked deletions on one line | (c); **B9: written as deleted paragraph marks, narrowed to deletions holding tables or sections** |
| DOCX031 | 1 | odt->docx | tables | `DocxWriter.cs:62` | Table cell number formats | (b) with alternative |
| DOCX003 | 1 | odt->docx | links | `DocxWriter.cs:56` | Section names, protection, links | (b) partly; (c) for protection |
| DOCX034 | 1 | odt->docx | fields | `DocxWriter.cs:111` | Conditional hidden paragraphs | (b) with alternative |
| DOCX020 | 1 | odt->docx | frames | `DocxWriter.cs:101` | Wrap limited to anchor paragraph | (b) with alternative |
| DOCX002 | 1 | odt->docx | groups | `DocxWriter.cs:50` | Connectors, linked files, groups with charts | (c), not (b) |
| DOCX195 | 1 | odt->docx | fields | `DocxWriter.cs:160` | Fields inside tracked deletions written as text | (c), not (b) |
| DOCX008 | 1 | odt->docx | fields | `DocxWriter.cs:124` | Range index entries become point XE | (c), not (b) |

Corpus examples (paths relative to the reference clone):

| Code | Examples |
|---|---|
| DOCX102 | a corpus file (`v:oval`), a corpus file (`v:line`), a corpus file |
| DOCX123 | three corpus files |
| DOCX171 | three corpus files |
| ODF041 | three sample spreadsheets with Basic macros from the reference suite's test corpus |
| DOCX045 / ODT007 | three corpus files |
| DOCX040 | three corpus files |
| DOCX039 | three corpus files |
| ODF082 | three corpus files |
| ODF081 | three corpus files |
| DOCX175/126 | three corpus files |
| DOCX113 | two corpus files |
| DOCX151 | two corpus files |
| DOCX125 | two corpus files |
| XLSX081 | two corpus files |
| XLSX180 | two corpus files |
| PPTX020 | three corpus files |
| ODP180 / ODF090 | two corpus files |
| ODF193 / XLSX059 | two corpus files |
| others | see `res.tsv` of the run; one or two files each |

## 3. Findings per code and remedies

### Deliberate (a): not remediable

| Code | Rows | Why |
|---|---|---|
| ODF041 | 51 | Basic is not run (decision 12/13: JS and Python only, sandboxed). Keeping the Basic library opaque would be possible in ODF (`Basic/` package entries are untouched files), but the signature over them would no longer match the edited document and the code would be dead: the user's choice is to discard and declare. Not remediable by decision. |
| XLSX089 | 2 | ActiveX: excluded by user decision ("no ActiveX"). Not remediable. |

### Broken or invisible source content (d): not remediable, or declared correctly

| Code | Rows | Why |
|---|---|---|
| ODF176 | 8 | The picture file is missing from the source package: there is no data to carry. Correct declaration. |
| ODP180 | 6 | Same, in presentations. |
| DOCX182 | 2 | The note text is missing from the file. |
| ODF191 | 4 | Overlapping merges are invalid in both formats (ODF 1.3 §9.1.4 `table:number-columns-spanned` covers cells; ECMA-376 §18.3.1.55 `mergeCell` forbids overlap in practice). |
| DOCX113 | 10 | Comments with no anchor in the text are not shown by Word; ECMA-376 §17.13.4 requires `commentReference` for display. Optional: anchor them at the start of the document (small, `DocxReader.cs`), but that would show content the author never saw. Recommended to leave declared. |
| DOCX170, ODF175 | 8 | Links with no text: invisible and without a target. Correctly dropped. |

These rows (91 with the (a) rows) are the irreducible floor.

### DOCX102 (124 rows): stand-alone VML shapes are not read (c)

Probe of the 62 files: 55 contain `w:pict`; the dominant cases are `v:rect`, `v:oval`, `v:roundrect`, `v:line`,
`v:shape` with a path, WordArt (`v:textpath`) and ink, **without** `v:textbox`. `DocxVmlShapes.Boxed`
(`DocxVmlShapes.cs:83`) accepts a VML member only if it holds a text box, so everything else falls to the picture
path and is dropped with DOCX102. `DocxVmlShapes.Single`/`Shape` already map rect/oval/line/presets (spt) to
`ShapeObject`. Smaller groups: `gd:picture` with an invalid or missing image link (`FileWithInvalidImageLink.docx`,
a corpus file: (d), correct), `lockedCanvas` (`group-shape-rotation.docx`), an empty `wpg:wgp`
(`empty_group.docx`, (d)), legacy `w:object`/`w:control` (a corpus file, ActiveX: (a)).

Remedy: let `Boxed` (rename to `Drawable`) accept any VML member that is filled or stroked; for `v:shape` with
`path`/`v:path` map the VML path to a custom geometry (ECMA-376 Part 4 §19.1.2.14 `path` grammar) via the existing
custom-geometry support; `v:textpath` (Part 4 §19.1.2.23; §19.1.2.22 is `textbox`) to a text-art shape; ink (`o:ink`) to a polyline. Files:
`DocxVmlShapes.cs`, `DocxVmlReader.cs`, a new `VmlPathParser` service. **Medium.** Removes about 100 rows.

### DOCX045 / ODT007 (51 + 51 rows): hidden first/even headers are representable in both formats (c) — done in B1

The model keeps first-page and even-page headers whose page style has "different first/even" off (they come from
DOCX files with `w:headerReference w:type="first"` and no `w:titlePg`). Both writers drop them, but:

- **OOXML**: ECMA-376 Part 1 §17.10.5 `headerReference` allows `type="first"` and `type="even"` in any
  `sectPr`; whether they are shown is governed separately by `w:titlePg` (§17.10.6) and the document setting
  `w:evenAndOddHeaders` (§17.15.1.35). Writing the reference without `titlePg` is exactly what the source file did.
- **ODF 1.3**: `style:header-first` / `style:footer-first` and `style:header-left` / `style:footer-left` exist
  (§16.11-16.13, present in `OpenDocument-v1.3-schema.rng`) and carry `style:display="false"` (§19.480), which
  keeps the content without showing it.

Remedy: write the hidden variants with `display="false"` in ODT (`OdtWriter`, master-page writer) and as plain
references in DOCX without setting `titlePg`/`evenAndOddHeaders`; read `style:display="false"` back into the
"kept but hidden" model state. Remove DOCX045/ODT007. **Small to medium.** Removes about 68 rows alone, more
together with DOCX125.

### DOCX171 (53 rows): unused page styles in DOCX (b) with alternative

True format limit: WordprocessingML has no page styles, only `sectPr` (§17.6.17), and a header part referenced by no
`sectPr` is dropped by Word. Part of the content is never visible in the source either (unused page style), so a
share is (d). Alternative: an ignorable extension. Write the unused page styles in an `urn:pulseoffice:page-styles`
part (custom XML part, ECMA-376 Part 1 §15.2.5, or an element in `settings.xml` declared in `mc:Ignorable`,
Part 3 §10.2.1) whose entries carry the page layout and relationship ids of normal header/footer parts; Word ignores
them, PulseOffice restores the page styles. Files: `DocxWriter.cs:155` and the page-style writer, `DocxReader`
section reading. **Medium.** Removes about 40 rows alone, 60 with the companion codes of block B5.

### DOCX040 (22 rows): misattribution hiding a real loss (d) plus (b) with alternative

The message speaks about frame **styles**, but the rows show whole objects disappearing. Verified on
a corpus file (3 page-anchored text frames, empty body): the ODT copy keeps 3 frames,
the DOCX copy contains only `<w:p/>` and `sectPr`, no drawing at all. Page-anchored objects are queued in
`context.PendingPageObjects` (`DocxWriter.cs:331`) and evidently never flushed when the body has no ordinary
paragraph. This is a real loss that the keyword matcher accepts because "frame/graphic/object" occur in the
DOCX040 text. Remedy: (1) fix the flush of pending page objects (always emit them in the first written paragraph,
creating one if needed); inspect the other 21 rows with `corpus-audit one` and `CORPUS_AUDIT_KEEP`. (2) The style
loss itself: DrawingML has no graphic styles (§20.1.4 has theme style matrices only); keep the style name in
`wp:docPr/a:extLst` with an `urn:pulseoffice` URI (§20.1.2.2.15 `extLst`), restored on reading. **Medium.**
(3) Tighten `DeclarationMatcher`: an Info diagnostic about *styles* should not cover an element count dropping to
zero. Removes 22 rows (and turns any residue into honest LOSS).

### DOCX039 (20 rows): tool false positive (d)

Verified on a corpus file (lists 36 -> 0): in the DOCX copy the list membership is in the paragraph styles
(`w:numPr` inside `styles.xml`, paragraphs use those styles), which is the normal Word form; the rule
`Rule("lists", "w:numPr", ...)` in `tools/corpus-audit/FeatureRules.cs:292` counts only `numPr` in paragraphs.
Remedy: count paragraphs whose style (with `basedOn` chain) carries a `numPr` with `numId != 0`. The message itself
is (b) with an alternative: the number's character formatting can be written as `w:lvl/w:rPr` (§17.9.24), which
loses only the style link; keep the name in an ignorable `urn:pulseoffice` attribute. **Small.** Removes 20 rows.

### DOCX123 (57) and ODF082 (13): unknown fields (c)

Instructions seen: ADDRESSBLOCK, EQ, ADDIN, BIDIOUTLINE, CITATION, BIBLIOGRAPHY, ADVANCE, EDITTIME, ASK,
PRINTDATE, USERINITIALS, USERNAME, MACROBUTTON, LISTNUM, CONTROL. Many have ODF equivalents (ODF 1.3 §7.3-7.7:
`text:editing-duration`, `text:print-date`, `text:author-name`/`text:author-initials`, `text:bibliography-mark`,
`text:execute-macro`, `text:placeholder`, `text:variable-input`). In the other direction, ODF input fields,
placeholders, drop-downs (`text:drop-down`), page variables and formulas map to FILLIN/MACROBUTTON, FORMDROPDOWN,
`=` fields. Remedy: (1) add those kinds to `TextFieldKind` and to the two mappers (`DocxFieldInstructions.cs`,
`OdtBodyReading.cs:403` and the ODT field writer); (2) a generic "opaque field" carrying the raw instruction and
the last result, written back as `fldChar` in DOCX and, in ODT, as the result text wrapped in `text:span` with an
`urn:pulseoffice` attribute holding the instruction (ignorable foreign attribute, ODF 1.3 Part 3 §3.17), so it
survives the round trip without being a lie in the other suite. **Medium.** Removes about 70 rows.

### Write-side Folio codes (block B5)

| Code | Spec check | Remedy | Difficulty |
|---|---|---|---|
| DOCX016 | Word has no unnumbered list entry; a level with `numFmt="none"` (§17.9.17) would count the entry. | Write the paragraph with `numPr` of an extra level `numFmt none`, `lvlText ""`, plus an `urn:pulseoffice` attribute so it is read back as a list header. | Small |
| DOCX031 | `w:tc` has no number format. | Keep value and format in `w:tcPr/…` extension? Not allowed; use a `w:customXml`-free approach: an ignorable `urn:pulseoffice` attribute on `w:tc` (Part 3 §10.2.1). | Small |
| DOCX003 | `sectPr` has no name/condition; protection exists as `w:formProt` (§17.6.6). | Write `formProt`; names/conditions as ignorable attributes. | Small |
| DOCX034 | Word has `w:vanish` but no condition. | Approximate with an IF field wrapping the paragraph text plus an ignorable attribute holding the condition. | Medium |
| DOCX020 | No anchor-paragraph-only wrap. | Ignorable attribute on `wp:anchor`. | Small |
| DOCX001 | A deletion that spans paragraphs is a deleted paragraph mark (`w:pPr/w:rPr/w:del`, ECMA-376 Part 1 §17.13.5.15, not §17.13.5.14 which is deleted run content). | **Done in B9**: `DocxParagraphMarkSplits` splits the paragraph. Left: deleted tables and sections inside a deletion. | Medium |
| DOCX002 | Connectors exist (`wps:wsp` with `wps:cNvCnPr`, §20.4/Word 2010 schema), linked pictures via `r:link` (§20.1.8.13), groups with charts via `wpg:graphicFrame`. | Write them. | Medium |
| DOCX195 | Deleted field codes: `w:delInstrText` (§17.16.13). | Write fields inside deletions with `delInstrText`. | Small |
| DOCX008 | XE has the `\r` bookmark-range switch (§17.16.5.79). | Write a bookmark and `XE \r`. | Small |
| DOCX025 | TOC/INDEX switches cover levels and most options (§17.16.5.68, .35); bibliography order and user-defined indexes not. | Write the switches that exist, the rest in an ignorable attribute. | Small |

### Ledger codes (block B6)

| Code | Spec check | Remedy | Difficulty |
|---|---|---|---|
| ODF081 | ODF `table:database-range` + `table:filter` (§9.4.1, §9.5.2); XLSX `autoFilter` on a defined name or a table (§18.3.1.2) and `_xlnm._FilterDatabase`. | Model filters per database range, not per sheet. Files: `OdsReader.cs:168`, the ODF database-range mapper, `XlsxReader`/table writer. | Medium |
| XLSX180 | ODF supports several database ranges with filters per sheet; XLSX supports one autoFilter per sheet **plus** one per table. | Same model change as ODF081 (filters per range/table). | Medium (shared with ODF081) |
| ODF192 | External links are allowed in both: ODF `xlink:href` outside the package (§19.910), DrawingML `a:blip r:link` (§20.1.8.13). Missing data part is (d). | Keep linked pictures as linked; drop only missing ones. Files: `OdsLinkedPictures.cs`, drawing mappers. | Medium |
| ODF193, XLSX059 | `draw:g` may hold any frame (§10.3.15); `xdr:grpSp` may hold `xdr:pic` and `xdr:graphicFrame` (§20.5.2.17), so XLSX059 is **not** (b). | Allow pictures, charts and controls as group members in the Ledger drawing model and both mappers. | Medium |
| ODF080, XLSX081 | Both formats carry external pivot sources. ODF: `table:data-pilot-table` takes one of `table:database-source-sql`, `-table`, `-query`, `table:source-service` or `table:source-cell-range` (choice verified in the ODF 1.3 RELAX NG schema, `define table-data-pilot-table`; the database-source elements are the ones of database ranges, ODF 1.2 Part 1 §9.4.3-9.4.5, `table:source-service` §9.6.3 — section numbers from the 1.2 layout, the schema is the authority). ODF has no record cache. XLSX: `pivotCacheDefinition/cacheSource` with `type="external"` + `connectionId` into `connections.xml` (`connection/dbPr` with `command`, `commandType`), or `type="worksheet"` whose `worksheetSource r:id` names an `externalLinkPath` relationship (another workbook); records in `pivotCacheRecords` (ECMA-376 Part 1 §18.10, pivot cache parts; element and attribute names verified on the Open XML SDK types, the earlier "§18.10.1.67" could not be checked offline and is withdrawn). | **Done in block B6b**: cached-record pivot source, refresh through the connection directory. | Large |

### Deck codes

| Code | Spec check | Remedy | Difficulty |
|---|---|---|---|
| PPTX020 | No graphic styles in PresentationML. But rows show links/shapes/images dropping in odp->pptx (`badPercentageValue.odp`, two corpus files): a misattribution like DOCX040. | Inspect with `corpus-audit one`; fix the real drop (likely click actions or grouped pictures); keep the style name in `p:cNvPr/a:extLst` with an `urn:pulseoffice` URI. | Small to medium |
| ODF090 | Master pages may hold any shape in both formats (ODF §10.2.4 `style:master-page`; PresentationML `p:sldMaster/p:cSld/p:spTree`). | Read and write non-placeholder text boxes of masters. `OdpReader.cs:204`. | Small |
| ODP102 | `dr3d:scene` (§10.5) has no PresentationML twin; DrawingML `a:scene3d`/`a:sp3d` (§20.1.5) only extrude 2D shapes. ODP->ODP is fully representable (c); ODP->PPTX can carry a rendered picture plus the scene XML in `mc:AlternateContent` with an ignorable `urn:pulseoffice` choice. | Keep 3D scenes as opaque preserved XML with a rendered fallback. | Large |

### Folio reading codes of block B7/B8

| Code | Spec check | Remedy | Difficulty |
|---|---|---|---|
| DOCX175/DOCX126 | `w:del` may contain `w:drawing` (§17.13.5.14); ODF `text:changed-region` deletion holds any content, frames included (§5.5.7). | Model deleted runs with objects; one diagnostic code is enough (they fire on the same files). | Medium |
| DOCX121 | `w:trPr/w:ins` (§17.13.5.18) and `w:trPr/w:del` (§17.13.5.12); ODF 1.3 has no row-level change (§5.5 tracks text regions only), so ODT carries the row change as `pt:change-id` on `table:table-row` pointing to a `text:changed-region` (foreign attribute, Part 3 §3.17). | **Done in B9** for rows. Left: `w:cellIns`/`w:cellDel`/`w:cellMerge` (§17.13.5.2, .1, .3). | Medium |
| DOCX117, DOCX122 | `w:delInstrText` (§17.16.13); deleted paragraph mark (§17.13.5.15). ODF: a paragraph merge is a `text:deletion` holding several `text:p` (§5.5.5) at a `text:change` point (§5.5.7). | DOCX122 **done in B9** (removed). DOCX117 left: partial instruction changes need marks inside field instructions. | Medium |
| DOCX176 | `a:hlinkClick` on `wps:cNvPr` inside groups (§20.1.2.2.23). | Read hyperlinks on group members. | Small |
| DOCX180 | `wpg:graphicFrame` in groups; ODF `draw:g` with chart frames. | Allow charts as group members. | Medium |
| DOCX181, DOCX124 | Text boxes with tables in groups and in non-rectangular shapes are representable in both. | Support tables in group-member stories. | Large |
| ODF178, DOCX173 | `text:list-level-style-image` (§16.32); `w:numPicBullet` + `w:lvlPicBulletId` (§17.9.21). | Picture bullets in the list model. | Medium |
| ODF190 | `draw:measure` (§10.3.12). No OOXML twin: approximate as group of lines and text, original in `mc:AlternateContent` ignorable choice. | Model dimension lines. | Medium |
| DOCX151, DOCX160, DOCX161 | Fields spanning paragraphs and nested fields are legal OOXML (§17.16.18); ODF has no nested fields, but text inside a field result can be kept with `text:span`. | Model nested fields/complex field ranges. | Large |
| DOCX125 | Headers of a continuous section apply from the next page (§17.6.22). ODF: a page-style change at the next page break. | Model "header change at next page". | Large |

## 4. Summary and estimates

| Class | Codes | Rows |
|---|---|---|
| (a) not remediable | ODF041, XLSX089 | 53 |
| (d) correct, not remediable (broken or invisible source) | ODF176, ODP180, DOCX182, ODF191, DOCX113, DOCX170, ODF175 | 38 |
| remediable ((c), (b) with alternative, (d) tool/misattribution) | all others | **438** |

No code was left as (b) without an alternative: every apparent format limit has either an equivalent element or an
ignorable `urn:pulseoffice` extension (ECMA-376 Part 3 `mc:Ignorable`/`extLst`; ODF foreign attributes and
elements, ODF 1.3 Part 3 §3.17).

## 5. Work order (at most two agents at a time)

Cumulative rows eliminated are computed on `res.tsv` (a row disappears only when all its codes are remedied).

| Block | Agents | Content | Difficulty | Cumulative rows eliminated |
|---|---|---|---|---|
| B1 | 1 | **Done (2026-11-05)**, see `docs/deep-audit/losses-b1.md`. DOCX045/ODT007 hidden first/even headers in both writers; ODT007 removed, DOCX045 narrowed to hidden even headers beside a page style that shows its own | Small-medium | 68 |
| B2 (done 2026-11-05) | 1 (parallel to B1) | Tool fixes (`FeatureRules` list counting via styles; `DeclarationMatcher` stricter on Info style messages), DOCX040 page-object flush, PPTX020 misattributed drops | Small-medium | 97 |
| B3 | 1 | **Done (2026-11-05)**, see `docs/deep-audit/losses-b3.md`. DOCX102 stand-alone VML shapes, paths, WordArt: DOCX102 rows 124 -> 20 (left: broken image links, DrawingML `lockedCanvas`/empty groups, `w:object`) | Medium | 215 |
| B4 (done 2026-11-05) | 1 (parallel to B3) | **Done**, see `docs/deep-audit/losses-b4.md`: preserved fields both ways, DOCX123 and ODF082 removed. Unknown fields both ways (DOCX123, ODF082, DOCX025, DOCX008, DOCX195); B4b: DOCX008 and DOCX025 removed, DOCX195 narrowed, see `docs/deep-audit/losses-b4b.md` | Medium | 285 |
| B5 | 1 | **Done (2026-11-05)**, see `docs/deep-audit/losses-b5.md`: DOCX171 unused page styles in a `urn:pulseoffice:page-styles` custom XML part, DOCX020 in the `wp:docPr` anchor extension; both removed. DOCX016/031/003/034/001/002 left declared (need a paragraph/cell carrier or new writers) | Medium | 345 |
| B6 | 1 (parallel to B5) | **Done (2026-11-05)**, see `docs/deep-audit/losses-b6.md`; external pivots in block B6b, `docs/deep-audit/losses-b6b.md`. Ledger: filters per range (ODF081, XLSX180), linked and grouped pictures (ODF192, ODF193, XLSX059), external pivots (ODF080, XLSX081) | Medium-large | 377 |
| B7 | 1 | **Done in part (2026-11-05)**, see `docs/deep-audit/losses-b7.md`. Removed DOCX175, DOCX126, DOCX176, DOCX180, ODF090; narrowed DOCX184, DOCX173, ODF178. Open: DOCX121/117/122/150 (paragraph-mark and row revisions need the DOCX paragraph writer to split at deletions), ODF190 (dimension-line object) | Medium | 412 |
| B8 | 2 | **Done in part (2026-11-05)**, see `docs/deep-audit/losses-b8.md`. Fields spanning paragraphs, tables and nested fields kept as field ranges (`FieldCodeMark`) in both formats: DOCX151 and DOCX161 narrowed (forms, hyperlinks, index and contents fields). Groups with tables kept as groups: DOCX181 removed, DOCX124 narrowed to non-rectangular shapes. Open: DOCX125 (header change at the next page), ODP102 (3D scenes), DOCX160 for legacy form fields | Large | 438 |
| B5b | 1 | **Done (2026-11-05)**, see `docs/deep-audit/losses-b5b.md`: `urn:pulseoffice:block-properties` custom XML part keyed by hidden `_PulseBlockN` bookmarks carries section name/protection/hidden/condition/link/nesting, list headers and cell number formats; hidden-paragraph fields as IF + `w:fldData`. DOCX016/031/034 removed, DOCX003 narrowed to sections starting or ending with a table, DOCX002 kept (grouped form controls cannot live in `wpg:wgp`) | Medium | - |
| B8 | 2 | Nested/spanning fields, mid-page section headers, tables in grouped text boxes, 3D scenes | Large | 438 |
| B11 | 1 | **Done (2026-11-05)**, see `docs/deep-audit/losses-b11.md`. Continuous mid-page sections: `TextParagraph.NextPageStyleName` (page style from the next page) honoured by the paginator, written in DOCX as a continuous `w:sectPr` with its own header references and in ODT as `pt:next-master-page` (ODF has no native carrier, §19.509). DOCX125 narrowed to sections holding only tables; full run LOSS 0, DECL 133, no DOCX125 row | Medium | 133 |
| B9 | 1 | **Done (2026-11-05)**, see `docs/deep-audit/losses-b9.md`. Paragraph-mark deletions read and written (DOCX122 removed, DOCX001 and DOCX150 narrowed), tracked table rows in the model, DOCX and ODT with accept/reject (DOCX121 narrowed to cells). DOCX117 left | Medium | - |

Every block follows rule 7 (both formats, `XlsxOdsParityTests` and the Folio/Deck parity tests), and closes with a
`corpus-audit run` whose LOSS must stay 0: removing a misattributed declaration (B2) may surface honest LOSS rows,
which are then fixed in the same block.

## 6. Block B2 outcome (2026-11-05)

Done; details in [deep-audit/losses-b2.md](deep-audit/losses-b2.md). New run after B2: **LOSS 0, DECL 503**
(was 0 / 529).

- `DeclarationMatcher` now uses the explicit table `tools/corpus-audit/declared-features.json` (code -> feature kinds);
  `DOCX039`, `DOCX040`, `PPTX020`, `DOCX020`, `DOCX031` cover no element kind.
- Real losses that the keyword matcher hid, now fixed: page-anchored objects dropped from DOCX bodies without
  paragraphs (DOCX040 rows), drawings inside tracked deletions dropped in DOCX, slide jumps to `Slide N` and to object
  names dropped in PPTX (PPTX020 rows), outline numbering of paragraphs whose style is not the outline heading style
  dropped in DOCX. Newly declared: `PPTX039` (jumps to missing slides or objects), `DOCX184` (fill and outline of
  linked text boxes, to be modelled in B7).
- Tool false positives removed: list membership through paragraph styles in both formats, outline numbering,
  silent numbering levels (no visible label), invisible shapes in ODF and SpreadsheetML.

## 7. Block B4 outcome (2026-11-05)

Done; details in [deep-audit/losses-b4.md](deep-audit/losses-b4.md). Fields Folio does not model are kept as
`TextFieldKind.Preserved` (Word instruction + ODF element + cached result) and cross both formats: `w:fldSimple` with
`w:fldData` in DOCX, the native element or `text:span pt:field-instruction` in ODT, with eight Word/ODF equivalents
mapped. DOCX123 and ODF082 no longer exist (0 rows, were 57 + 13). New run: **LOSS 0, DECL 349**. DOCX025, DOCX008
and DOCX195 remain open.

## 8. Block B3 outcome (2026-11-05)

Done; details in [deep-audit/losses-b3.md](deep-audit/losses-b3.md). New run after B3: **LOSS 0, DECL 321**
(was 0 / 503); DOCX102 rows 124 -> 20. Stand-alone VML `rect`/`roundrect`/`oval`/`line`/`shape` are read with or
without a text box; numeric VML paths, `v:polyline` and `v:curve` become freeform objects, `v:textpath` becomes
Fontwork, known spt presets map to the model's shapes. Formula paths and unknown presets stay rectangles with
DOCX140 (Info). Remaining DOCX102 rows: broken image links (d), DrawingML `lockedCanvas` and empty groups, `w:object`.

## 9. Block B4b outcome (2026-11-05)

Done; details in [deep-audit/losses-b4b.md](deep-audit/losses-b4b.md). DOCX008 removed (range entries written as a
hidden bookmark plus `XE \r`, read back as ranges), DOCX025 removed (index settings Word lacks carried in the begin
`w:fldChar`'s `w:fldData`), DOCX195 narrowed to fields without a Word instruction (others written with `w:delInstrText`
and read back as fields; the one corpus row, a corpus file, holds deleted `text:hidden-paragraph`/`text:hidden-text`).
New run: **LOSS 0, DECL 349**: the earlier DOCX008/DOCX025 rows were rows also declared by other codes.

## 10b. Block B6b outcome (2026-11-05): external pivots

Done; details in [deep-audit/losses-b6b.md](deep-audit/losses-b6b.md). Pivots on databases, queries, SQL, source
services and other workbooks keep their source and cached records across ODS and XLSX both ways, recompute from the
cache and refresh through the connection directory. ODF080 narrowed to source-service passwords (0 rows), XLSX081 to
OLAP, consolidation, non-database connections and named ranges of another workbook (2 rows,
`column-style-autofilter.xlsx`). New run (main with B1-B6 plus this block): **LOSS 0, DECL 170**.

## 10. Block B6 outcome (2026-11-05)

Done in part; details in [deep-audit/losses-b6.md](deep-audit/losses-b6.md). New run: **LOSS 0, DECL 236** (was 0 / 321,
the B5 changes included). Filters per range cross ODS and XLSX (named ranges become Excel tables); linked pictures and
pictures in groups cross both formats. XLSX180, ODF023, XLSX027 removed; ODF081 narrowed to pivot-source filters (0
rows), ODF192 to missing picture data (2 rows, class d), ODF193/ODF042/XLSX059 to charts, embedded objects, vector
pictures and controls in groups (ODF193 2 rows: a corpus file), XLSX031 to ranges that cannot become tables (3 rows:
numeric header cells). ODF080 (2) and XLSX081 (6), external pivots, stay open: they need a cached-record pivot source.

## 11. Block B5 outcome (2026-11-05)

Done; details in [deep-audit/losses-b5.md](deep-audit/losses-b5.md). Unused page styles travel in a
`urn:pulseoffice:page-styles` custom XML part with real header and footer parts (DOCX171 removed); wrapping only the
anchor paragraph travels in the `wp:docPr` anchor extension (DOCX020 removed). DOCX016, DOCX031, DOCX003, DOCX034,
DOCX001 and DOCX002 remain declared. New run: **LOSS 0, DECL 193**.

## 12. Block B7 outcome (2026-11-05)

Done in part; details in [deep-audit/losses-b7.md](deep-audit/losses-b7.md). Drawings inside tracked deletions are read
into the removed paragraph (DOCX175/DOCX126 removed); hyperlinks and charts of group members cross DOCX and ODT
(`GroupChild.Hyperlink`, `wpg:graphicFrame`; DOCX176/DOCX180 removed); free text boxes on ODP masters become master
decoration shapes (ODF090 removed); text boxes carry a fill and border (`TextBoxContent.Look`; DOCX184 narrowed to
gradient, picture and pattern fills); picture bullets cross both formats (`ListLevel.Picture`; DOCX173 and ODF178
narrowed to missing pictures). DOCX121/117/122/150 and ODF190 stay open. Tool: linked text boxes outside groups count
as text boxes. New run: **LOSS 0, DECL 219** (was 0 / 236).


## 13. Block B5b outcome (2026-11-05)

Done; details in [deep-audit/losses-b5b.md](deep-audit/losses-b5b.md). Sections, list headers and cell number formats
cross DOCX through a `urn:pulseoffice:block-properties` custom XML part tied to hidden bookmarks; conditional hidden
paragraphs through an IF field with `w:fldData`. DOCX016, DOCX031, DOCX034 removed; DOCX003 narrowed; DOCX002 stays for
groups of form controls. Tool: lists inside annotations no longer count for the enclosing list. New run: **LOSS 0,
DECL 151** (was 0 / 219 after B7; includes B6b/B7 work merged before).

## 14. Block B9 outcome (2026-11-05)

Done; details in [deep-audit/losses-b9.md](deep-audit/losses-b9.md). Paragraph-mark deletions are read as joins and written
as deleted marks (DOCX122 removed, DOCX001 narrowed to deletions holding tables or sections, DOCX150 to marks before a
table, at the end of a content control or on a section break); tracked table rows cross DOCX and ODT and resolve in the
model (DOCX121 narrowed to single-cell changes). DOCX117 stays. New run: **LOSS 0, DECL 137**.

## 15. Block B10 outcome (2026-11-05)

Done; details in [deep-audit/losses-b10.md](deep-audit/losses-b10.md). Dimension lines are a shared drawing object
(`ShapeObject.Dimension`, `DimensionLineGeometry`), drawn in the three modules, read and written natively as
`draw:measure` in ODT, ODS and ODP, and written to DOCX, XLSX and PPTX as a custom geometry (dimension line, guides,
filled arrowheads, label text) with the model in a `urn:pulseoffice:dimension-line` `a:spPr` extension: ODF190 removed.
3D scenes are kept as `Scene3DObject` (scene XML plus its graphic styles), round-trip in ODP and ODT, and go to PPTX as
a "3D" placeholder carrying the scene in `urn:pulseoffice:scene3d`; ODP102 narrowed to "other programs see a placeholder
in PPTX" (PPTX writer rule), DOCX002 names 3D scenes. Tool: Word shapes carrying the dimension extension count as
shapes, not text boxes. New run: **LOSS 0, DECL 131** (was 0 / 151); no ODF190 or ODP102 rows remain.
