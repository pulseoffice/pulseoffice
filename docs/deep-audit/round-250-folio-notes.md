# Round 250: Folio footnotes and endnotes

Area: footnote and endnote continuation notices, numbering and restart, placement, separators, and their DOCX and ODT
round trips. No build or test was run in the round (audit cycle rule); the coordinator runs them at the join.

## Defects found

| Severity | Defect | Status |
|---|---|---|
| Medium | A click on a continuation notice placed the caret in a paragraph that does not exist in the document (`note-continuation-notice`), and every notice on every page shared that id in the caret index, so caret queries could land on any of them. The notice could not be selected or copied. | Fixed |
| Medium | Word's "restart at each section" was read as our chapter restart (at each level 1 heading), so a DOCX with section restarts was numbered at headings, or never restarted when it had none; a chapter restart was written to DOCX as a section restart without a declared loss. | Fixed |
| Medium | The note settings dialog indexed the restart choices by the enum value: an endnote setting read from ODT as "page" selected a choice that does not exist in the two-item endnote list. | Fixed |
| Minor | The alignment of an object relative to a paragraph broken across pages was measured to the column bottom plus the first line that does not fit, so a bottom-aligned object could hang below the last line on the anchor page. | Fixed |

## Fixes

- `ILayoutReadOnlyText` (`PulseOffice.Documents.Layout.Queries`) recognises continuation notice lines
  (`LayoutParagraphs.ContinuationNotice`) and returns the notice under a point as `LayoutText` (page, text, line
  rectangles). `LayoutQueries` keeps notice lines out of hit testing, character hits and the caret index. In Folio a
  click on a notice selects it as read-only text (`FolioEditor.LayoutText`, cleared by any other selection), painted
  with the selection colour; Copy is enabled and puts its plain text on the clipboard; Cut stays disabled and typing
  never edits it.
- New `NoteNumbering.Section`: numbering restarts at each page style run (Word's section). DOCX `eachSect` reads and
  writes it; a chapter restart is still written as `eachSect` and now declared under DOCX271. ODT writes
  `text:start-numbering-at="document"` plus our `pt:start-numbering-at="section"` (ODF 1.3 has only document, chapter
  and page), so other readers number continuously. The dialog lists document, chapter, section (and page for
  footnotes) by value, in the five languages.
- The wrapped paragraph height used for paragraph-relative alignment stops at the last line that fits the column.

## Research

ODF 1.3 `text:start-numbering-at` allows `document`, `chapter` and `page`; ECMA-376 `w:numRestart` allows
`continuous`, `eachSect` and `eachPage` (the latter only meaningful for footnotes). There is no standard mapping
between chapter and section restarts, hence the extension attribute and the declaration.

## Open

- Continuation notices can be selected only as a whole by a click; a drag or Shift+click from body text does not
  extend into them, and Select All does not include them (Word does not let you select them at all in the body view).
- Section restarts follow page style runs only; a section block with its own note settings is not a restart point.
- None of this has been opened in Word or the reference suite.
