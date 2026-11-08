# Deep audit round 91: paste fidelity construct by construct

Limit left by round 88 ([round-88-exchange-objects.md](round-88-exchange-objects.md)): the matrix pasted every
representation without crash but did not assert that each Folio and Deck construct survives.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| docs/deep-audit/round-88-exchange-objects.md | 58/58 | starting point |
| tests/PulseOffice.Desktop.Tests/ExchangeSuite.cs | 227/227 | extended (second document of a module) |
| src/PulseOffice.Documents.UI/Exchange/FolioExchange.cs | 94/94 | D2 (fixed) |
| src/PulseOffice.Documents.UI/Editing/FolioRichTransfer.cs | 63/63 | D1, D2 (fixed) |
| src/PulseOffice.Documents/Clipboard/DocumentFragment.cs | 49/49 | ok |
| src/PulseOffice.Documents/Clipboard/FragmentExtractor.cs | 139/139 | D1 (fixed) |
| src/PulseOffice.Documents/Clipboard/FragmentScanner.cs | 116/116 | ok |
| src/PulseOffice.Documents/Clipboard/FragmentRemapper.cs | 207/207 | D1 (fixed) |
| src/PulseOffice.Documents/Clipboard/FragmentPaster.cs | 159/159 | ok |
| src/PulseOffice.Documents/Annotations/TrackedChanges.cs | 104/104 | ok |
| src/PulseOffice.Documents/Marks/InlineMark.cs | 140/140 | ok |
| src/PulseOffice.Documents/Revisions/TrackedEditing.cs | 225-260/~300 | recording paste settles the copied revisions |
| src/PulseOffice.Documents/Revisions/DeletedAnnotations.cs | 20-90/~90 | ok (change marks never travel into deleted text) |
| src/PulseOffice.Documents/Editing/Subdocuments.cs | 20-50/~120 | D1 (whole-document fragment carries the change log) |
| src/PulseOffice.Documents/Fields/MailMerge.cs | 160-195/~200 | ok (records now keep their revisions) |
| src/PulseOffice.Presentations.UI/Exchange/DeckExchange.cs | 201/201 | D3 (fixed) |
| src/PulseOffice.Presentations.UI/Exchange/DeckSelectionPackages.cs | 64/64 | D3 (fixed) |
| src/PulseOffice.Presentations/ElementBehaviourCloning.cs | 120/120 | ok |
| src/PulseOffice.Formats.OpenXml/PptxInteractionMapper.cs | 120-160/~170 | D4 (fixed) |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxFieldRanges.cs | 120/297 | ok (single-paragraph range read as preserved field) |

## Matrix

`tests/PulseOffice.Desktop.Tests/PasteFidelityMatrixTests.cs`, built on `ExchangeSuite` (which gained `OpenAnother`,
`FolioOf`, `DeckOf` to reach a second document of the same module). Each construct is counted in the destination; the
expected losses per representation are a table in the test, so a new loss or an unexpected survival both fail.

Folio (source: fixed date field, opaque field range, tracked insertion, deletion, deletion nested in another author's
insertion, format change, plain-text content control with tag, footnote, endnote, bookmark, comment, index entry,
cross-reference to the bookmark, picture-bullet list, vertical-text cell, per-cell padding):

| Construct | native, same document | native, other document | ODF | OOXML | HTML | RTF |
|---|---|---|---|---|---|---|
| field | yes | yes | yes | yes | no | no |
| field range | yes | yes | yes | yes (preserved field) | no | no |
| insertion / deletion / nested deletion / format change | yes | yes | yes | yes | no | no |
| content control | yes | yes | yes | yes | no | no |
| footnote | yes | yes | yes | yes | yes | yes |
| endnote | yes | yes | yes | yes | yes | yes |
| bookmark | yes | yes | yes | yes | no | yes |
| comment, index entry, cross-reference | yes | yes | yes | yes | no | no |
| picture bullet, vertical cell, cell padding | yes | yes | yes | yes | no | no |

Deck (text box with a fly-in animation carrying an embedded sound, and a hyperlink action):

| Construct | native, same document | native, other document | ODF (selection .odp) | OOXML (selection .pptx) | HTML | RTF |
|---|---|---|---|---|---|---|
| animation | yes | yes | yes | yes | no | no |
| action | yes | yes | yes | yes | no | no |
| sound | yes | yes | yes | yes | no | no |

Expected degradations: HTML and RTF carry text, notes and (RTF) bookmarks only; they have no vocabulary for revisions,
content controls, index entries, field codes, picture bullets or cell geometry, and none for slide behaviour. With
recording on, a paste is the paster's own insertion: copied revisions are settled (accepted view) first, as before.

Not applicable: Deck slide notes and sections are slide-level, while the exchange carries slide elements; slides move
between documents through duplicate and "insert slides from file", not through the clipboard.

## Defects

### D1 (serious) - tracked changes flattened by copy and paste
`FragmentExtractor.Balance` and `FragmentRemapper` dropped every `ChangeMark` and the fragment had no change log: copied
insertions became plain text with no author, deletions vanished, nested deletions lost their insertion, on every path
(native, ODT, DOCX, mail merge). Fix: `DocumentFragment.Changes`; the extractor keeps change marks, clips ranges cut by
the selection and collects the changes (with deleted content scanned for notes and comments); the remapper gives each
change a fresh id (`TrackedChange.CopyAs`, nested deletions follow their insertion); adoption adds them to the log in the
paste's undo step; `FolioRichTransfer` and `Subdocuments.Whole` carry the log. With recording on, `TrackedEditing`
settles the copied revisions before marking the paste as an insertion.

### D2 (medium) - DOCX representation never pasted into Folio
`FolioRichTransfer.Import` read only ODT, RTF and HTML, and formatted paste kept only HTML/RTF: a package holding only a
word-processing OOXML or ODF text representation offered no formatted paste. Fix: DOCX is read after ODT and both are
kept for formatted text.

### D3 (serious) - Deck selection packages lost animations, actions and sounds
`DeckSelectionPackages.Portion` copied the elements but not their animations, interactions and embedded sounds, so the
.odp/.pptx representations offered to other applications had no slide behaviour. Fix: the behaviours captured for the
native package are passed in and written into the portion.

### D4 (medium) - PPTX hyperlink actions read back with a changed target
`HyperlinkClickAction.Read` used `Uri.ToString()`, which normalises absolute URIs (`https://example.org` came back as
`https://example.org/`, escapes undone). Fix: absolute targets keep `OriginalString`.

## Tests run
Desktop.Tests filtered on Exchange|Paste|Clipboard|Folio 126/126 (the new matrix failed before D1-D4 were fixed);
Documents.Tests 363/363 (two new tracked-paste tests); Documents.UI.Tests 355/355; Presentations.UI.Tests 97/97;
Formats.OpenXml.Tests 2403/2403; Architecture.Tests 43/43.
