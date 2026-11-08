# The plan for Folio: towards parity with the reference word processor and Word

> Folio must be **as powerful as the reference word processor and Word** and read/write **ODT and DOCX 100 %**, or
> declare the loss in `CheckFidelity`. The reference word processor is the **functional specification**: Folio's menus,
> ribbon and names are **our own**, as for Ledger and Deck. The real menu of the reference word processor
> (the menu bar definition in the reference clone) counts **554 entries** between commands and submenus;
> the measurable map is born in block F5.

Folio comes last for a declared reason: it rests on **rich-text layout**, the
riskiest subsystem of the project. The suite around it now exists: shell, commands, ODF and
OOXML formats, embedded fonts, spelling, cross-app objects, macros, printing, encryption and signatures. The plan
therefore puts **the risk first**: pagination and editor before any command.

## Principles

1. **Reuse, do not rewrite.** Much already exists in Deck and in the shared pieces: runs and paragraphs,
   `ParagraphLineBreaker`, `LineJustifier`, `FirstLineBreaker`, `ListLabels`, inline editor with IME and
   line navigation, autocorrect while typing, spelling, fonts, cross-app
   objects, signatures, encryption, versions, templates, mail, printing. What Folio needs in addition must be
   moved into the shared projects (rule 10), not copied.
2. **Layout is pure logic.** `PulseOffice.Documents.Layout` does not know Avalonia: text
   measurement is an injected strategy, as in `RowAutoHeight`. So pagination is tested without
   UI, and screen, print and PDF use the **same** result.
3. **Measured against the reference suite.** Where it matters, pagination is compared with the PDF that the
   reference word processor produces, with the same embedded font: number of pages, line and page breaks,
   position of objects.
4. **Incremental from the start.** A five-hundred-page document is not repaginated at every keystroke:
   it is repaginated from the modified paragraph until the pages coincide again.
5. All the usual rules apply: DI and patterns, no gratuitous statics, no comments, five
   languages, logs in English, ODF default and OOXML aligned, tests with the reference suite and live.

## Order of the blocks

### F0 — Targeted audit of the shared foundations
Before building: a short pass over the parts Folio inherits. Deck's runs and paragraphs,
line composers, inline editor, text in the ODF and OOXML formats, fonts, spelling.
*Criterion*: no open defect on those pieces, and the list of things to move into the shared projects.

### F1 — The document model
`PulseOffice.Documents`, today a placeholder. The document is a **tree of blocks**: paragraphs,
tables, sections (with their own columns), frames. Text is made of **runs**, and the run
model is extracted from Deck into a shared project (`PulseOffice.Text.Rich`, name to be confirmed), so
Deck and Folio have only one.

The model includes:
- **styles as document objects**: paragraph, character, list, page, frame and table, with
  inheritance, as in ODF;
- **anchors** for bookmarks, comments, tracked changes, notes and fields;
- **objects** anchored to the page, to the paragraph, to the character or as character.

Undo on the usual history (`UndoStack`), with transactions.
*Criterion*: a document built in code, with inherited styles, lists, a table and an anchored
image, is cloned, modified and undone. All tested without UI.

### F2 — The pagination engine (the risk)
`PulseOffice.Documents.Layout`, pure logic:
- **lines**: reuses Deck's composers; justified, indents, tabs (left, right,
  centre, decimal, with leader), automatic hyphenation with the Liang engine already written;
- **paragraphs**: spacing, exact, minimum and proportional line spacing, borders and backgrounds, drop caps,
  basic right-to-left text;
- **pages**: widows and orphans, "keep together", "keep with next", manual breaks;
- **page structure**: page styles with headers and footers (also different for the first
  page and for even pages), columns and sections;
- **notes**: footnotes and endnotes;
- **tables**: split across pages and repeat the heading;
- **objects**: text wrap around them (none, rectangle, contour, behind, in front).

Incremental. Text measurement is a strategy, with an Avalonia implementation and a fake for
tests.
*Criterion*: ten test documents (letter, thesis with notes, long tables, two columns, images with
wrap) paginated like the reference word processor: same pages and same breaks, with declared tolerance.
An edit on a 500-page document is repaginated in less than 50 ms.

### F3 — The view and the editor
`PulseOffice.Documents.UI`:
- **canvas**: pages on the canvas, virtualised (only what is visible is drawn), with zoom and
  multi-page view;
- **caret and selection**: caret on the paginated lines, continuous and block selection;
- **typing**: typing, IME, autocorrect while typing and spelling squiggles.
  Deck's inline editor must be generalised into a shared component, not duplicated;
- **clipboard and drag**: clipboard with formatting and text drag-and-drop;
- **shell**: `Documents` module document in the shell (`IDocumentOperations`, autosave,
  recovery, unsaved changes), with the blue accent `#2563EB`.

*Criterion*: live, a three-page text is written with arrows, selection, undo, enter, IME and
autocorrect; pages are redone while typing.

**Audit after F3.**

### F4 — The formats: ODT and DOCX
- **ODT**: reader and writer in `Formats.Odf`, with streaming read as for sheets.
- **DOCX**: reader and writer in `Formats.OpenXml`.
- **Surroundings**: FODT, OTT and DOTX templates, legacy 1.x office XML files (`.sxw`, with the translator already
  written), ODF encryption and signatures as for the other modules.
- **Interchange**: RTF, plain text and HTML in `Formats.Interchange`.

Round-trip tests on at least a hundred files from a corpus file (the reference clone's corpus) in the four directions, as for ODS and ODP.
*Criterion*: the F2 test documents go through ODT↔DOCX with the reference suite without undeclared losses.

### F5 — The command surface and the census
- **Commands**: registry of the `Documents` module, ribbon, menus, palette, context menus and shortcuts,
  with the same infrastructure and its own maps (`PulseRibbonLayout`, `PulseMenuLayout`,
  `PulseContextLayout`).
- **Ribbon**: our own tabs, with the rich controls that already exist (font, size, split colours,
  galleries) and the paragraph **style gallery**.
- **Census**: a command map of the reference word processor and its census script, with exclusions
  declared as for the spreadsheet and presentations.

*Criterion*: startup without warnings (titles, icons, rules) and measured coverage.

### F6 — Complete formatting
- **Character**: all effects, spacing and kerning, language.
- **Paragraph**: tabs, borders, background, text flow, drop caps.
- **Lists**: multi-level outline and chapter numbering.
- **Styles**: style dialogs and panel, with "new from selection" and "update".
- **Page**: paper, margins, headers and footers, columns, sections, borders, watermark.
- **Clone formatting.**

Every attribute goes through ODT and DOCX, or the loss is declared.

**Audit after F6.**

**Status (2026-10-10): F6 closed, audit included.** Character, paragraph (with drop caps, per-side
borders, tabs, flow, page breaks and **column breaks**), lists and chapter numbering,
styles (shared panel, new from selection, update, modify, delete, load from template), page
(six-tab dialog, borders, background, watermark, columns, header and footer), sections, **title
page**, clone formatting, case and kana transformations. Every attribute goes through ODT and DOCX
— also tested in the four round trips through the reference suite run headless — or the loss is declared
(`DOCX014`–`DOCX018`). The audit fixed: double break before a drop cap in DOCX,
caret three lines tall on the last line of a drop cap, spurious decimals in centimetre fields,
unreadable ribbon previews in dark mode, ribbon tabs not redrawn on theme change,
"Interface appearance" not wired, command palette that jumped and did not execute on click,
tabbed dialogs that changed height. The details are in the [register](worklog.md).

### F7 — Tables
Insertion and drawing, merge and split, draggable widths, fit to content, per-cell borders and backgrounds,
table styles, repeated heading, sorting, simple formulas in cells,
conversion between text and table. Deck's table data model is compared and, where possible,
unified.

**Status (2026-10-10): F7 closed, with a final check of the areas touched.** Insertion (with the caret
in the first cell and Tab/Shift+Tab between cells, the last Tab adds a row), rows and columns in
number with the neighbours' format, rectangular selection of cells, rows, columns and table, merge and
split of cells (into rows or columns, or dissolving a merge), split and merge table,
draggable widths on cell borders, width and height from dialog, optimal, minimum and
distributed, per-side borders and per-cell background, vertical alignment, **table styles** (eleven
predefined with coloured heading and bands, in a shared gallery), heading repeated
in pagination, rows that do not split, sorting with up to three keys, formulas in cells
(SUM, AVERAGE, MIN, MAX, COUNT, PRODUCT, references and ABOVE/LEFT/BELOW/RIGHT) recalculated in the same
edit, conversion between text and table in both directions, tabbed properties dialog, contextual
"Table" ribbon (at the end of the tabs, only with the caret in a table), menu and context menu.
Everything goes through ODT and DOCX, also tested in the four round trips through the reference suite run headless
(`tools/table-fixtures.sh`); the only loss is `DOCX019` (a heading that does not repeat).
**Unification with Deck**: the model stays double for a substantive reason — Deck's cells are
values of a data table (with its query), Folio's are containers of blocks — but
the cell range, the merge algebra, the widths, sorting, formulas and predefined styles
are common, in `PulseOffice.Document.Tables`; Deck was restructured on these parts
and gained sorting and table styles, Ledger the same gallery. Word-processor census: **199 of
472 (42.2 %)**. Left out, declared: Word's "hand-drawn table", cell
protection, number format and recognition in cells.

### F8 — Objects and drawing
All existing `EmbeddedObject`s inside the text: images, shapes, charts, formulas, Fontwork,
form controls, media, OLE and packages. Anchoring, wrap, captions and linked text
frames. Objects are manipulated with the shared `ObjectManipulator`.

**Status (2026-10-10): F8 closed, with a final check of the areas touched.** Model: contour
(normalised polygon or derived from the shape; for images from transparency), wrap of the first
paragraph only, image mirroring, linked frames (`IFrameChains`), complete object
memento. Pagination: text wraps around the contour even rotated and mirrored, first-paragraph-only
leaves the others below the object, text flows from one frame to the next with different widths,
images as character inside frames are drawn. Services: anchor (with the position computed
from pagination), order, captions numbered by category (Figure, Illustration,
Table, Text, Drawing; for an anchored object the frame that contains it, as the reference word processor does) with
renumbering after deletions and paste, frame linking. UI: contextual tab
"Object", Format › Object, Arrange, Image menus, context menu; three-tab properties
dialog; insertion of frame, shapes (gallery and families), line, chart, formula, Fontwork, QR,
audio and video, object from file, gallery, twenty-one form controls; image commands (filters
applied to the pixels as in the reference word processor). Shared components born or extracted here and wired into the three modules:
`IContentFlipping`, `IEmbeddedChartPrompt`, `IFormulaPrompt`, `IPackagedFilePrompt`,
`IRasterImageOperations`, `IImageContourTracer`. Formats: ODT and DOCX writing and reading, round trips
through the reference suite run headless (`tools/object-fixtures.sh`); declared loss `DOCX020` (first
paragraph only). Word-processor census: **285 of 472 (60.4 %)**. Left out: manual contour editing
(the reference suite's contour dialog), grouping (Folio's selection is of a single object), freehand
drawing of lines, curves and polygons, control and form properties (waiting for Folio's macros).

### F9 — Fields, references and indexes
- **Fields**: date, page, counts, author, title, variables, conditionals.
- **References**: bookmarks, cross-references, hyperlinks, numbered captions.
- **Footnotes** and endnotes.
- **Indexes**: table of contents, alphabetical index, index of figures, bibliography, with update.
- Document **navigator**.

**Audit after F9.**

**Status (2026-10-10): F9 closed, audit included.** Model: fields carry a detail (format, values
for true and false, fixed, stored value) and are updated with per-type strategies (`IFieldValueSource`):
document and custom properties, date and time (also fixed), counts of words, characters,
paragraphs, tables, images, objects and pages, set and read variables, user fields, conditional
text and conditional hidden text (with a small condition evaluator in the reference word processor's
syntax), sequences, chapter, references to bookmarks, headings, captions and notes with the formats text,
page, number, above/below, chapter, caption, category and number, number only; citations with the
**bibliographic database in the document**. Indexes as generated and protected sections (`IndexDefinition`):
table of contents from levels and entries, alphabetical index with merged entries, keys and letters, indexes of figures
and tables from numbered captions, bibliography, user-defined indexes; heading and
level styles with dotted tab leader; links from the table of contents. Pages come from real pagination
(`IFieldLayoutFacts`), and "Update all" repeats until pages and indexes no longer change (three passes at
most, a single undo entry). Index text is not written by hand. Navigation
(`IDocumentNavigation`) for fourteen categories and chapters that are moved, promoted, demoted and change
level with their subchapters (`IChapterMoving`). Footnotes already had
real pagination (space at the bottom, continuation, separator); here are added special note, note editing and settings (numbering, start, restart per
document, chapter or page). UI: field and cross-reference dialogs, update fields (F9), selected
field and all, convert to text, open and remove link, table of contents and indexes with their
settings, index entry, citation and bibliographic database, field shading (Ctrl+F8), "References"
tab. **Shared component** (rule 10): the **navigator** in the side panel
(`NavigatorPanel` with one source per module), wired into Folio (F5), Deck and Ledger, with jump, filter and
reordering with buttons or by dragging (chapters, slides, sheets). Formats: ODT (`text:conditional-text`,
`hidden-text`, `variable-set`/`get`, `user-defined`, `note-ref`, `sequence-ref`, `bibliography-mark`,
counts, fixed date with value; `table-of-content`, `alphabetical-index`, `illustration-index`,
`table-index`, `bibliography`, `user-index` with source and templates) and DOCX (`IF`, `SET`/`REF`,
`DOCPROPERTY`, `NOTEREF` with bookmark, `REF` with `\r`/`\p`, `PAGEREF`, `STYLEREF`, `CITATION` with the
sources in `customXml`, `TOC`/`INDEX`/`BIBLIOGRAPHY` across several paragraphs, locked fields). Declared losses
`DOCX021`–`DOCX025`; the six round trips are in `ReferenceRoundTripTests` with the samples from
`tools/reference-fixtures.sh`. Word-processor census: **301 of 472 (63.8 %)**.

**Audit after F9 — done.** Found and fixed: an edit to an object repaginated the whole document
(now incremental: from about 126 to 18 ms on 128 pages with 500 objects); the navigator did not follow
edits in Deck (new slides missing) and now follows the history of the active document; panel title
not translated and categories taking half the panel (now a drop-down list). The "stuck" Shift
key seen with `uiprobe` does not reproduce: neither the HID state nor the application retains it
after `keys` or `click --mods shift` (verified with a probe on modifier state and live).
Samples of formatting, tables and objects from the reference suite run headless, regenerated with the current code: no
regression. Timing tests: 51 pages with 450 fields, 150 notes, table of contents and alphabetical index
update in 180 ms and typing takes 2 ms per keystroke. The details are in the [register](worklog.md).

### F10 — Review
- **Tracked changes**: recording changes, accept and reject, compare and merge documents.
- **Comments** with replies.
- **Spelling and grammar** (the engine exists), with per-run language and word count.
- **Protection** of sections and of the document.

Tracked changes and comments go through both formats.

**Status (2026-10-10): F10 closed, with a final check of the areas touched.** Tracked changes: the editor is
decorated (`TrackingDocumentEditor` over `IDirectDocumentEditor`) and with recording on, insertions,
deletions (including of paragraph end), enter, character and paragraph format become tracked changes
with author and date; typing inside one's own insertion does not create new ones, backspace merges into a
single deletion and deleting freshly inserted text really removes it. Accept, reject and "reject
while recording", on a single change, on the selection, on all and moving to the next, each in one undo
entry (`IRevisionResolver`); compare (the differences become tracked changes in the current document) and
merge (the other document's differences enter as tracked changes by its author), on paragraphs and words
(`IDocumentComparison`). On screen, insertions underlined and deletions struck through in the author's colour,
format changes in bold, change bars in the margin also in print; show and hide. Recording protection
with password (key derived as in the reference word processor). Comments with replies, resolved, edit, delete also by author,
bring a reply to the top, navigation; **margin balloons** with lines to the text and commented text
highlighted; printing comments at the end. Proofing: checking while typing **per run
language** with red (spelling) and blue (grammar, our own rules: repeated words, double spaces,
space before punctuation, capital at sentence start) squiggles, checking dialog, language for selection,
paragraph and all text, thesaurus; word count already existed. Protection of sections with password and
of the document (read-only, comments only, tracked changes only, forms only). **Shared
components** (rule 10): the **review panel** (`ReviewPanel` with one source per module, filter by
author and text, accept and reject, replies and resolved), wired into Folio, Ledger (cell changes and
comments; "Manage changes" opens the panel) and Deck (slide comments); the **spell-check
dialog** (`ISpellingPrompt`) in Folio, Ledger and Deck; the author palette. Formats:
ODT (`text:tracked-changes` with the true default of `track-changes`, regions across several paragraphs,
`office:annotation` with `loext:parent-name` and `loext:resolved`, section key, document
protection) and DOCX (`w:ins`/`w:del`/`w:rPrChange`/`w:pPrChange`, **inserted paragraph end** in
`w:pPr/w:rPr/w:ins`, `comments.xml` with `commentsExtended` and `commentsIds`, `w:documentProtection`);
declared losses `ODT005`, `DOCX026`, `DOCX027`. Round trips through the reference suite run headless in `ReviewRoundTripTests`
(`tools/review-fixtures.sh`). Word-processor census: **336 of 472 (71.2 %)**.

Left out, declared: tracked changes on **tables and objects** (inserting or removing a table or object
is not recorded), the **previous format** of a format change in ODT (`ODT002`, as other
office suites do), **manual hyphenation** (automatic per-paragraph hyphenation exists since block F6),
protection of **fields and bookmarks** and of **table cells**.

### F11 — Printing and export
- **Printing**: multi-page preview and printing with the reference word processor's options.
- **PDF with embedded fonts.** The declared limit of our PDF, i.e. only the fourteen
  standard fonts and the rest as `?`, is unacceptable for a word processor. **Subset TrueType
  embedding** with `ToUnicode` is needed, which will immediately serve Ledger and Deck too.
- **Other exports**: EPUB and HTML.
- **Mail merge** from a data source, with the existing connections.

**Status (2026-10-13): F11 closed, with a final check of the areas touched.** PDF: subset TrueType embedding
(`TrueTypeSubsetter` in Typography, CIDFontType2, Identity-H, `W`, `CIDToGIDMap`, `ToUnicode`),
whole OpenType CFF as FontFile3 up to 16 MB, per-character fallback over a list of installed
fonts, fsType respected (forbidden: `PDF010` is declared and another font used), fonts without outlines (colour
emoji) declared in `PDF012`; the fourteen standard fonts remain only as last resort. Options: range,
bookmarks from headings, internal and external links, comments as notes, metadata, PDF/A-2b (XMP, sRGB,
`/ID`; not validated with veraPDF). Printing: shared multi-page preview and print dialog with imposition
(`PrintImposition`: range, even/odd, reverse, copies and collation, pages per sheet, booklet,
duplex, graphics, black text, comments), print to PDF file; Windows with `printto` and declared capabilities,
not tested. EPUB 3 and XHTML; mail merge (`Database` field, MERGEFIELD, `text:database-display`, single
document, ODT/DOCX/PDF files, single PDF; mail not sent). Details in the register.

### F12 — Finishing touches and API
- **Macros**: `doc` object for macros, in the five languages and with undo.
- **Templates**: templates and master documents.
- **Text**: word-processor autocorrect (automatic lists, word completion),
  shared special characters and symbols.
- **View**: view modes (web, full screen, side-by-side pages), rulers with tabs.
- **Commands**: everything the census leaves missing.

**Status (2026-10-13): F12 closed, final audit closed. Folio is complete.** Word-processor census **471 of 471 wanted entries**
(100 %; `InsertContentControl` completed with real content controls), 22 excluded with
reasons. Done: `doc` object for macros with undo in one step; `.ott`/`.dotx` templates and master
documents (`.odm`, `text:global`, linked subdocuments with relative paths, `DOCX029`); word-processor
autocorrect (lists, borders, word completion, autoformat also with tracked changes); view
(single page, multiple pages, book, web, draft, full screen, shared `RulerStrip` rulers with
tabs and margins, formatting marks, boundaries, grid, guides, highlights); block
selection and direct cursor; and all the remaining commands (details in the register). The open points of F12 were
closed on 2026-10-13 (see register, "Chiusura dei punti aperti di F12"): Deck uses `RulerStrip` with margins,
indents and tabs of the box being edited; the signature line is written and reread in ODT and ODS as
the reference suite does; the web view shows no headers, footers or margins and prints paginated as the reference word processor does;
block selection follows the horizontal band in points; a test composes the whole Desktop container.

**Final audit** across Ledger, Deck and Folio — **closed on 2026-10-13** (register, "Audit finale: chiusura"):
signature line in DOCX and XLSX, complete content controls, unified shared components (find and
replace, zoom, alternative text), read-only and shortcuts aligned, typical path tested live in the
three modules (open, edit, format, save in ODF and OOXML, reopen, PDF) with a clean app log. Only
the checks on real Windows and Linux and in Word and Excel remain, which this machine does not allow.

## Declared risks
- **Pagination fidelity**: breaks identical to Word and to other word processors are impossible in absolute terms
  (different engines). The goal is consistency with the reference word processor given the same fonts, measured and with
  written tolerance, not the pixel promise.
- **Performance**: incremental pagination must be designed in F2, not added later.
- **Complex text** (Arabic, Hebrew, Indic, vertical CJK): the bidirectional base enters in F2;
  vertical scripts and advanced Asian justification are declared for a later block.
- **DOCX** is the format with the most real-world variants (Word of different versions, Google Docs, other office suites):
  the F4 test battery must include files from all three.
