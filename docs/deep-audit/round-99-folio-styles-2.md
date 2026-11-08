# Deep audit round 99: Folio styles and numbering, second part

Follows [round-98-folio-styles.md](round-98-folio-styles.md).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Documents.UI/Commands/FolioStyleCommands.cs | 106/106 | sound; edits go through `RefusesDocumentEdit` |
| src/PulseOffice.Documents.UI/StyleCatalog/FolioStylesSource.cs | 405/405 | delete gated by built-in names and document lock |
| src/PulseOffice.Documents.UI/StyleCatalog/FolioStyleDialogs.cs | 183/183 | D4 fixed |
| src/PulseOffice.Documents/Editing/StyleEditing.cs | 197/197 | D3 fixed; one edit, one undo step |
| src/PulseOffice.Formats.Odf/Odt/OdtBodyWriting.cs | 1-90 | list writer; D1 fixed |
| src/PulseOffice.Formats.Odf/Odt/OdtBodyReading.cs | 68-139 | list reader; D1 fixed |
| src/PulseOffice.Formats.Odf/Odt/OdtStylesWriting.cs | 75-280 | page regions, list levels; D1, D2 |
| src/PulseOffice.Formats.Odf/Odt/OdtStylesReading.cs | 140-350 | page regions, list levels; D1, D2, display levels clamped |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxNumberingCodec.cs | 150-207 | D2 |
| src/PulseOffice.Documents.Layout/Flow/ListLabeling.cs | 151/151 | D2 |

Not reached: style rename (Folio has no rename command; only the display name changes, so references never move),
TOC level styles and note settings after a paragraph style deletion, `text:continue-list` pointing at a specific
`xml:id` (read as plain continuation; we always write `continue-numbering`), DOCX list continuation across sections.

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| D1 | medium | OdtWriteSession/OdtReadContext `StartedLists`, OdtStylesWriting/Reading regions | one started-list set for the whole file: header and footer lists were written with `continue-numbering` because the body had started the same style, and on reading the header lists (read first) made the first body list an explicit restart, so every round trip added a `ListRestart` | `OdtListContinuity` with a `Story()` scope; each header/footer region is its own story on writing and reading |
| D2 | medium | ListLevel, DocxNumberingCodec, ListLabeling, ODT list levels | legal numbering (`w:isLgl`) was silently dropped and never rendered | `ListLevel.Legal`; DOCX `w:isLgl` read and written; ODT `pt:legal` on `text:list-level-style-number`; labels render every level in arabic digits |
| D3 | medium | StyleEditing.Delete | deleting a table or frame style left tables and objects pointing at a missing style and detached children to no parent; deleting a character style left list levels (outline included) and drop caps pointing at it | references and children move to the deleted style's parent in the same undo step |
| D4 | medium | FolioStyleDialogs paragraph and character organizer | the parent choice offered the style's own descendants, creating an inheritance cycle | `IStyleLineage.PossibleParents` (injected) excludes the style and its descendants |
| D5 | minor | OdtStylesReading list level | `text:display-levels` unbounded | clamped to 1..10 |

## Tests

- tests/PulseOffice.Formats.Odf.Tests/Odt/OdtListContinuityRound99Tests.cs (header test failed before D1)
- tests/PulseOffice.Formats.OpenXml.Tests/DocxLegalNumberingRound99Tests.cs
- tests/PulseOffice.Documents.Layout.Tests/ListLegalNumberingRound99Tests.cs
- tests/PulseOffice.Documents.Tests/StyleDeletionRound99Tests.cs

Formats.Odf.Tests 814/814 (plus the legal test, run alone), Formats.OpenXml.Tests 2423/2423, Documents.Layout.Tests
255/255, Documents.Tests 369/369 (`RefillingManyEmptyControlsWalksTheDocumentOnce` is timing-sensitive under load and
passes alone), Documents.UI.Tests style subset 5/5.
