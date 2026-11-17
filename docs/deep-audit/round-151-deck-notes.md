# Round 151: Deck speaker notes and comments, Folio comments (2026-11-16)

Scope: the plain-text paths of Deck (speaker notes in ODP and PPTX, PPTX comments) and the Folio comment codecs
(ODT annotations, DOCX comments, notes), chosen as the Folio/Deck areas with the oldest deep audit.

## Defects
- **ODP speaker notes lost whitespace** (medium, fixed): notes were written as raw `text:p` text and read with the
  element value. `text:s`, `text:tab` and `text:line-break` written by other suites were dropped (words merged, tabs
  and manual breaks gone), and repeated spaces written by Deck collapsed in other readers. Writer and reader now use the
  shared `IOdfParagraphText`. Tests: `OdpNotesTextRound151Tests`.
- **PPTX notes and modern comments ignored `a:br`** (medium, fixed): the two lines of a paragraph were concatenated.
  New `IDrawingPlainText` (`DrawingPlainText`) shared by `PptxPrintMasters` and `PptxCommentMapper`. Test:
  `Round151NotesBreakTests`.

## Read without defects
DOCX notes and comments (`DocxAnnotationsCodec`: id tables, reply threads, resolved state), ODT annotations
(replies, resolved, point comments).

## Not verified
Not cross-checked in the reference suite or OnlyOffice; the legacy PPTX comment position unit was not compared with
PowerPoint.
