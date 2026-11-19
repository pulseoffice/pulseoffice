# Round 254: Folio and PDF form leftovers

Area: queued items from rounds 250-252 on Folio note restarts, anchored object properties and relations, and PDF form
widgets. No build or test was run in the round (audit cycle rule); the coordinator runs them at the join.

## Defects found

| Severity | Defect | Status |
|---|---|---|
| Medium | DOCX `wp:anchor/@layoutInCell` and `@allowOverlap` were always written true and never read, so a Word object set not to lay out in its table cell or not to overlap lost the setting on the first save. | Fixed |
| Medium | Word's inside and outside margin relations were read as left and right (or top and bottom) margin, so on even pages of a mirrored document the object sat in the wrong margin, and the relation was lost on save. | Fixed |
| Medium | A PDF form field crossing pages joined its page fragments in `/V` with no separator, so a paragraph break at the page boundary vanished from the field value. | Fixed |
| Medium | Widget appearances were always Helvetica 10 pt WinAnsi: the control's font and size were ignored and characters outside WinAnsi showed as `?`. | Fixed (see Open) |
| Minor | Section note restarts happened only at page style runs; a section block was not a restart point. | Fixed |
| Minor | Continuation notices cannot be reached by a drag, Shift+click or Select All from body text. | Open |
| Minor | The bottom-margin vertical relation goes to ODT as `page` plus our attribute. | Open |

## Fixes

- `AnchoredObject.LayoutInCell` and `AllowOverlap` (both default true, Word's default): DOCX reads and writes them;
  ODT writes `style:flow-with-text="false"` when the object does not lay out in its cell (the native ODF property for
  "follow text flow", which other applications map to the same Word setting) and keeps allow-overlap in our
  `pt:allow-overlap` attribute. Snapshot, clone, assign and state comparison carry them.
- `HorizontalRelation.InsideMargin`/`OutsideMargin` and `VerticalRelation.InsideMargin`/`OutsideMargin`: placement
  mirrors them on even pages (horizontal inside is the left margin on odd pages, the right one on even pages; vertical
  inside is the top margin on odd pages, the bottom one on even pages). DOCX maps them one to one; ODT writes the
  nearest native relation (`page-start-margin`, `page-end-margin`, `page`) plus `pt:horizontal-rel` or
  `pt:vertical-rel`. The position dialog lists them, in the five languages.
- `PrintFormField.FirstSegment`/`LastSegment` carry the paragraph a page fragment starts and ends in; fragments from
  different paragraphs are joined with a line feed in `/V`.
- `PrintFormField.Font` carries the control's first run font from Folio. `PdfWidgetFonts` gives the form writer the
  export's font set and font objects, so a widget appearance is shown in that font and size with the same subset as the
  page (embedded fonts cover non-WinAnsi text); `/DA` names that font and `/DR` lists it. Without a font set, or a font
  on the field, Helvetica 10 pt stays the fallback.
- `FlowBuilder.NoteOrder` marks where a visible section block starts and ends as a section restart point.

## Research

ODF 1.3 `style:vertical-rel` has no bottom-margin, inside-margin or outside-margin values, and
`style:horizontal-rel` has page-start-margin and page-end-margin but nothing that mirrors by page side. A native bottom
margin form would be `page` with the offset converted to page coordinates, which needs the anchor page's geometry in
the ODT writer; it stays queued.

## Open

- Continuation notices: drag, Shift+click and Select All do not extend into them.
- Bottom-margin, inside and outside relations go to ODT as the nearest native relation plus our attribute; other
  applications measure from the page top or the start or end margin.
- An embedded subset used by a widget holds only the glyphs printed; a viewer that regenerates the appearance after
  the user types new characters may lack glyphs for them.
- None of this has been opened in Word, the reference suite or an external PDF viewer.
