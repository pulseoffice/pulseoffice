# Round 223: Folio notes queue

Area: the three Folio notes items left open by round 219. Tests were written but not run in this round (the
coordinator builds and tests after the join).

## Fixed

1. **Medium. Note placement had no model.** `NoteSettings.Placement` (`NotePlacement`: PageBottom, BelowText,
   SectionEnd, DocumentEnd; endnotes default to DocumentEnd). Layout: footnotes below the text sit right under the last
   line of the page instead of at the bottom; footnotes at the section or document end and endnotes at the section end
   leave the page and are appended to the flow after their top-level section (`NoteNumbers.Sections`, `InSection`) or
   after the body, before the endnotes. The note settings dialog has a Position choice on both tabs (five languages).
   DOCX writes `w:pos` in `w:footnotePr` (pageBottom, beneathText, sectEnd, docEnd) and `w:endnotePr` (sectEnd, docEnd)
   and reads it from settings and the last section. ODT writes and reads `text:footnotes-position` (page, text, section,
   document) and, for endnotes at the section end, an endnote `text:notes-configuration` in the section properties of
   each top-level section. ODT290 and DOCX272 are gone. `Round223NotePlacementLayoutTests`,
   `Round223DocxNotePlacementTests`, `Round223OdtNotePlacementTests`.
2. **Minor. Continuation notices were not drawn and the separator line was fixed.** A footnote split across pages ends
   with the forward notice and resumes with the backward one; both are composed in the footnote paragraph style and
   their height is reserved with the note. `NoteCollection.FootnoteSeparator` (relative length, thickness, space before
   and after) replaces the fixed layout options. ODT writes `style:footnote-sep` in every page layout and reads the
   first one; DOCX writes a custom line as the separator note's paragraph with a top border (thickness), a right indent
   (length against the initial page's text width) and spacing, keeps the plain separator mark for the default line, and
   reads both forms back after the page styles. `Round223NoteSeparatorLayoutTests`, `Round223OdtNoteSeparatorTests`,
   `Round223DocxNoteSeparatorTests`.
3. **Minor. Backspace on a note reference ignored change recording.** `ITrackedEditing.RemoveNote` moves the reference
   into a deletion region holding the note (the note stays until the deletion is accepted; rejecting pastes the
   reference and the note back). Inside the author's own insertion the note is removed directly, as text is.
   `Round223TrackedNoteRemovalTests`. The round 219 tests that checked ODT290 and DOCX272 were removed and the endnote
   settings equality now includes the placement.

## Open

- "Section end" means the end of a top-level text section, not of a Word section (page style run); endnote collection is
  one document setting while ODT allows it per section; the separator is one document setting while ODT stores it per
  page layout; continuation notices are layout-only text (no caret, footnote paragraph style).
- The paginator plumbing for the separator and the notices landed with the first commit, since both items change the
  same request.
