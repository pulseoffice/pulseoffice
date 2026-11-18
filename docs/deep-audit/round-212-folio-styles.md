# Round 212: Folio styles and templates

Area: paragraph, character, list, table and page styles; inheritance, next style, defaults, rename, delete, loading
styles from another document, update from selection, the styles panel; DOCX `styles.xml` and ODT style round trip;
the open DOCX line-number restart item.

Tests were written but not run in this round (the coordinator builds and tests after the join).

## Defects fixed

1. **Medium. DOCX default paragraph style with a localized id.** Word in other languages writes the default paragraph
   style with a localized `w:styleId` (for example `Standard`) and `w:default="1"`, name `Normal`. Only the id `Normal`
   was mapped to the Standard style, so the file's default style became an extra style named "Normal" and every
   paragraph without `w:pStyle` lost its formatting. The style marked `w:default="1"` (or `Normal` when none is marked)
   is now Standard. On writing, the Normal style carries `w:default="1"`, which Word needs to apply it to paragraphs
   without a style, and a user style named "Normal" no longer takes the `Normal` id. `Round212DocxDefaultStyleTests`.
2. **Medium. Page style rename and delete left paragraph styles dangling.** Paragraph styles that break to a page style
   (`style:master-page-name`) kept the old name after a rename and pointed to a missing style after a delete. They now
   follow, in the same undo step. `Round212PageStyleReferenceTests`.
3. **Medium. Clearing direct formatting dropped field details and ruby text.** `DirectFormatting.Clear(TextRun)`
   rebuilt the run without `FieldDetail` (field format, fixed value, conditional texts) and `Ruby`; clear formatting,
   the format brush and update style from selection lost them. `Round212ClearFormattingTests`.
4. **Medium. Loading styles from another document duplicated block ids.** Page styles loaded from a template kept the
   ids of their header and footer paragraphs, which can equal ids already used in the document. Loading now remaps
   them through the same `IFragmentRemapper` the paste path uses (`StyleEditing` takes it). `Round212StyleLoadTests`.
5. **Minor. Derived paragraph styles.** A style derived from one whose next style is itself now has itself as next
   style (it pointed to the parent) and keeps the parent's category; a new page style made from another keeps the
   gutter. `Round212DerivedStyleTests`.
6. **Minor. DOCX `w:semiHidden w:val="0"`** was read as hidden; it is now read as visible.

## Checked without defects

Style chains guard against cycles (`StyleChain`, `StyleLineage`); rename and delete move parents, next styles,
paragraph and run references, drop caps, list level character styles, index level styles and note settings in one
undo step; built-in styles cannot be renamed or deleted from the panel; ODT writes and reads parent, next, display
name, list style and level, outline level, master page and class; the ODT line-numbering start value travels as the
first paragraph's standard restart.

## Open

- A paragraph cannot switch off the list of its paragraph style (DOCX `w:numId="0"`, ODF empty `list-style-name`).
- DOCX linked styles (`w:link`) are not modelled.
- A new page style made from another does not copy header and footer text.
- DOCX line-number restarts: unchanged, the remaining cases have no Word section form (DOCX248).
