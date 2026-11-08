# Round 109: layout gallery by identity

The open point of round 107 (docs/deep-audit/round-107-deck-masters-2.md): the ribbon showed one tile per layout
type, so custom layouts (several of a type, several masters) could not be chosen from the interface.

## What changed

### Model (`PulseOffice.Presentations`)
- `ISlideLayoutCatalog` / `SlideLayoutCatalog` (new, injectable): find a layout by identity, tell the standard one of a
  type (the first of its type in the master) from custom ones, rename (custom only, trimmed, blank refused), duplicate
  (copy right after the source, fresh `layout.custom.<n>` identity unique in the master), remove (custom only).
- `MasterEditing` edits a layout **by identity**. Defect fixed on the way (medium): the type-based overloads changed
  every layout of that type, so moving a placeholder of the standard Title and Content in the master view also moved
  it in every custom layout of the same type. The type overloads now resolve `Template(kind)` and edit only that one.

### Actions (`PulseOffice.Presentations.UI`)
- `DeckLayoutActions` (reached as `DeckActions.Layouts`): `AddSlide(master, template, after)`,
  `ApplyLayout(slide, master, template)` (a layout of another master assigns the master and the layout in one undo
  transaction), `RenameLayout`, `DuplicateLayout`, `DeleteLayout`. Deleting moves every slide of that master that uses
  the layout to the standard layout of the same type (`ISlideFactory.Relayout`) in the same single undo step.
- `DeckActions.SetPlaceholderBounds(layoutId, …)`: the master view edits the shown layout by identity.

### Ribbon
- `SplitGallerySpec` (shared, `PulseOffice.UI.Commands`) and `SplitGallerySlot` (shared, `PulseOffice.UI.Controls`):
  a split button whose main part runs the command and whose lower part opens the shared `ChoiceGrid` (sections,
  thumbnails, current one checked). Usable by Folio and Ledger as any other slot; registered in the slot catalog.
- `deck.slide.add` (moved to `Design.DeckLayoutCommands`): split button; main part adds a Title and Content slide as
  before, the gallery adds a slide on the chosen layout of any master.
- `deck.layouts.gallery` (Design ▸ Layout): band gallery of every layout by identity; the current slide's master
  first, grouped under the master names when the deck has several; the current layout checked; choosing applies
  `SetLayout(Slide, SlideLayoutTemplate)` as one undo step.
- `ILayoutChoices` / `LayoutChoices` builds the entries (key = master id + layout id), `LayoutThumbnail` +
  `LayoutThumbnailPainter` + `ILayoutPreviews` render each thumbnail from the layout itself (placeholders, master
  and layout objects, background). Standard layouts show the translated type name; custom ones their own name.
- The per-type `deck.layout.*` commands leave the ribbon and stay in the Slide menu, the context menu and the palette.

### Master view and menu
- The master view picks layouts by identity (not by type), with a name field (custom layouts; Enter or leaving the
  field renames, one undo step), Duplicate and Delete buttons; the chosen layout is kept in `DeckViewState`
  (`MasterLayoutId`, exposed on `DeckTarget`).
- Slide ▸ Master: `deck.layouts.rename` (prompt modal with veil), `deck.layouts.duplicate`, `deck.layouts.delete`,
  enabled only in the master view; rename and delete only for custom layouts.
- Texts in the five languages.

## Tests
- `tests/PulseOffice.Presentations.Tests/SlideLayoutCatalogTests.cs` (three): duplication identity and placement,
  rename/remove refused on standard layouts and blank names, type-based edits no longer touch custom siblings.
- `tests/PulseOffice.Presentations.UI.Tests/DeckRound109LayoutGalleryTests.cs` (seven, headless with
  `UiThreadSession` for the slot): gallery by identity with the current one checked; custom layout applied with one
  undo step; several masters grouped and a layout of another master applied in one step; split button adds on the
  chosen layout or on Title and Content; deleting a custom layout moves its slides to the standard one in one step and
  undo restores master and slides; duplicate selects the copy, rename of a standard layout refused; the split slot
  runs the command from its main part and offers one tile per layout with one checked.

## Remaining
- The master view still edits the primary master only (as before this round); layouts of secondary masters are listed
  and applicable from the gallery but are edited through the master of the deck. To try live: Design ▸ Layout gallery,
  Home ▸ New slide arrow, View ▸ Master with Duplicate / Delete / name field, then undo.
