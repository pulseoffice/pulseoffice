# Deep audit round 18 — Folio editor services

Area: the nineteen services of `src/PulseOffice.Documents.UI/Editing` not read in round 17, plus the minor points left
open there. Every file is read in full with the Read tool; this log is updated every five files.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| FolioProtectedContentGuard.cs | 133/133 | no defect |
| FolioCaretSpeech.cs | 70/70 | no defect |
| FolioContextMenus.cs | 33/33 | no defect |
| FolioDrawing.cs | 115/115 | no defect |
| FolioFieldRefresh.cs | 77/77 | no defect (`ShowMergeRecord` changes field display outside history by design: the merge preview must not fill the history) |
| FolioImages.cs | 48/48 | no defect |
| FolioKeyMap.cs | 113/113 | no defect |
| FolioLinks.cs | 84/84 | no defect |
| FolioPageStatus.cs | 109/109 | no defect; `PageOf` now returns on the first match from the end |
| FolioPageStyles.cs | 71/71 | see defects |
| FolioProofing.cs | 129/129 | see defects |
| FolioRichTransfer.cs | 68/68 | no defect |
| LayoutLines.cs | 104/104 | no defect |
| ParagraphOrder.cs | 52/52 | no defect; new `Indexes` (one pass) used by the navigator |
| FolioObjectCycle.cs | 29/29 | no defect |
| FolioObjectHits.cs | 42/42 | no defect |
| FolioEditRefusals.cs | 20/20 | no defect |
| ContentControlPlaceholders.cs | 21/21 | no defect |
| PendingCharacterFormat.cs | 39/39 | no defect |
| CaretNavigator.cs (`Step`) | 49–61 | minor of round 17, fixed: defect 3 |
| FolioReviewSource.cs (`FolioReviewText`, `Comments`) | 1–140 | minors of round 17, fixed: defects 4 and 5 |
| FolioNavigatorSource.cs (`Current`) | 86–98 | minor of round 17, fixed: defect 6 |

All nineteen files are read in full. The protection guard, the object and drawing services and the key map refuse
edits through `ReadOnly`, `RefusesEdit` and `RefusesDocumentEdit`; no untranslated interface text was found.

## Defects

1. **Medium** — `FolioPageStyles.Current` and `Anchor` (FolioPageStyles.cs:28, 38) walked only the top-level paragraphs
   of the body: with the caret inside a section or a table cell the loop never met the caret and returned the page style
   of the end of the document, so the margin, column and orientation commands, the page dialog and the page style panel
   changed the wrong page style. They now walk every paragraph in document order (`IBlockTree.Paragraphs`). Test
   `TheCaretInsideASectionReadsThePageStyleBeforeIt` (failed before: Landscape read instead of the default).
2. **Minor** — `FolioProofing.Suggestions` (FolioProofing.cs:90) threw `ArgumentOutOfRangeException` for a mark past the
   end of the text (a stale mark after an edit, or a negative start). Out-of-range marks have no suggestions. Test
   `AStaleProofMarkPastTheEndHasNoSuggestions` (failed before).
3. **Minor (performance)** — `CaretNavigator.Step` (CaretNavigator.cs:51) copied the whole story twice for every arrow
   press. The story is now read only when the caret crosses a paragraph; a missing paragraph leaves the caret in place
   instead of indexing `-1`. Test `ArrowsCrossParagraphsInsideAndOutOfASection` (regression guard).
4. **Minor (performance)** — `FolioReviewText.Describe` built the whole text of a change before clipping it to 400
   characters, and `Plain` walked the paragraph list three times. Clipping now stops building at the limit and `Plain`
   walks once. Test `ALongChangeIsDescribedClipped` (regression guard on `Plain`).
5. **Minor (performance)** — `FolioReviewSource.Comments` looked up each root's anchor with a linear search (quadratic in
   the comments); a dictionary built once replaces it.
6. **Minor (performance)** — `FolioNavigatorSource.Current` compared every heading with the caret through
   `IParagraphOrder.Compare` (two linear scans each: headings × paragraphs); it now uses one index map
   (`IParagraphOrder.Indexes`).

Remaining: none found in the assigned files.

## Tests run

- `PulseOffice.Documents.UI.Tests`: 295/295.
- `PulseOffice.Desktop.Tests` filtered on `Folio`: 18/18.
- `PulseOffice.Architecture.Tests`: 38/38.

## To try live

- Caret inside a section or a table placed before a landscape page break: Layout > Margins/Orientation/Columns and the
  page style panel act on the page style of the caret's page, not on the last one of the document.
- Right-click a red-underlined word right after deleting text at the end of the paragraph: no crash.
- Review panel and navigator on a document with hundreds of comments and headings: stay responsive.
