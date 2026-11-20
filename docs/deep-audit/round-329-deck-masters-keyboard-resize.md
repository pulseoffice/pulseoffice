# Round 329 (2026-11-20): keyboard object resizing, per-tab module contexts, Deck masters and layouts

Area: the two queue items (Shift+arrow resizing, the shared context slot of two tabs of one document), then a fresh
audit of Deck slide masters and layouts: master editing, placeholder inheritance, applying layouts, several masters,
renaming, slides between decks and the PPTX and ODP round trips of masters and layouts. No builds or tests were run
in the round (audit cycle rule).

## Defects found

| # | Severity | Defect | Fix |
|---|---|---|---|
| 1 | Medium | `ObjectNudgeKeys` made Shift+arrow a large nudge in Ledger, Deck and Folio; Office resizes the selected object with Shift+arrow and moves it finely with Ctrl+arrow. | `ObjectNudge` gains a kind (move or resize) and a fine flag: arrow moves, Ctrl or Alt+arrow moves finely, Shift+arrow resizes (Right and Down grow, Left and Up shrink, never below a minimum), Ctrl+Shift+arrow resizes finely. Ledger resizes every selected object (5/1 px), Deck through `DeckCanvasEditing.ResizeBy` (120/15 twips), Folio through `IFolioObjectMover.Resize` (5/1 pt, refused in protected content). `docs/architecture.md` updated. Tests: `SharedObjectViewTests`, `FolioObjectTests.ShiftArrowResizesTheSelectedObjectAsInOffice`. |
| 2 | Medium | Two views of the same document as tabs in one window shared that window's single module context slot, so a background tab that attached (sheet change, reload, macro claim) took over the ribbon, commands and agent of the active tab. | `WindowScopedSlots` keys slots by window and view; the current slot follows the active tab (falling back to the window's last attached slot while the active tab has none), a tab switch raises the focus change, closing or detaching a tab drops its slot. `IWindowScopeLocator.DocumentShowing` finds the tab of a view. Test: `Round329SameDocumentTabContextTests` (Desktop). |
| 3 | Medium | The master view edited only the primary master: a second master (theme applied to some slides, a PPTX with several masters) could not be edited, renamed or recoloured, and the layout commands (rename, duplicate, delete) always worked on the primary master. | The master view has a master picker and a master name box; geometry, background and layout edits go to the chosen master (`DeckViewState.EditedMasterId`, `DeckTarget.EditedMaster()`, `Presentation.MasterById`/`ThemeOfMaster`); `DeckDesignActions.RenameMaster` and per-master `SetMasterBackground`. Test: `Round329MasterEditingTests`. |
| 4 | Medium | Moving a placeholder in the master view moved every placeholder of the same role in the layout (both bodies of Two Content), and slides on that layout kept the old geometry, unlike PowerPoint where untouched slide placeholders follow the layout. | Placeholders are edited by index (`MasterEditing.WithPlaceholderBoundsAt`); slides of that master and layout whose matching placeholder still has the layout's old bounds move with it, in the same undo step. Test: `Round329MasterEditingTests`. |
| 5 | Medium | Change Slide Master and applying a theme to one slide only switched the master id: the slide kept the old master's layout id and placeholder geometry. Deleting a master did the same for its slides. | `DeckLayoutActions.ChangeMaster` assigns the master and relayouts each slide on the new master's layout of the same type and name (falling back to the type), in one undo step; `DeckLayoutActions.DeleteMaster` moves the slides that way before deleting. Tests: `Round329MasterEditingTests`. |
| 6 | Medium | ODP lost further layouts of the same type (custom and duplicated layouts, layouts read from PowerPoint), their names, geometry and backgrounds, and every slide's layout identity; a duplicated layout with its own background wrote two master pages with the same name. | A `qm:layouts` catalog on each master page (id, name, type, hidden master objects, background, placeholder geometry in twips) and `qm:layout-id` on each page, read back by `OdpLayoutCatalogXml`; layout master pages are written once per type. ODP107 narrowed to the decoration of further layouts; ODP022 reworded (geometry and backgrounds now return). Test: `OdpSecondMasterLayoutTests.Further_layouts_of_the_same_type_keep_name_geometry_background_and_their_slides`. |
| 7 | Medium | Insert Slides from File always dropped the source master: the slides took the primary master with a layout id from the other file. | The command asks (shared `ChoicePromptView`) whether to use this presentation's theme or keep the source formatting; keeping it adopts each source master (with its theme and layouts) under a new id, in one undo step. Test: `Round329MasterEditingTests`. |
| 8 | Medium | Whole slides could not be copied or pasted: copy needed selected objects. | In the slide sorter, copy and cut take the current slide (`DeckSlidesNative`), paste inserts it after the current slide: within the same deck it keeps its master, into another deck it takes the destination theme (`DeckSlideActions.PasteSlides`). Test: `Round329MasterEditingTests`. |

## Reviewed without defects

- `SlideLayoutCatalog` rename, duplicate and delete (standard layouts protected, read-only refused, one undo step).
- PPTX: several masters, custom layouts (`PptxLayoutIdentity`), layout and master names, layout backgrounds and
  hidden master shapes round-trip; slide layout identity follows the layout part.
- `DeckLayoutActions.ApplyLayout` across masters (round 170) and `SlideFactory.Relayout` matching.

## Still open (queued)

- Slide clipboard: only the current slide in the sorter; no multi-slide selection, no copy from the normal view's
  filmstrip, and no "keep source formatting" paste mode (Insert Slides from File has it).
- PPTX standard layouts read from PowerPoint take our standard names, and are written with type names, so a
  PowerPoint layout name such as "Title, Content" is not kept.
- ODP: the decoration of a further layout of a type comes back as that of the first layout of its type (ODP107).
- None of the new keyboard resizing, the master picker and name box, the formatting prompt or the slide clipboard
  has been seen in the running app; the arrow keys on objects have no in-app list (they are not commands).
