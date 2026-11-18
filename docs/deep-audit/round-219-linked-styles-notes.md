# Round 219: linked styles queue and Folio footnotes and endnotes

Area: the two linked-style items left open by round 215, and a new audit of Folio footnotes and endnotes. Tests were
written but not run in this round (the coordinator builds and tests after the join).

## Fixed

1. **Minor. No "… Char" partner was created.** Applying a paragraph style other than the default one to part of a
   single paragraph, when the style has no usable character partner, now creates `<name> Char` (character formatting of
   the paragraph style, display name followed by " Char", a free name when taken), links both styles and applies the
   new character style, in one undo step (`ILinkedStyleSelection` returns `LinkedCharacterStyle`; the direct editor
   records the style collections with the text). The tracking editor wraps the same call. The styles panel shows and
   edits the link: a paragraph style chooses none, a new linked character style or an existing character style; a
   character style chooses its linked paragraph style. `IStyleEditing.Link` with `StyleLink` (None, ToNew, ToExisting)
   updates both sides and clears the old partners, as one step together with the other changes of the dialog. Texts in
   the five languages. `Round219LinkedStyleCreationTests`.
2. **Minor. Linked partners did not travel with copied or loaded styles.** `StyleClosure` adds the linked partner of a
   collected paragraph or character style; `StyleMerger` and `IStyleEditing.Load` drop, on the styles they add, a link
   whose partner is missing or linked to a third style (`StyleSheet.DropDanglingLinks`). `Round219LinkedStyleTransferTests`.
3. **Medium. DOCX lost the note numbering settings.** Number format, start value and restart of footnotes and endnotes
   were neither written nor read. `DocxNoteSettingsCodec` writes `w:footnotePr` and `w:endnotePr` in settings.xml
   (format, `numStart`, `numRestart`, the special note ids) and reads them from settings.xml and from the last body
   section, which overrides them. Formats Word cannot number (other than 1, a, A, i, I, unless every note has its own
   mark) and endnotes restarting on each page are declared (DOCX271, five languages). `Round219DocxNoteSettingsTests`.
4. **Medium. A note reference could not be removed from the keyboard.** The reference is a zero-width mark; Backspace
   right after it deleted the previous character and left the note. Backspace with the caret on a note reference now
   removes the note and its reference (`IDocumentEditor.RemoveNote`, one undo step). `Round219NoteRemovalTests`.
5. **Medium. Notes inside frames were neither numbered nor placed.** Note numbering walked only the body, so a note
   in a text frame had no citation and its footnote never reached the page. Numbering now follows each body paragraph
   with the frames anchored to it, and the paginator reserves the footnotes of a frame when it places the frame.
   `Round219FrameNoteTests`.
6. **Minor. Continuation notices were lost.** `NoteSettings` carries the forward and backward continuation notices.
   ODT writes and reads `text:note-continuation-notice-forward/backward`; DOCX writes the forward notice as the
   `continuationNotice` note and reads it back; the backward notice has no Word form and is declared under DOCX271.
   `Round219OdtNoteContinuationTests`, `Round219DocxNoteSettingsTests`.
7. **Minor. Note placement was ignored silently.** ODT `text:footnotes-position` other than `page` (ODT290) and DOCX
   footnote positions other than the page bottom or endnote positions at the section end (DOCX272) are declared on
   reading. `Round219OdtNoteContinuationTests`, `Round219DocxNoteSettingsTests`.

## Reviewed without defects

Insertion and the notes panel entry, per-chapter and per-page restarts in layout, custom marks in both formats, notes
in tables (row deletion removes them, rows move with their notes), undo of insertion, tracked deletions holding notes,
endnotes collected on their own pages (declared DOCX115), cross-references to notes.

## Open

- No model for footnotes below the text or at the document or section end, nor endnotes at each section end.
- Continuation notices are not drawn by layout; the separator line is fixed and not read.
- Backspace on a note reference while recording changes removes the note directly, without a tracked deletion.
