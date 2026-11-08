# Declared losses, block B7

Block B7 of [declared-losses-analysis.md](../declared-losses-analysis.md): tracked changes with objects, group
hyperlinks and charts, picture bullets, master text boxes, dimension lines, linked text box styles.

## Files read

| File | Outcome |
|---|---|
| `docs/declared-losses-analysis.md` (B7 rows) | scope |
| `docs/deep-audit/losses-b2.md` | deleted drawings written through `DocxInlineWriter.Drawing`; DOCX184 plan |
| `Formats.OpenXml/Wordprocessing/DocxInlineReader.cs` | `Deleted` dropped drawings (DOCX175) |
| `Formats.OpenXml/Wordprocessing/DocxReader.cs` | DOCX126 duplicate check |
| `Formats.OpenXml/Wordprocessing/DocxRevisionWriter.cs` | writer side already complete |

## Changes

| Code | Outcome |
|---|---|
| DOCX175, DOCX126 | **Removed.** `w:del`/`w:moveFrom` may hold `w:drawing`, `w:pict`, `w:object` and `mc:AlternateContent` (ECMA-376 Part 1 §17.13.5.14). `DocxInlineReader.Deleted` now reads them into the removed paragraph: inline ones as `ObjectMark`s, anchored ones anchored to the removed paragraph, the same model the ODT reader and the DOCX writer (B2) use. Tests: `DocxDeletedPictureTests`, `DocxPageObjectTests.PicturesInsideATrackedDeletionAreWrittenAsDeletedDrawings` (now a round trip). |
| DOCX176 | **Removed.** `a:hlinkClick` on a member's `cNvPr` (§20.1.2.2.23) is read into the new `GroupChild.Hyperlink` and written back (`DocxGroupMemberLinks`, shapes, pictures, chart frames); groups unpacked for tables (DOCX181) put it on the member object. ODT: a member wrapped in `draw:a` inside `draw:g` (ODF 1.3 §10.4.12; `draw:a` is a shape in `draw:g`). Tests: `DocxGroupMemberTests`, `OdtGroupMemberTests`, `DocxDeletedPictureTests.A_hyperlink_on_a_group_member_is_read`. |
| DOCX180 | **Removed.** `wpg:graphicFrame` members with `c:chart` (Word 2010 `wpg` schema) are read through the chart codec and written back as `wpg:graphicFrame` (the group codec no longer excludes charts). ODT already kept chart members in `draw:g`. |

Files read (2): `Formats.OpenXml/Wordprocessing/DocxDrawingCodec.cs` (group codec, unpacked groups), `Formats.Odf/Odt/OdtDrawings.cs` (group writer/reader).

| Code | Outcome |
|---|---|
| DOCX184 | **Narrowed** to gradient, picture and pattern fills of text boxes. `TextBoxContent.Look` (a `FrameProperties`: background, border colour and width) is new model data, kept by undo snapshots, chaining and unlinking. DOCX: `wps:spPr` solid fill and `a:ln` (`DocxTextBoxLook`, ECMA-376 §20.1.2.2.35, §20.1.2.3.24) for every text box, linked or not; ODT: `fo:background-color` + `draw:fill`/`draw:fill-color` and `fo:border` on the frame's graphic properties (ODF 1.3 §20.175, §20.183). Tests: `DocxTextBoxLookTests`, `OdtTextBoxLookTests`. |
| DOCX173, ODF178 | **Narrowed** to bullets whose picture is missing from the file. `ListLevel.Picture` (`ListBulletPicture`: data, media type, size in points). DOCX: `w:numPicBullet` with a VML `v:shape`/`v:imagedata` (or a `w:drawing` blip when read) and `w:lvlPicBulletId` (§17.9.21, §17.9.20), the image part belongs to the numbering part (`DocxPictureBullets`); the character bullet stays in `w:lvlText` for readers without pictures. ODT: `text:list-level-style-image` with `xlink:href` to `Pictures/` and `fo:width`/`fo:height` on `style:list-level-properties` (§16.32, §17.20); our fallback character, start value and display levels go in `pt:` attributes; `office:binary-data` is read too (`OdtListPictures`). Tests: `DocxPictureBulletTests`, `OdtPictureBulletTests`. The layout still draws the character bullet. |
| ODF090 | **Removed.** A `draw:frame`/`draw:text-box` without `presentation:class` on a master page is read as a master decoration: a rectangle `ShapeObject` with the frame's graphic style and its formatted text (the frame is read as the equivalent `draw:custom-shape`). Decorations already cross ODP (`draw:custom-shape` on the master) and PPTX (a non-placeholder `p:sp` on the master). Test: `DeckEighthAuditCorpusTests.Free_text_boxes_of_ODP_masters_are_read_as_master_shapes` (ODP -> model -> PPTX -> model). |

## Left declared in this block

| Code | Why |
|---|---|
| DOCX122, DOCX150, DOCX121, DOCX117 | The model can hold a paragraph-mark deletion (a point `Deletion` whose removed content holds two paragraphs, as `DocxDeletedParagraphMarks.Merge` does for empty paragraphs), but `DocxRevisionWriter.Deleted` writes a paragraph break inside a deletion as a deleted line break, not as a deleted paragraph mark (`w:rPr/w:del` in `w:pPr`, §17.13.5.14). Reading them now would turn a declared loss into a silent change on DOCX write. They need the paragraph writer to split a paragraph at such a deletion; tracked rows need a row-level change in the table model. Next block. |
| ODF190 | A dimension line needs its own drawing object (measured length, guides, text placement) rendered in three modules; a flag on a line would keep the file but not what the user sees. Next block. |

Files read (10): `DocxDrawingCodec.cs` (shape codec), `DocxReader.cs` (chains), `DocxNumberingCodec.cs`, `DocxWriter.cs` (numbering), `OdtBodyWriting.cs`/`OdtBodyReading.cs` (frames), `OdtProperties.cs` (frame codec), `OdtStylesWriting.cs`/`OdtStylesReading.cs` (list levels), `OdfMasterPages.cs`, `OdpReader.cs`, `DocxBlockReader.cs`, `DocxDeletedParagraphMarks.cs`, `DocxRevisionWriter.cs`.

## Follow-up found with `corpus-audit one`

- a corpus file (docx->odt, shapes LOSS once DOCX184 no longer covered it): the linked box is a group member, and
  `GroupStories.Member` turned it into a transparent rectangle. It now takes the box's `Look` as fill and line.
- Tool: `FeatureRules.WordTextBox` treats a `wps:wsp` with `wps:linkedTxbx` outside groups as a text box.
- `DocxThirteenthAuditTests`: a `wpg:graphicFrame` with an empty `a:graphicData` is now declared as DOCX102 (an
  unreadable member), not DOCX180.

## Corpus run

Full run after B7: **LOSS 0, DECL 219** (was 0 / 236).
