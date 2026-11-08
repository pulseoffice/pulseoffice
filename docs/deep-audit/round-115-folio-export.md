# Round 115: Folio export and import in RTF, HTML, EPUB and plain text

Area: `src/PulseOffice.Formats.Interchange/Text` (RTF, HTML/XHTML, EPUB and plain text writers and readers, the shared
assembler and outline helpers), plus the turned-cell cap in `src/PulseOffice.Documents.Layout/Flow/Stories.cs`.

## Coverage

| File | Lines read / total | Outcome |
|---|---|---|
| Rtf/RtfDocumentWriter.cs | 553/553 | list identity (L1), note recursion (L6), bidi (L3) fixed |
| Rtf/RtfDocumentReader.cs | about 900/1035 (session state, controls, destinations, tables, paragraph and run shapes) | list identity (L1), unbounded tables (L7), bidi (L3) fixed |
| Rtf/RtfVocabulary.cs | 237/237 | clean |
| Rtf/RtfTokenizer.cs | 126/126 | clean (word length, `\bin` and parameter overflow capped) |
| Rtf/RtfPicture.cs | 68/68 | `(measured ?? declared) * 15` can overflow `int`; harmless because the reader clamps the size to 20-40000 twips |
| Rtf/RtfState.cs | 105/105 | gained direction and right-to-left run state |
| Html/HtmlDocumentWriter.cs | 618/618 | list identity (L1), bidi (L3), class collisions (L5) fixed |
| Html/HtmlDocumentReader.cs | about 700/827 (session, start/end tags, frames, cells, anchors, images, base shapes, lists, paragraph and run shapes) | list identity (L1), charset (L4), bidi (L3), nesting caps (L7), numeric field kinds (L8) fixed |
| Html/HtmlTokenizer.cs | 147/147 | clean (attribute cap, raw-text elements) |
| Html/CssDeclarations.cs | 124/124 | clean (lengths and font sizes bounded, 64 declarations) |
| Html/HtmlTableReader.cs, Html/HtmlCellBorders.cs | not read this round | |
| Epub/EpubDocumentWriter.cs | 232/232 | heading ids can collide with bookmark ids (open, minor) |
| Plain/PlainTextFormat.cs | 125/125 | list numbering ignored identity (L2) fixed |
| Plain/TextEncodingDetector.cs, Plain/CodePages.cs | 66/66, 41/41 | `ICodePages.Named` added for L4 |
| Outline/ParagraphSemantics.cs, Outline/ListNumbering.cs | 64/64, 47/47 | `ListNumbering` is now stateless; counting moved to `ListThreadCounters` |
| Assembly/TextDocumentAssembler.cs | 437/437 | carries list thread and restart segments (L1) |
| Documents.Layout/Flow/Stories.cs | lines 174-463 | turned-cell cap (T1) fixed |

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| L1 | serious | `RtfDocumentWriter.List` (old key `(style, kind)`), `RtfReadSession.Paragraph`, `HtmlWriteSession.OpenLists`, `HtmlReadSession.List` | RTF and HTML lost list identity: separate lists sharing a style were written as one list, a restart (`ListRestart`) was dropped, a list split by other paragraphs restarted at 1 in HTML, and nothing was read back | RTF: one `\list` per (style, kind, thread), one `\listoverride` per restart with `\lfolevel\listoverridestartat\levelstartat`, `\levelstartat` from the style. HTML: `ListThreadCounters` replays the layout's counting; `<ol start>` when an opened list does not start at 1, `<li value>` on a restart or a jump; a thread change closes the open lists. Readers: `ParagraphShape.ListId/ListSegment/ListSegmentStart`; the assembler sets `ListRestart` on the first paragraph of each segment; `IListThreads.Settle` normalises the raw ids after reading (as ODT does). HTML: a top-level list with `start` continues the remembered thread of that style whose count is `start - 1`, a list without `start` is a new list |
| L2 | medium | `PlainTextWriter` with the old stateful `ListNumbering` | plain text restarted numbering after any non-list paragraph, shared one counter between separate lists and ignored restarts and start values | the writer counts with `ListThreadCounters`; `ListNumbering.Label(placement, value)` only formats |
| L3 | medium | RTF and HTML writers and readers | paragraph direction and right-to-left runs (decision 27) were dropped by RTF and HTML; an RTF/HTML right-to-left paragraph came back left-to-right | RTF `\rtlpar`/`\ltrpar`, `\rtlch`/`\ltrch`; HTML `dir` on paragraphs and runs, `direction:rtl` in class CSS, logical `end` for right alignment in RTL paragraphs; the reader maps physical `left`/`right` of an RTL paragraph to logical alignment |
| L4 | medium | `HtmlDocumentReader.Read` | the declared `<meta charset>` was ignored: legacy pages (windows-1251, Shift_JIS, ISO-8859-x) decoded as UTF-8 or windows-1252 | `Decode` honours a declared non-UTF charset in the first 4096 bytes when there is no BOM (`ICodePages.Named`) |
| L5 | minor | `HtmlWriteSession.ClassName` | style names that sanitise alike (`A b`, `A_b`) shared one CSS class, so one style's look overwrote the other | unique class per style name, suffix `-2`, `-3`… |
| L6 | minor | `RtfWriteSession.Mark` | a note whose content references itself recursed without end | each note is written once |
| L7 | minor | `RtfReadSession` (`\cellx`, list table and overrides), `HtmlReadSession` (`<ol>`/`<ul>`, `<table>` nesting) | sizes without a cap from hostile input | caps: 4096 cell definitions, 4096 lists and overrides, 9 levels; HTML lists and tables beyond 256 levels are ignored with balanced end tags |
| L8 | minor | `HtmlReadSession.Character` | `data-field="999"` was accepted by `Enum.TryParse`, giving an undefined field kind | only named, defined kinds are read |
| T1 | minor | `BlockStacker.LongestTurnedLine` (`Flow/Stories.cs`) | vertical cells, nested or not, were still capped at 22 in (1584 pt) even on taller pages: the page limit passed since rounds 71 and 93 was clamped to 22 in | the cap is now the largest page (110 in, 7920 pt, as `DocxSectionCodec.LargestPage`); the page limit decides |

## Remaining limits

- HTML has no list header paragraphs (`ListHeader`): they are written as items.
- EPUB heading ids (`h1`, `h2`…) can collide with a bookmark of the same name.
- List counting in table cells starts afresh per cell in plain text, as before.
- `HtmlTableReader` and `HtmlCellBorders` were not read this round.
- The test for L1 was written after the fix (the old writer had no way to express threads, so it fails by construction); T1's test failed before its fix.

## Tests

- `tests/PulseOffice.Formats.Interchange.Tests/Text/ListIdentityTests.cs`: RTF and HTML round trips of continued, separate and restarted lists; HTML `start` continuation; RTF override restart; plain text numbering.
- `tests/PulseOffice.Formats.Interchange.Tests/Text/InterchangeRound115Tests.cs`: RTF and HTML bidi, physical alignment of RTL HTML, class collisions, declared charset, numeric field kinds.
- `tests/PulseOffice.Documents.Layout.Tests/TableTurnedAndSplitTests.cs`: `OnAPageTallerThanTwentyTwoInchesANestedVerticalCellUsesThePageHeight` (failed before).
- Runs: Formats.Interchange 149/149, Documents.Layout 257/257, Architecture 43/43, Robustness 70/70.

## Live checks for the coordinator

- Export to RTF a document with two lists of the same numbering interleaved, a list split by a body paragraph and a list restarted at 5; open it in the reference suite and in Word; reopen it in Folio.
- Same document to HTML: the browser shows the same numbers; reopen in Folio.
- An Arabic or Hebrew paragraph exported to RTF and HTML: direction and alignment kept.
- Open a windows-1251 HTML page with `<meta charset>`.
- A page taller than 22 in with a long vertical cell.
