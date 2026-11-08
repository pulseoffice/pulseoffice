# Deep audit round 101: Folio list threads, style rename, references after deletion

Follows [round-99-folio-styles-2.md](round-99-folio-styles-2.md): the four points it did not reach.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Documents/Editing/StyleEditing.cs | 197/197 | deletion did not move TOC level styles nor note settings (D2) |
| src/PulseOffice.Documents/Styles/Styles.cs | 268/268 | `Name` is read-only: no rename existed (D1) |
| src/PulseOffice.Documents/Styles/StyleSheet.cs | 183/183 | sound |
| src/PulseOffice.Documents.Layout/Flow/ListLabeling.cs | 151/151 | counters keyed only by list style (D3) |
| src/PulseOffice.Formats.Odf/Odt/OdtListContinuity.cs | 22/22 | per-style continuation only (D3) |
| src/PulseOffice.Formats.Odf/Odt/OdtBodyReading.cs | 60-200 | `text:continue-list` read as plain continuation (D3) |
| src/PulseOffice.Formats.Odf/Odt/OdtBodyWriting.cs | 1-110 | only `continue-numbering` written (D3) |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxNumberingCodec.cs | 208/208 | instances with a start override aliased onto the abstract's list (D3) |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxBlockWriter.cs | 130-165 | list instance chosen by style only (D3) |
| src/PulseOffice.Documents/Indexes/IndexEditing.cs | 1-90 | index styles fixed by kind (D2) |
| src/PulseOffice.Documents/Indexes/IndexDefinition.cs | 61/61 | no level styles (D2) |
| src/PulseOffice.Documents/Annotations/Notes.cs | 25-70 | note settings name a paragraph style (D2) |
| src/PulseOffice.Documents/Editing/ListEditing.cs | 105-126 | continue command updated for threads |
| src/PulseOffice.Documents/Editing/DocumentEdit.cs | 190-270 | recorder and mementos; used for the new reference walk |
| src/PulseOffice.Documents/Editing/Stories.cs | 51/51 | header and footer stories are covered by reference rewrites |
| src/PulseOffice.Formats.Odf/Odt/OdtIndexes.cs | 194/194 | index templates read without their styles (D2) |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxIndexSettings.cs | 90/90 | our index extension extended with level styles |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxIndexes.cs | 120-145 | title detection now uses the definition's heading style |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxReader.cs | 15-60, 135-175 | threads settled after sections are built |
| src/PulseOffice.Documents.UI/StyleCatalog/FolioStylesSource.cs | 1-405 | rename action added |
| src/PulseOffice.Documents.UI/StyleCatalog/FolioStyleDialogs.cs | 1-120 | rename dialog added |
| src/PulseOffice.UI.Controls/StylePanels/StylesPanel.cs | 180-300 | rename item in the shared context menu |
| src/PulseOffice.Documents/Clipboard/FragmentRemapper.cs | 1-110 | pasted list threads could join a thread of the target (D5) |
| src/PulseOffice.Documents.UI/Shell/Snapshot/StyleConverters.cs, SnapshotNodes.cs | read in full | snapshot converter for index level styles |

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| D3 | medium | model, ListLabeling, ODT and DOCX list codecs | list identity did not exist: all paragraphs of a list style shared one counter, so a list interrupted by another list of the same style (A1 A2, B1, A3) resumed from B; `text:continue-list` was read as `continue-numbering`, and a DOCX `w:num` with a start override was folded into its abstract's list | `TextParagraph.ListId` (the list thread; null is the style's default list); `ListLabeling` counts per thread; `IListThreads` settles the threads read from a file into the compact form (only lists that are interleaved get an explicit thread, named `list1`, `list2`, …); ODT reads `xml:id` + `text:continue-list` and writes `continue-list` with a generated `xml:id` when the continued list is not the latest of its style; DOCX gives each thread its own `w:num` with a `w:lvlOverride/w:startOverride`, and reads override instances as their own threads |
| D1 | medium | StyleEditing, Styles.cs, Folio style panel | no rename existed: only the display name could change, so a renamed style kept its internal name in files and references | `IStyleEditing.Rename` with `DocumentStyle.CloneAs`; `IStyleReferences.Redirect` moves every reference (paragraphs and runs in every story including headers and footers, children, next styles, list styles of paragraph styles, character styles of list and outline levels, drop caps, index level styles, note paragraph and citation styles, page next styles, deferred page styles, the initial page style, frames, tables) in one undo step; refused for built-in styles and for an existing name (the panel then makes the name unique); "Rename…" in the shared styles panel menu, enabled in Folio, disabled in Ledger and Deck, which have no rename; five languages |
| D2 | medium | StyleEditing.Delete, IndexDefinition, ODT/DOCX index codecs | after deleting a paragraph style, index level styles and `NoteSettings.ParagraphStyleName` (and, for character styles, `CitationStyleName`) still named it; index level styles were not even modelled: a TOC whose levels used custom styles came back with the default ones | `IndexDefinition.LevelStyles` (`IndexLevelStyles`, value equality); ODT reads and writes `text:style-name` of title and entry templates; DOCX keeps them in our index extension (`heading-style`, `entry-style.N`); deletion goes through the same `Redirect` and moves them to the parent |
| D4 | medium | StyleEditing.Delete (page) | deleting a page style left paragraphs' deferred page style (`NextPageStyleName`) and other page styles' next style pointing at it | both follow (`Standard`, and next style cleared) |
| D5 | minor | FragmentRemapper | pasted paragraphs kept their list thread, so a pasted interleaved list could join an unrelated list of the target | threads are renamed to fresh ones on paste |
| — | — | DOCX list continuation across sections (point 4) | verified sound: a list interrupted by a Folio section or by a Word section break keeps one list and no restart through reading and round trips | tests only |

## Tests

- tests/PulseOffice.Formats.Odf.Tests/Odt/OdtListThreadRound101Tests.cs (continue-list, interleaved round trips, model threads, index level styles)
- tests/PulseOffice.Formats.OpenXml.Tests/DocxListThreadRound101Tests.cs (start override instances, plain restarts, model threads, Folio sections, Word section breaks, index level styles)
- tests/PulseOffice.Documents.Layout.Tests/ListThreadLabelsRound101Tests.cs
- tests/PulseOffice.Documents.Tests/StyleRenameRound101Tests.cs (rename in every family, undo and redo, collision, deletion of paragraph and page styles, paste)
- tests/PulseOffice.Documents.UI.Tests/FolioStylesPanelTests.cs (panel rename, built-in refused, collision made unique)
- tests/PulseOffice.Documents.UI.Tests/FolioSnapshotCompactnessTests.cs (threads and index level styles survive the recovery snapshot)

Results: Documents.Tests 377/377, Documents.Layout.Tests 256/256, Formats.Odf.Tests 819/819, Formats.OpenXml.Tests
2429/2429, Documents.UI.Tests 365/365, Architecture.Tests 43/43.

## Remaining

- Ledger and Deck have no style rename: the shared panel shows the item disabled there.
- RTF and HTML export do not carry list threads: interleaved lists of one style number as one list there.
- Failing-before runs could not be shown because the worktree may not be reverted; the defects were established by reading the old code paths.
- Live checks: rename a custom style from the styles panel (text, headers, TOC and undo follow); open an ODT with `text:continue-list` and a DOCX with a `w:num` start override, check numbering after save and reopen in the reference suite and Word.
