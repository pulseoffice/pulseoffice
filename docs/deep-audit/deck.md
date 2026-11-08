# Deep audit: Deck

Area: `src/PulseOffice.Presentations.UI` (root and subfolders) and `src/PulseOffice.Presentations`. Reserved loss codes:
PPTX230–234, ODP225–229.

## Inherited work

Completed and committed the changes of an interrupted agent: `PrintMasterView` cancels the drag of the
frame when it loses capture; `SlideView` closes the freehand stroke on capture loss and releases
the filtered bitmap (greyscale, black and white) instead of leaving one behind on every draw; `DeckFacade.Existing`
says «No presentation is open.» instead of a misleading «the presentation has 0 slides». Tests:
`DeckPointerCaptureTests` (Desktop.Tests; fixed the stroke points, which fell outside the slide and
made the test ineffective), `DeckFacadeWithoutDeckTests`.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| ActiveDeckContext.cs | 106/106 | clean |
| AssemblyMarker.cs | 3/3 | clean |
| ControlAncestry.cs | 14/14 | clean |
| ControlTabOrder.cs | 28/28 | clean |
| CustomShowsView.cs | 422/422 | minor: every click on a row rebuilds all thumbnails (`RebuildSlides`); acceptable at the size of a modal |
| DeckActionRecorder.cs | 26/26 | clean: single passage point of the history, respects read-only |
| DeckActions.cs | 270/270 | clean (facade) |
| DeckAnimatedImageCommands.cs | 98/98 | clean: goes through `ReplaceElementsWith`, hence through the history |
| DeckAnimationPlayer.cs | 65/65 | clean |
| DeckArrangeCommands.cs | 356/356 | minor, fixed: `ReverseOrder` was `static` although it modified the document (rule 1) |
| DeckCanvasEditing.cs | 131/131 | minor, not fixed: the `ZOrder` of a freehand figure is `Elements.Count`, which with gaps in the numbering can coincide with an existing one |
| DeckCanvasMenus.cs | 107/107 | clean |
| DeckCharacterCommands.cs | 39/39 | clean |
| DeckClipboardCommands.cs | 92/92 | clean |
| DeckColorBar.cs | 78/78 | medium, fixed: a click on a colour with N objects selected made N history steps (⌘Z undid only one); now a single step with `ReplaceElements`. No automatic test: `DeckColorBar` is internal and has no test bench; to be tested live |
| DeckCommandSet.cs | 16/16 | clean |
| DeckCommands.cs | 344/344 | medium, fixed: closing the document during rehearse timings left the full-screen window open and, once that was closed, asked to apply the timings to a closed document; now the window closes with the document (as already in the slide show) and the question does not appear |
| DeckConnectorCommands.cs | 40/40 | clean |
| DeckConvertCommands.cs | 151/151 | medium, fixed: «Convert to bitmap» had no cap on the bitmap size (a large object off the slide requested hundreds of MB); now the maximum side is 4096 px. Same cap (2048 px) for the frames of `DeckAnimatedImageCommands` |
| DeckDesignActions.cs | 113/113 | clean: all changes go through `recorder.Do` |
| DeckDocumentFactory.cs | 89/89 | clean |
| DeckDocumentOperations.cs | 101/101 | clean |
| DeckDocumentService.cs | 70/70 | clean |
| DeckDrawCommands.cs | 90/90 | clean |
| DeckEditCommands.cs | 256/256 | medium, fixed: «Header and footer» once confirmed made three history steps (slides, notes, handouts); now a single transaction. No automatic test (the modal would need to be simulated); to be tested live with ⌘Z |
| DeckElementActions.cs | 362/362 | minor, fixed: `InsertMedia` did not bring the new element to the top (the `ZOrder` was the number of elements, which can be below the existing maximum); now as `InsertObject`. Test `DeckMediaZOrderTests` (fails without the fix). The same defect in `DeckCanvasEditing` is covered because it goes through `InsertObject` |
| DeckExportCommands.cs | 170/170 | medium, not fixed: video export keeps all BGRA frames in memory (100 slides at 1920 px ≈ 800 MB); the encoder interface must be restructured to accept a stream of frames (outside the area) |
| DeckExtraInsertCommands.cs | 127/127 | minor, fixed: the photo album copied every image twice. Note: added guides do not go through the history, consistent with the rest of the grid (view setting) |
| DeckFieldCommands.cs | 152/152 | clean |
| DeckFileInsertion.cs | 54/54 | clean |
| DeckFilmstrip.cs | 259/259 | medium, fixed: when renaming a section from the slides panel, Esc removed the box and the resulting loss of focus confirmed the new name anyway (and Enter confirmed it twice); now a flag closes the edit only once and Esc really cancels. No automatic test (internal class, no view test bench); to be tested live |
| DeckFindCommands.cs | 26/26 | clean |
| DeckFindTarget.cs | 57/57 | clean: replace all goes through the history and respects read-only |
| DeckFontworkCommands.cs | 77/77 | clean |
| DeckFormCommands.cs | 57/57 | clean |
| DeckFormControlCommands.cs | 57/57 | clean |
| DeckFormFieldCommands.cs | 106/106 | clean |
| DeckFormatCommands.cs | 112/112 | clean |
| DeckImageCommands.cs | 174/174 | minor, not fixed: the resolution check decodes every image instead of reading `PixelWidth` |
| DeckInsertCommands.cs | 383/383 | minor, fixed: the inserted text box (and the free text of `DeckTextInsertion`) goes through `InsertElement`, which did not bring it to the top; now it does. Test in `DeckMediaZOrderTests` (fails without the fix) |
| DeckInspector.cs | 221/221 | clean |
| DeckInteractionForm.cs | 68/68 | clean |
| DeckLinkCommands.cs | 76/76 | minor, not fixed: «Links» is enabled in read-only mode; update/unlink there do nothing (the history refuses) without saying so |
| DeckLinkResolver.cs | 27/27 | clean: relative paths are resolved but the file is not read until the user asks for the update |
| DeckMacroCommands.cs | 34/34 | clean |
| DeckMacroEventsSource.cs | 31/31 | clean |
| DeckMasterCommands.cs | 104/104 | clean |
| DeckMasterViews.cs | 168/168 | clean |
| DeckMediaCommands.cs | 48/48 | clean |
| DeckMediaFiles.cs | 35/35 | **serious, fixed**: a *linked* movie or audio whose path (relative, absolute or `file://`) points to a file that runs code (`.command`, `.app`, `.exe`, `.sh`…) was opened with `open`/`xdg-open`/ShellExecute when started in the slide show: opening a received presentation and clicking the media was enough to run a program. Now `DeckMediaFiles` uses `IExecutableFileTypes` (the same rule as embedded objects) and returns `BlockedMediaFile`; `DeckCommands` says so in the status bar (`deck.media.blocked`, five languages). Test `Un_media_collegato_a_un_file_che_esegue_codice_non_si_apre` (fails without the fix) |
| DeckModeViews.cs | 219/219 | minor, not fixed: in Outline view the top-level lines beyond the number of slides are silently discarded (they do not create new slides) |
| DeckMotionCommands.cs | 101/101 | clean |
| DeckNavigationCommands.cs | 102/102 | clean |
| DeckNavigatorSource.cs | 92/92 | clean |
| DeckObjectCommands.cs | 161/161 | clean |
| DeckObjectInsertion.cs | 135/135 | **serious, fixed**: the id of an inserted image or formula was `{slide}.img{number of elements + 1}`; after a deletion it could coincide with that of an existing element (insert two images, delete the first, insert a third: two `img2` elements). With duplicate ids `Find`, moves, undo and selection hit the wrong element. Now a Guid, like the other insertions. Also fixed the double decoding of the dimensions. No automatic test (internal class of the view); to be tested live |
| DeckOutlinePresentation.cs | 22/22 | clean |
| DeckPanelCommands.cs | 50/50 | clean |
| DeckPrintable.cs | 193/193 | clean |
| DeckProofing.cs | 63/63 | clean: events are unhooked in `StopProofing` |
| DeckPublisher.cs | 106/106 | clean |
| DeckReviewSource.cs | 88/88 | minor, not fixed: the Review panel declares comment editing also in read-only mode (the history then refuses it without saying so) |
| DeckRibbonCommands.cs | 336/336 | clean |
| DeckRichTextTransfer.cs | 73/73 | clean |
| DeckRulers.cs | 167/167 | medium, fixed: dragging an inner margin on the ruler of a box narrower (or shorter) than the opposite margins plus 1 pt called `Math.Clamp(x, 0, negative)`, which throws `ArgumentException` inside the pointer handler; now the maximum does not go below zero. No automatic test (the ruler would be needed in a window) |
| DeckShapeTextEditing.cs | 129/129 | clean |
| DeckShowActions.cs | 152/152 | clean |
| DeckSlideActions.cs | 336/336 | minor, not fixed: `SetBackgroundImage`/`SetBackgroundFill` do not skip the step when nothing changes |
| DeckSlideCommands.cs | 200/200 | minor, not fixed: «Insert slides from file» stays silent if the file cannot be read; the background image is not validated before insertion; «Remove background» can make two history steps |
| DeckSlideSetupCommands.cs | 35/35 | clean |
| DeckSnapshotProvider.cs | 34/34 | clean |
| DeckSpellingCommands.cs | 175/175 | clean |
| DeckStageKeys.cs | 117/117 | medium, fixed: Delete/Backspace with N objects selected made N history steps; now `DeleteElements`, a single step. Also aligned the «Delete object» command (menu/shortcut), which deleted only the main object of a multiple selection. No automatic test (view classes); to be tested live with ⌘Z |
| DeckStageNavigation.cs | 116/116 | clean |
| DeckStyleCommands.cs | 233/233 | clean |
| DeckTableBorders.cs | 134/134 | clean |
| DeckTableCommands.cs | 283/283 | clean |
| DeckTableLooks.cs | 62/62 | clean |
| DeckTextActions.cs | 151/151 | minor, not fixed: «Replace all» does not touch the notes, while «Find» walks them (`RewriteTexts` receives the notes only if the caller passes the function, and `DeckFindTarget` does not pass it) |
| DeckTextCorrection.cs | 32/32 | clean |
| DeckTextDialogCommands.cs | 209/209 | minor, not fixed: from the Character dialog the font cannot be reset to the theme default (an empty name is ignored) |
| DeckTextEditing.cs | 308/308 | clean |
| DeckTextFrameCommands.cs | 109/109 | clean |
| DeckTextInsertion.cs | 78/78 | clean (the `ZOrder` is now normalized in `InsertElement`) |
| DeckToolCommands.cs | 81/81 | clean |
| DeckView.cs | 589/589 | medium (performance), not fixed: every `deck.Changed` (every confirmed key, every move) rebuilds all thumbnails of the panel and the inspector (`Sync` → `BuildFilmstrip`); on presentations of hundreds of slides it is linear per change. A restructuring is needed (thumbnails reused by id), beyond the scope of this round |
| DeckViewCommands.cs | 92/92 | clean (grid and guides are view settings, outside the history as in the reference suite) |
| DeckViewOptions.cs | 67/67 | clean |
| DeckViewState.cs | 42/42 | clean |
| DeckZoomCommands.cs | 111/111 | clean |
| IDeckViewHost.cs | 39/39 | clean |
| PresentationOptions.cs | 24/24 | clean |
| PresenterWindow.cs | 214/214 | minor, not fixed: the presenter window marks every key as handled, system shortcuts too |
| PrintMasterView.cs | 213/213 | inherited fix done (lost capture); minor, fixed: fixed brushes and pens are no longer recreated on every draw |
| ShowSlideSetup.cs | 21/21 | clean |
| SlideImageRenderer.cs | 90/90 | clean |
| SlideKeyNavigation.cs | 27/27 | clean |
| SlideRenderer.cs | 224/224 | minor (performance), not fixed: on every draw the border pen, the background brush and a list of layers with closures are created; the layers serve layered rendering, but pen and brush could be kept per colour |
| SlideSorterStrip.cs | 57/57 | clean: reuses frames by stamp (`SlideThumbnailStamp`), the model the thumbnail panel of `DeckFilmstrip` lacks |
| TransitionStage.cs | 57/57 | minor, not fixed: a new `TransformGroup` per frame during transitions |
| PresentationWindow.cs | 670/670 | clean on the security side (links open only http, https, mailto; media go through `DeckCommands`, now protected). Minor, not fixed: «Run macro» and «Play sound» as an interaction in the slide show do nothing and the click advances; every key is marked handled |
| SlideView.cs | 1214/1214 | inherited fix done (lost capture in the freehand stroke, filtered bitmap released). Medium, fixed: a tiny grid spacing read from the file (`pulse:grid spacing="1"`) made it draw tens of thousands of lines on every frame and generate as many snap lines on every mouse move; now the effective step does not go below 4 pixels. Minor, fixed: brushes and pens of fixed colour (shadow, cells, guide lines, comments, grid, stroke in progress) are no longer created on every draw. Minor, not fixed: `ElementAt` ignores rotation (the unrotated frame is hit) |

## Coverage of the subfolders and the core

| File | Lines read | Outcome |
|---|---|---|
| Remote/DeckRemoteCommands.cs | 57/57 | clean |
| Remote/RemotePage.cs | 41/41 | clean: texts HTML-encoded, values in the script serialized as JSON. Minor, not fixed: a wrong PIN shows the login page again without saying it was wrong |
| Remote/RemoteView.cs | 71/71 | clean |
| Remote/WindowRemoteTarget.cs | 23/23 | clean |
| Motion/AnimationPane.cs | 451/451 | clean: duration, delay and repetitions have caps (600 s, 600 s, 100); out-of-range moves are clamped by `AnimationOrdering.Move` |
| Motion/TransitionEditor.cs | 354/354 | clean: duration and advance with caps (30 s, 3600 s) |
| Motion/AnimationPresetThumbnail.cs | 153/153 | minor, not fixed: a new `TransformGroup` per frame, only during mouse hover |
| Motion/TransitionThumbnail.cs | 182/182 | clean: the timer stops when the thumbnail leaves the visual tree |
| Motion/TransitionDialogView.cs | 79/79 | clean |
| Motion/DeckSlidePictures.cs | 69/69 | minor, fixed: frame pen and palette created on every draw of every thumbnail |
| Motion/AnimationDialogView.cs | 25/25 | clean |
| Motion/DeckMotionEditors.cs | 37/37 | clean |
| Motion/TransitionPainters.cs | 34/34 | clean |
| Exchange/DeckExchange.cs | 198/198 | clean |
| Exchange/DeckPortableTextBox.cs | 61/61 | clean |
| Exchange/DeckSelectionPackages.cs | 64/64 | clean |
| Fonts/DeckFontCollector.cs | 10/10 | clean |
| Design/HeaderFooterView.cs | 290/290 | clean |
| Design/SlideSizeView.cs | 237/237 | clean: measures between 1 and 150 cm |
| Design/ShowSetupView.cs | 232/232 | clean |
| Design/PhotoAlbumView.cs | 175/175 | medium, fixed: every thumbnail decoded the photo at full resolution (24 Mpx = ~96 MB) and again on every move or removal, without ever releasing it; an album of dozens of photos exhausted memory. Now the thumbnail is decoded once, at 128 px width; an image unreadable for any of the expected decoding exceptions gives an empty frame instead of bringing the modal down. Not covered by tests in this project (headless Avalonia needed): to be tested live |
| Design/MasterGalleryView.cs | 133/133 | clean (minor, not fixed: without actions `request.Actions[^1]` would throw; all calls pass at least one) |
| Design/FooterPlaceholderOverlay.cs | 43/43 | clean |
| Design/ImageExportView.cs | 113/113 | clean: width between 16 and 8192 px |
| Design/MasterElementsView.cs | 109/109 | clean |
| Design/SlideBackgroundView.cs | 113/113 | clean |
| Design/SlidePickerView.cs | 108/108 | clean |
| ObjectDialogs/AltTextDialog.cs | 27/27 | clean |
| ObjectDialogs/ChartEditDialog.cs | 41/41 | clean |
| ObjectDialogs/DeckObjectDialog.cs | 56/56 | minor, not fixed: the edited element is derived from the snapshot taken before the modal; if during the modal the agent (MCP) changes it, the confirmation overwrites that change (if it deletes it, `ReplaceElement` does nothing) |
| ObjectDialogs/DeckObjectDialogCommands.cs | 51/51 | minor, not fixed: `TryEditSelection` launches the modal without awaiting it (`_ =`), an exception would remain unobserved |
| ObjectDialogs/PositionSizeDialog.cs | 52/52 | clean: position and sizes with caps (three times the slide), rotation ±360; the `static`s are pure conversions |
| ObjectDialogs/ShapeStyleDialog.cs | 42/42 | clean |
| ObjectDialogs/SlideChartData.cs | 20/20 | clean |
| Editing/ShapeRichText.cs | 114/114 | clean |
| Rendering/ChromePainter.cs | 42/42 | minor, fixed: ink brush created on every draw |
| Rendering/DeckSelectionPicture.cs | 45/45 | medium, fixed: the PNG image accompanying every copy had the size of the selection at 2 px per point, without a cap; slide and object sizes come from the file without an upper limit (an ODP with `fo:page-width="1000cm"` and an object as large as the page requested a bitmap of billions of pixels and brought ⌘C down). Now the bitmap does not exceed 16 Mpx: beyond that, the scale is reduced. Not covered by tests in this project (Avalonia rendering needed) |
| Rendering/ElementPainters.cs | 117/117 | minor, fixed: two new brushes and a pen on every draw of every media |
| Rendering/SlidePagePainter.cs | 126/126 | clean: rasterization is limited to the area of the printed page |
| Rendering/TextFrameFitting.cs | 31/31 | clean |
| Scripting/DeckScripting.cs | 194/194 | clean: non-finite or out-of-range slide numbers give clear errors in English; changes only on a writable presentation. Minor, not fixed: the host's copy, cut, paste launched without awaiting them |
| Themes/DeckThemeCommands.cs | 132/132 | minor, not fixed: the cache of template previews (`_masters`) grows for every theme-size pair of every document opened in the session |
| Themes/ThemeDialogView.cs | 315/315 | clean |
| Themes/ThemeMasterLookup.cs | 9/9 | clean |
| Themes/ThemePreviews.cs | 102/102 | minor, fixed: with a slide of zero width (the PPTX reader accepts `p:sldSz cx="0"`) the ratio became infinite or NaN and the drawing frame invalid; now it falls back to 16:9. Palette no longer created on every draw |
| **Core** `src/PulseOffice.Presentations` | | |
| Remote/RemoteServer.cs | 239/239 | clean on the security side: 4-digit PIN from `RandomNumberGenerator`, constant-time comparison, lock after 10 wrong PINs, at most 16 connections with 10 s each, line at most 8 KB and 100 headers, `Cache-Control: no-store`, commands only via POST. Minor, not fixed (choice): the lock is permanent until shutdown, so anyone on the network can make the remote unusable with 10 attempts (turning it back on gives a new PIN); the PIN travels in clear over HTTP, as declared |
| Animation.cs | 84/84 | clean |
| AnimationBehaviourLibrary.cs | 62/62 | clean (embedded resource, trusted) |
| AnimationBehaviourResolver.cs | 71/71 | clean |
| AnimationBehaviours.cs | 236/236 | clean |
| AnimationSchedule.cs | 239/239 | medium, fixed: start and end were added in `int` without a cap, but delay and duration come from the file (PPTX `p:cond delay` up to `int.MaxValue`, duration from the natural duration of the behaviours): the sum overflowed to negative, the schedule ran backwards and the slide show's clicks skipped or stopped. Now summed in `long` and saturated at `int.MaxValue`. Test `DeckDeepAuditCoreTests`, verified to fail before |
| AnimationTimeline.cs | 68/68 | clean |
| PresentationController.cs | 128/128 | clean |
| AnimationPresetPreview.cs | 132/132 | clean: progress clamped between 0 and 1, a fallback motion that accepts everything |
| AnimationPresetRegistry.cs | 137/137 | clean: table shared between the two mappers, unknown presets carried as `ooxml-N` |
| AnimationSubtypeTable.cs | 77/77 | clean |
| CanvasContext.cs | 37/37 | clean |
| CustomShow.cs | 3/3 | clean |
| CustomShowNaming.cs | 24/24 | clean |
| DeckConnectors.cs | 73/73 | clean |
| DeckGrouping.cs | 101/101 | minor, not fixed: public `static` `Explode` with a closure counter (state) used as an id generator; the rest are pure computations |
| DeckSpelling.cs | 89/89 | clean |
| DeckTextRedaction.cs | 43/43 | clean (redaction deliberately flattens the paragraph into a single segment) |
| DeckTextSearch.cs | 150/150 | clean |
| Decorations/DecorationCanvas.cs | 107/107 | clean |
| ElementBehaviours.cs | 29/29 | clean |
| ElementIdRemap.cs | 27/27 | clean |
| EmbeddedStyled.cs | 22/22 | clean |
| FreeAreaPlacement.cs | 46/46 | clean |
| FreeformRefit.cs | 38/38 | clean |
| GridSettings.cs | 67/67 | clean (the spacing without a minimum is defended at the point of use in `SlideView`) |
| HeaderFooter.cs | 56/56 | clean |
| HeaderFooterXml.cs | 72/72 | clean: the date format read from the file is accepted only if it is a defined value |
| MasterEditing.cs | 34/34 | clean |
| Presentation.cs | 191/191 | clean |
| Slide.cs | 252/252 | clean |
| SlideElement.cs | 99/99 | clean |
| SlideMaster.cs | 70/70 | clean |
| SlideFactory.cs | 139/139 | medium, fixed: duplicating a slide (and importing from another file) silently lost all its groups, because `Clone` copied elements, animations, interactions and comments but not `Groups`; now groups are copied with ids renamed like those of the elements, nested groups included. Medium, fixed: the same for `Relayout` (changing the slide layout dissolved the groups). Tests `DeckDeepAuditCoreTests` (two), verified to fail before |
| SlideGroup.cs | 61/61 | clean: visit with a visited set, group cycles from the file do not recurse forever |
| SlideGroupEditing.cs | 207/207 | clean |
| SlideGroupRotation.cs | 64/64 | clean |
| SlideGroupUnits.cs | 83/83 | clean: parent chains and leaves protected from cycles |
| SlideLayoutRegistry.cs | 136/136 | clean |
| SlideNames.cs | 25/25 | minor, not fixed: `static` class with public methods (rule 1); `GeneratedRegex` requires a static method, but the class should become an injectable service |
| SlideOutlining.cs | 92/92 | clean: the album clamps photos per slide between 1 and 4 |
| SlideResize.cs | 100/100 | clean: zero or negative sizes leave the frames as they are |
| SlideSections.cs | 65/65 | clean |
| HeaderFooterDateFormats.cs | 158/158 | clean: PPTX type `datetimeN` accepted only if defined |
| MasterFactory.cs | 352/352 | clean |
| NativeAnimationPresetTable.cs | 222/222 | clean (shared data, rule 9) |
| OutlineDeckBuilder.cs | 47/47 | clean: levels clamped between 0 and 8 |
| PresentationFontNames.cs | 49/49 | clean |
| PrintMasters.cs | 83/83 | clean |
| RehearsalTimeline.cs | 18/18 | clean |
| ShowSetup.cs | 35/35 | clean |
| ShowStart.cs | 20/20 | clean |
| SlideBackgrounds.cs | 45/45 | clean |
| SlideComment.cs | 18/18 | clean |
| SlideFields.cs | 25/25 | clean |
| SlideSize.cs | 25/25 | clean (no upper cap: defended at the points of use, see `DeckSelectionPicture`) |
| SlideTextHyphenation.cs | 64/64 | clean |
| SlideThumbnailStamp.cs | 39/39 | minor, not fixed: `static Of` factory (rule 1) |
| SlideTransition.cs | 80/80 | clean |
| SoundLibrary.cs | 49/49 | clean |
| TextFrameDefaults.cs | 19/19 | clean |
| Theme.cs | 74/74 | clean |
| ThemeChoices.cs | 55/55 | clean |
| TransitionCatalog.cs | 68/68 | clean |
| CustomShowEditing.cs | 42/42 | clean |
| TransitionEffectFactory.cs | 144/144 | clean: unknown SMIL type and subtype fall back to dissolve |
| TransitionEffects.cs | 334/334 | clean: progress clamped between 0 and 1, fixed-size grids |
| Decorations/ThemeDecorations.cs | 303/303 | clean: the constructor verifies that every decoration is registered |
| ThemeCatalog.cs | 481/481 | clean |

## Summary

Coverage: 100 `.cs` files at the root of `src/PulseOffice.Presentations.UI` (no `.axaml` at the root), 14,814 lines
read in full. Subfolders not yet read.

Defects by severity:

- **Serious (2, fixed)**: linked media pointing to an executable opened in the slide show (`DeckMediaFiles.cs`, `DeckCommands.cs`);
  duplicate ids of inserted images and formulas (`DeckObjectInsertion.cs:42,93`).
- **Medium (fixed, 9)**: colour bar with N steps (`DeckColorBar.cs:59`); rehearse timings surviving the document
  (`DeckCommands.cs:235`); conversion bitmap without a cap (`DeckConvertCommands.cs:101`, `DeckAnimatedImageCommands.cs:61`);
  header and footer in three steps (`DeckEditCommands.cs:103`); Esc confirming the section rename
  (`DeckFilmstrip.cs:155`); `Math.Clamp` with a negative maximum in the rulers (`DeckRulers.cs:111,114,140,141`); multiple
  deletion in N steps and the Delete command on the main selection only (`DeckStageKeys.cs:100`, `DeckInsertCommands.cs:148`);
  tiny grid from the file (`SlideView.cs`); plus the inheritance (lost capture in `PrintMasterView` and `SlideView`, macros
  without a presentation, filtered bitmap).
- **Medium (not fixed, 2)**: video export with all frames in memory (`DeckExportCommands.cs:146`);
  full rebuild of the thumbnails on every change (`DeckView.cs:415`). Both require a restructuring.
- **Minor fixed**: `static` with side effects (`DeckArrangeCommands.cs:215`); `ZOrder` of media and text boxes
  (`DeckElementActions.cs`); double copy in the album; allocations while drawing (`PrintMasterView`, `SlideView`).
- **Minor not fixed**: listed row by row in the table.

New tests: `DeckPointerCaptureTests` (Desktop.Tests), `DeckFacadeWithoutDeckTests`, `DeckMediaZOrderTests`,
`Un_media_collegato_a_un_file_che_esegue_codice_non_si_apre`; each verified to fail without the fix.
`PulseOffice.Presentations.UI.Tests`: 46 out of 46.

Live tests for the coordinator: ⌘Z after the colour bar, Delete on several objects, header and footer; Esc in the
rename of a section from the panel; dragging an inner margin on the ruler of a very narrow box; closing the
document during rehearse timings; inserting two images, deleting one, inserting another and moving them; linked media
pointing to a `.command` in the slide show (the warning must appear); greyscale and black-and-white views.

To report outside the area: the same executable check should be verified for the linked media of Folio and Ledger;
`OdpGridXml` accepts any positive spacing (here defended at the point of use).

### Subfolders of `PulseOffice.Presentations.UI` and core `PulseOffice.Presentations`

Coverage: 45 `.cs` files of the subfolders (Design, Editing, Exchange, Fonts, Motion, ObjectDialogs, Remote, Rendering,
Scripting, Themes; no `.axaml`), 4,999 lines, and 73 `.cs` files of the core, 6,924 lines: all read in full.

Defects by severity:

- **Medium (fixed, 5)**: animation schedule overflowing with huge delays and durations from the file
  (`AnimationSchedule.cs:54-55`); groups lost when duplicating or importing a slide (`SlideFactory.cs:53`, `Clone`) and
  when changing its layout (`SlideFactory.cs`, `Relayout`); photo album thumbnails decoded at full resolution
  and never released (`Design/PhotoAlbumView.cs:144`); PNG image of the copy without a pixel cap
  (`Rendering/DeckSelectionPicture.cs:36`).
- **Minor fixed**: pens, brushes and palettes created on every draw (`Motion/DeckSlidePictures.cs`,
  `Rendering/ChromePainter.cs`, `Rendering/ElementPainters.cs`, `Themes/ThemePreviews.cs`); theme preview with
  a zero-width slide (`Themes/ThemePreviews.cs:82`).
- **Minor not fixed**: permanent lock of the remote after 10 wrong PINs and wrong PIN without a message
  (`Remote/RemoteServer.cs:195`, `Remote/RemotePage.cs`); object modal overwriting a change made
  by the agent during the modal (`ObjectDialogs/DeckObjectDialog.cs:47`); modals and clipboard launched without awaiting them
  (`DeckObjectDialogCommands.cs:44`, `Scripting/DeckScripting.cs:16-22`); growing cache of theme previews
  (`Themes/DeckThemeCommands.cs:22`); `static` against rule 1 (`DeckGrouping.Explode`, `SlideNames`,
  `SlideThumbnailStamp.Of`); `TransformGroup` per frame in the animation previews.

New tests: `DeckDeepAuditCoreTests` in `PulseOffice.Presentations.Tests` (three), each verified to fail without the
fix. `PulseOffice.Presentations.Tests` 302 out of 302, `PulseOffice.Presentations.UI.Tests` 46 out of 46. The two
rendering fixes (album, copy image) have no tests here: they require headless Avalonia.

Live tests for the coordinator: photo album with dozens of large photos (memory, moving and removing);
copying an object from an ODP with a huge `fo:page-width`; duplicating a slide with nested groups and changing its
layout, then ⌘Z; slide show of a PPTX with a huge `p:cond delay`; remote from a phone (right PIN, wrong PIN,
lock after 10).

To report outside the area: the PPTX reader accepts `p:sldSz` with zero or negative width or height
(`Formats.OpenXml/Presentation/Reading/DeckStructure.cs:66`) and neither ODP nor PPTX put an upper cap on the
slide size; the PPTX delay `p:cond delay` goes up to `int.MaxValue`
(`PptxAnimationMapper.cs:213`), here defended in the schedule.
