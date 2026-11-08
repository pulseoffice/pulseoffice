# Deep audit, round 8: Deck interface (commands and dialogs)

Re-reads the `Deck*Commands` files that `deck-round2.md` and `ledger-deck-round3.md` did not cover, closes the two
uncapped image readers and the round-3 minors. Changes are uncommitted in the worktree.

## Coverage

| File | Read | Outcome |
|---|---|---|
| Presentations.UI/DeckSlideCommands.cs | 200/200 | **fixed**: uncapped reads (background image, insert from file), stale slide/document after await, silent failure |
| Presentations.UI/DeckExtraInsertCommands.cs | 128/128 | **fixed**: album photos read whole with no cap; guide position not clamped |
| Presentations.UI/DeckCommandSet.cs | 16/16 | extended with `Holds(target, slide[, element])` |
| Presentations.UI/DeckAnimatedImageCommands.cs | 101/101 | **fixed**: frames replaced on the slide that was current after the dialog |
| Presentations.UI/DeckCharacterCommands.cs | 39/39 | ok |
| Presentations.UI/DeckClipboardCommands.cs | 92/92 | ok |
| Presentations.UI/DeckConnectorCommands.cs | 40/40 | ok |
| Presentations.UI/DeckDrawCommands.cs | 90/90 | ok (handle index re-checked before editing) |
| Presentations.UI/DeckFieldCommands.cs | 152/152 | **fixed**: field retarget applied to a removed or changed element |
| Presentations.UI/DeckFindCommands.cs | 26/26 | ok |
| Presentations.UI/DeckFontworkCommands.cs | 77/77 | ok (target captured before the await) |
| Presentations.UI/DeckFormCommands.cs | 57/57 | **fixed**: properties and tab order applied to the document active after the dialog |
| Presentations.UI/DeckFormControlCommands.cs | 57/57 | ok |
| Presentations.UI/DeckFormFieldCommands.cs | 106/106 | ok (target read once after the dialogs, consistently) |
| Presentations.UI/DeckFormatCommands.cs | 112/112 | ok |
| Presentations.UI/DeckImageCommands.cs | 174/174 | **fixed**: a crop/replace/recolour result was applied to whatever image was selected after the dialog |
| Presentations.UI/DeckLinkCommands.cs | 76/76 | ok |
| Presentations.UI/DeckMacroCommands.cs | 32/32 | ok |
| Presentations.UI/DeckMasterCommands.cs | 104/104 | **fixed**: delete/change/elements used masters and slides of one document with the history of another; index not bounded |
| Presentations.UI/DeckMediaCommands.cs | 48/48 | ok (read-only checked before placing) |
| Presentations.UI/DeckMotionCommands.cs | 101/101 | **fixed**: transition applied through another document's history |
| Presentations.UI/DeckNavigationCommands.cs | 102/102 | **fixed**: rename of a slide removed meanwhile; go-to clamped with the stale count |
| Presentations.UI/DeckObjectCommands.cs | 161/161 | **fixed**: name/interaction/distribute applied to stale element or other slide |
| Presentations.UI/DeckPanelCommands.cs | 50/50 | ok |
| Presentations.UI/DeckRibbonCommands.cs | async paths (colour, highlight, fill, line, zoom) | ok (applied through the current text target or selection) |
| Presentations.UI/DeckSlideSetupCommands.cs | 35/35 | **fixed**: size compared on one document and applied to another |
| Presentations.UI/DeckSpellingCommands.cs | 175/175 | **fixed**: language chosen for one deck set on another; thesaurus replaced through another history |
| Presentations.UI/DeckStyleCommands.cs | 233/233 | **fixed**: style chosen in one deck redefined in another; style index not bounded |
| Presentations.UI/DeckTableCommands.cs | 283/283 | **fixed**: table editor result replaced a table removed or changed meanwhile |
| Presentations.UI/DeckTextDialogCommands.cs | 209/209 | ok |
| Presentations.UI/DeckTextFrameCommands.cs | 109/109 | ok (insets bounded 0–20 cm by the field) |
| Presentations.UI/DeckToolCommands.cs | 81/81 | ok (redaction re-finds the element) |
| Presentations.UI/DeckViewCommands.cs | 92/92 | ok |
| Presentations.UI/DeckZoomCommands.cs | 111/111 | ok |
| Presentations.UI/ObjectDialogs/DeckObjectDialog.cs | 56/56 | **fixed**: same stale-element pattern for every object dialog |
| Presentations.UI/PrintMasterView.cs | 1-60 | **fixed** (round-3 minor): fixed page edge colour |

Not re-read in this round (remaining): the Design dialogs (`HeaderFooterView`, `ShowSetupView`, `SlideSizeView`,
`SlideBackgroundView`, `PhotoAlbumView` beyond its data path), `Motion/AnimationPane`, `Motion/TransitionEditor`,
`Themes/ThemeDialogView`, `CustomShowsView`, `PresentationWindow`, `PresenterWindow`, `SlideView`.

## Defects

### Medium: commands finished after a dialog on another document (or on a removed slide)

Most asynchronous Deck commands captured the slide, element, master or style before the dialog and re-read
`Context.Target` after it, then mixed them: e.g. `DeckMasterCommands.ChangeAsync` called the *new* document's
`AssignMaster` with the *old* document's slide and master, `DeckFormCommands.TabOrderAsync` replaced the controls of
the old slide on the new one, `DeckSpellingCommands.LanguageAsync` set on the new deck the language chosen for the
old one. Switching document while a dialog is open (another window) or an agent editing meanwhile put a step into the
wrong history, edited a slide no longer in the deck, or silently did nothing.
Fix (restructured): the target is captured once before the await and used afterwards; `DeckCommandSet.Holds(target,
slide[, element])` checks that the slide is still in that deck and the element is still the one the dialog was opened
on. Applied to `DeckSlideCommands`, `DeckMasterCommands`, `DeckMotionCommands`, `DeckFormCommands`,
`DeckObjectCommands`, `DeckNavigationCommands`, `DeckStyleCommands`, `DeckSpellingCommands`, `DeckFieldCommands`,
`DeckImageCommands`, `DeckAnimatedImageCommands`, `DeckTableCommands`, `DeckSlideSetupCommands`, `DeckObjectDialog`.
Tests: `DeckRound8AuditTests.Tab_order_confirmed_after_switching_document_changes_the_document_it_was_opened_on`,
`A_background_image_picked_while_its_slide_is_deleted_changes_nothing` (both failed before).

### Medium: uncapped readers of chosen files

`DeckSlideCommands.BackgroundImageAsync` and `DeckExtraInsertCommands.PhotoAlbumAsync` copied the whole file into
memory, and the background image was not even validated as an image (its media type came from a private copy of the
extension table). The background now uses the shared `IRasterImageFiles.PickAsync` (capped by `IChosenFileReader`,
validated, shared media types: duplicate removed); the album reads every photo through `IChosenFileReader.ImageLimit`
and reports oversize and unreadable files by name. «Insert slides from file» reads through the new
`IChosenFileReader.DocumentLimit` (512 MB) and reports an oversize or unreadable presentation instead of doing nothing
(new keys `deck.slide.insertFromFile.tooLarge` / `.unreadable` in five languages).
Test: `Album_photos_beyond_the_image_limit_are_refused_without_reading_them` (failed before).

### Minor

- `DeckImageCommands.Edit`: the edited image was applied to the image selected *after* the dialog (different image if
  the selection changed). Now applied only to the element it was taken from (covered by the `Holds` pattern).
- `DeckStyleCommands` and `DeckMasterCommands`: dialog indexes used without bounds; now bounded.
- `DeckExtraInsertCommands.GuideAsync`: the position is clamped to the slide.
- `PrintMasterView`: the page edge was a fixed grey; now the `Palette.Keys.Border` theme token.

## Tests run

`PulseOffice.Presentations.UI.Tests` (63, green), `PulseOffice.Localization.Tests` (32), `PulseOffice.Architecture.Tests`
(38), `PulseOffice.UI.Controls.Tests` filter `Objects` (21), `PulseOffice.Desktop.Tests` filter `Deck` (43, 1 skipped).

## Live checks

- Slide background image: a > 64 MB picture is refused with the size message; a valid one still applies with one ⌘Z.
- Photo album with one oversize and one valid photo: the album opens with the valid one, the status names the other.
- Insert slides from a corrupted `.odp`: error message instead of nothing.
- Open a dialog (master change, tab order, rename slide) in one window, switch to another presentation, confirm: only
  the first document changes, and ⌘Z there undoes it.
- Notes/handout master in dark theme: the page edge follows the theme.
