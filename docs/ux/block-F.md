# Block F — Deck: status (interrupted at the first stable point)

## Done
- **Transitions** (`Presentations.UI/Motion/`): `TransitionCatalog` in the core (Subtle/Exciting/Dynamic groups, allowed
  directions = those that `SmilTransitionMapper` writes: no new loss), `TransitionThumbnail` animated on hover
  (uses `TransitionStage`, previous slide → current), `TransitionEditor` (`ThumbnailGallery` gallery, direction
  `IconChoiceGroup`, duration and "after" `MeasureField` in s, advance as icons, sound: list of the document's sounds +
  Browse…, "Apply to all"). Same editor in the dialog (with a large preview) and in the side panel; in the ribbon
  the command `deck.transition.gallery` (band gallery with animated thumbnails); the old per-type tiles remain
  only in menus and the palette.
- **Animations**: `AnimationPane` (click number, coloured icon per category, start icon, timeline bar from
  `AnimationTimeline`, drag and Alt+↑/↓ to reorder, Del, options with `IconChoiceGroup` and `MeasureField`,
  effects gallery by category with search and animated previews from `AnimationPresetPreview`). In the panel and in the
  "Effect" dialog (`AnimationDialogView`, immediate and undoable changes). Preset names in the five languages.
- **Background**: `Design/SlideBackgroundView` with just the `FillEditor`, whole-slide preview, "Hide background
  graphics", "Reset", "Apply to all" (a single undo step: `DeckActions.SetBackgrounds`).
- **Header and footer**: `Design/HeaderFooterView`, Slide / Notes and handouts tabs, preview with the placeholders
  highlighted (`FooterPlaceholderOverlay`), notes page on `PagePreview`.
- **Slide size**: `Design/SlideSizeView`, gallery of sizes with drawn proportions (A3 and Banner added),
  measure in `MeasureField` cm, orientation as icons, "Fit / Don't scale" as option cards; keywords
  for the palette.
- **Slide show setup**: `Design/ShowSetupView` in two columns, range as option cards with from/to thumbnails,
  advance as icons, pen with `SwatchStrip`, screens listed by name and resolution.
- Tests: `Presentations.Tests/MotionModelTests`, `Desktop.Tests/DeckMotionTests` (green at the previous commit).

## Resumption (second session)
- **Master**: `MasterGalleryView` (gallery with theme thumbnails for new, delete, change, with "Apply to all") and
  `MasterElementsView` (check boxes next to the master preview with the placeholders highlighted) wired into
  `DeckMasterCommands`.
- **Go to slide**: `SlidePickerView`, thumbnails with number and title, search, groups by section.
- **Custom shows**: `CustomShowsView` with two lists and thumbnails; dragging from the deck to the
  show and within the show (insertion mark), double click to add or remove; logic in
  `ICustomShowEditing` (core).
- **Photo album**: `PhotoAlbumView`, photos with thumbnail, order and removal, 1/2/4 layout as a gallery.
- **Export images**: `ImageExportView`, linked width and height in px (slide proportions),
  presets, all or just the current one, preview.
- Fixed: thumbnail of the "None" transition (no SMIL effect), Deck panels rebuilt on language change,
  architecture rule on constructed fields (`AnimationPresetPreview`).
- Tests: `Desktop.Tests/DeckDesignDialogTests` (background, header and footer, size, show setup, master,
  go to, custom shows, album, export), core in `MotionModelTests`. Full suite green.

## Closure (third session)
- **Date formats** in header and footer: `HeaderFooterSettings.DateFormat` (enum `HeaderFooterDateFormat`, thirteen
  values numbered like PowerPoint's `datetime1..13`). The table is shared data in the core
  (`IHeaderFooterDateFormats`): for each format the .NET pattern to display it and the sequence of parts (day, month
  as text or not, year, hours, minutes, seconds, AM/PM, separators) from which ODF writes the style. PPTX: the `a:fld` field
  carries `datetimeN` and is read back from there. ODP: `presentation:date-time-decl` with `style:data-style-name` pointing to a
  `number:date-style`/`number:time-style` in content.xml; on reading, the signature of the parts is compared with the table,
  and a style that does not match (written by another office suite with parts we do not offer) takes the closest format with
  **ODP215** (info). The format also travels in our extension (`dateFormat` in `urn:pulseoffice:headerfooter`).
  In the dialog a 3×N gallery (`ThumbnailGallery` with `DateFormatPainter`: today's example in the thumbnail, the name
  below), visible with "Automatic", replaced by the text field with "Fixed". Slide preview and notes
  page show the chosen format. No PPTX21x code: all thirteen formats pass.
- **Go to slide** as a command of its own: `deck.navigate.goTo` now in the ribbon (Home › Slides, own icon),
  menu (Slide › Navigate), palette with synonyms in the five languages, shortcut **Ctrl+G** (like Folio's Go to page);
  it opens the `SlidePickerView` gallery.
- **Review**: `DeckReviewSource.Edit` (with `Abilities.Edit`) edits the comment text with one undo
  step (`DeckActions.EditComment`, `Slide.ReplaceComment`, which keeps the position).
- Tests: `Formats.Odf.Tests/OdpHeaderFooterDateFormatTests` (thirteen formats without our extension, written style,
  ODP215), `Formats.OpenXml.Tests/PptxHeaderFooterDateFormatTests`, in `Desktop.Tests/DeckDesignDialogTests` the gallery
  of formats, editing and undoing a comment, the Go to slide command.

## Not done
- Position and size: assigned to block C.

## To try live
Thumbnail animation on hover (gallery, ribbon, effects), dragging in the animation pane, width of the
side panel (240 px) with the three-column gallery, light and dark themes; gallery of date formats in the
Header and footer dialog (scrolling, examples readable in the dark theme) and the date rendered in an ODP opened in another office suite;
Ctrl+G and the ribbon button; editing a Deck comment from the Review panel.
