# Round 215: Folio styles queue from round 212

Area: the three open Folio style items of round 212. Tests were written but not run in this round (the coordinator
builds and tests after the join).

## Fixed

1. **Medium. A paragraph could not switch off the list of its paragraph style.** `TextParagraph.ListSuppressed` is an
   explicit "no list" value. The style resolver gives such a paragraph no list (and no style list level), list threads
   skip it, layout gives it no label and no outline number, and the layout cache treats it as list-relevant. Removing
   the list of a paragraph whose style carries one (`SetList` with no list) sets it; applying a list clears it; the
   "no list" command and the list toggles look at the effective list. DOCX writes `w:numPr` with `w:numId w:val="0"`
   and reads it back as suppression (a list header, which also writes 0, is restored by its own extension entry). ODT
   writes the paragraph outside `text:list` with `style:list-style-name=""` on its automatic paragraph style and reads
   that, outside a list, as suppression. RTF and HTML export (shared `ParagraphSemantics`) write no list.
   `Round215ListSuppressionTests`, `Round215DocxListSuppressionTests`, `Round215OdtListSuppressionTests`,
   `Round215ListSuppressionExportTests`.
2. **Minor. DOCX linked styles.** `ParagraphStyle.LinkedStyleName` and `CharacterStyle.LinkedStyleName` model
   `w:link`; DOCX reads it only when the id names a style of the other family and writes it after `w:next`. ODF has no
   linked styles, so ODT keeps the link in our `pt:linked-style-name` attribute on `style:style` (other readers ignore
   it). The native snapshot keeps it, renaming either style follows it, deleting either clears the partner's link.
   Applying a linked paragraph style to part of one paragraph applies the linked character style to the selection, as
   Word does (`ILinkedStyleSelection`, used by both the direct and the tracking editor); a caret, a whole paragraph or a
   multi-paragraph selection still sets the paragraph style. A paragraph style copied in the styles panel does not
   inherit the original's link. `Round215LinkedStyleTests`, `Round215DocxLinkedStyleTests`, `Round215OdtLinkedStyleTests`.
3. **Minor. A new page style made from another lacked header and footer text.** `IStyleEditing.PageCopy` clones the
   page style with its header and footer content and remaps the block ids through `IFragmentRemapper`; parent, display
   name, hidden flag and next style are not copied, as before. `Round215PageStyleCopyTests`.

## Open

- Word creates the "… Char" partner when a linked paragraph style is first applied to a partial selection; we only use
  an existing link, and the styles panel cannot create or edit a link.
- Pasting or loading styles from another document does not bring the linked partner along; a dangling link is ignored
  when applied but kept in the model.
