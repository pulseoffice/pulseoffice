# Deep audit round 22: clipboard and paste in Folio and Deck

Area: the receiving side of `PulseOffice.Exchange` in Folio and Deck (and the shared pieces Ledger uses for images):
unformatted, formatted and special paste, tables from sheet ranges, pasted images, plain text in lists and content
controls, very large pastes. Earlier rounds read first so as not to repeat: [shared-round9.md](shared-round9.md)
(exchange guard, object size cap, cell colours), [ledger-whole-ranges-round21.md](ledger-whole-ranges-round21.md)
(whole-range copy bounded to the occupied area), the Folio and Deck rounds.

## Coverage

| File | Lines read/total | Outcome |
|---|---|---|
| Documents.UI/Exchange/FolioExchange.cs | 108/108 | fixed: unformatted paste typed line by line through the interactive editor |
| Presentations.UI/Exchange/DeckExchange.cs | 198/198 | ok (sizes clamped to the slide; read-only checked) |
| Exchange/ObjectExchange.cs | 150/150 | ok (mode must be offered by the participant) |
| Exchange/ExchangeContentGuard.cs | 60/60 | ok (16 M cells, read in round 9) |
| Exchange/CellGridConversions.cs | 112/112 | fixed: table natural size overflowed `int` |
| UI.Controls/Services/SystemClipboard.cs | 127/127 | ok (HTML reader caps 64 MB; bitmaps decoded by the platform) |
| Shell/Exchange/HtmlTableGrids.cs | 46/46 | ok (table reader caps 1 M cells) |
| Documents.UI/Commands/FolioClipboardCommands.cs | 64/64 | ok |
| Documents.UI/Editing/FolioImages.cs | 48/48 | fixed: tall images taller than the page |
| Presentations.UI/DeckObjectInsertion.cs | 1-90/… | fixed: declared pixel sizes overflowed the image extent |
| Sheets.UI/SheetImageInsertion.cs | 1-102/102 | fixed: same overflow (shared fix) |
| UI.Controls/Objects/BitmapPixels.cs | 140/160 | ok (decoding capped at 40 Mpx) |
| UI.Controls/Objects/RasterImageFiles.cs | 80/… | ok |
| Documents.UI/Editing/FolioEditor.cs | 360-640/922 | fixed: `Type` and the new `PasteText` share one insertion path |
| Documents/Editing/DocumentEditor.cs | 60-180, 360-418/418 | fixed: multi-line insertion quadratic, one history entry and one notification per line |
| Documents/Revisions/TrackedEditing.cs | 30-80/… | ok (delegates to the inner editor, so it inherits the fix) |
| Documents.UI/Editing/FolioTypingAssist.cs | 1-100/… | ok (the assists are right for typing, wrong for paste: see defect 1) |

## Defects

### Serious
1. Documents.UI/Exchange/FolioExchange.cs:95 — unformatted paste (the default for plain text from any other app)
   replayed the text through `FolioEditor.SplitParagraph` and `Type` line by line, so every typing assist ran on
   pasted text: "- a" lines became an automatic list (prefix eaten), an empty line inside a list ended the list, a
   pending word completion was accepted, autocorrect could fire on one-character lines; inside a single-line content
   control the lines were glued together silently. Fix: `FolioEditor.PasteText` shares the insertion path of `Type`
   (`Insert`, with protection, form, placeholder and content-control checks) without the assists, in its own
   `paste-unformatted` step. Tests `IlTestoSempliceIncollatoNonDiventaUnElencoAutomatico`,
   `IlTestoSempliceConRigheVuoteInUnElencoNonChiudeLElenco` (both failed before).
2. Documents/Editing/DocumentEditor.cs:69 — `InsertText` with N lines committed 2N edits, each pushing to the history
   and raising a document change (relayout): pasting 3,000 plain lines took 13-26 s. Fix: one recorder, the split shared
   with `SplitParagraph` (`Split`, `InsertLine`), one commit: 0.14 s. Test
   `MoltissimeRigheDiTestoSempliceSiIncollanoInFrettaInUnSoloPasso` (26 s before).

### Medium
3. Documents.UI/Editing/FolioImages.cs:33 — the width was capped at 16 cm but the height followed the aspect ratio
   without bound: a 200 x 9,000 px screenshot became 238 cm tall. Deck (`DeckObjectInsertion`) and Ledger
   (`SheetImageInsertion`) computed the extent from the *declared* header size in `int` twips, which overflows for
   a hostile header (1 x 2^31 px). Fix: shared `IImageExtents` (`Drawing/Assets/ImageExtents.cs`, registered in the
   Desktop composition and in `AddFolioUI`), wired into all three modules: natural size at 96 px/inch, minimum width,
   proportional fit into a box (Folio 16 x 24 cm, Deck the slide and 24 cm, Ledger the 500 cm exchange cap).
   Tests `UnImmagineAltissimaIncollataStaNellaPagina` (failed before), `DeepAuditRoundTwentyTwoImageExtentsTests`.
4. Exchange/CellGridConversions.cs:74 — the natural size of a table built from a cell range was 2.6 cm per column and
   0.7 cm per row in `int`: past ~1.46 M columns or ~5.4 M rows (both admitted by the exchange guard from a foreign
   package) the width or height wrapped negative. Now capped at `ExchangeContentGuard.MaximumExtent`. Test
   `UnaTabellaDaUnIntervalloEnormeHaUnaDimensioneNaturaleValidaEntroIlTetto` (both cases failed before).

## Remaining
- A foreign package admitted by the guard (up to 16 M cells) still becomes a Deck table or a Folio table of that many
  cells; a real Ledger copy is bounded to the occupied area (round 21), so only a hostile native clipboard reaches it.
- Pending character formatting is not applied to multi-line plain text (unchanged behaviour of `Type`).

## Live checks
- Copy a bulleted list from a text editor ("- a", "- b") and paste into Folio: the dashes stay, no automatic list.
- Inside a Folio list, paste "a", blank line, "b": three items, the list continues.
- Paste a few thousand lines of plain text: immediate; one ⌘Z removes it all.
- Paste a long full-page screenshot into Folio, Deck and Ledger: it fits the page, the slide, the sheet cap.
