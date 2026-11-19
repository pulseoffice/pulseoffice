# Audit queue

Open items reported by audit rounds, waiting for the next cycle (user rule, 2026-11-18). An item leaves the queue when
the round that fixes it is joined and the join run is green.

## Deck
- The pipe audio players on Linux (paplay, pw-cat, aplay, ffplay) report no device position: the played time is
  estimated from the bytes written less the pipe capacity and the requested player latency, not measured. (194)
- Media "start automatically" is checked only by round trips in our own reader and writer; PowerPoint and the
  reference suite have not opened such a file here. (194)
- The built-in media player has not run live on any system (Windows and macOS cannot be tested here). (193)
- Mongolian lines are not mirrored inside shape or text box columns, and list labels of mirrored Mongolian paragraphs
  stay at the paragraph top. None of the East Asian, stacked or Mongolian modes has been seen live. Upright cells are
  widened by enlarging the hidden cluster's font size, which also makes East Asian vertical lines (columns) about a
  fifth thicker than the line height of the font; only the full-width punctuation that stands upright gets a vertical
  form (the dash and ellipsis turn with the line). (225, 230, 234)
- Vertical, 270-degree and upright shape text is now drawn and edited on the composed frame; this has not been seen
  live, and the drawing of these flows changed from the renderer's own formatted text to the composed frame, so
  spacing may differ slightly from earlier versions. Folio has no shape text flows. Long shape text column lines are
  probed per line assuming constant highlight tops along a horizontal line. (218, 230, 234, 240)
- ODF has no form for upright text or text box rotation: they keep our `urn:pulseoffice:shape-text` extension; the
  270-degree flow is now written as the extension writing mode `bt-lr` beside `tb-rl`, not yet opened in another
  application. Shapes still also write `style:text-rotation-angle`, which ODF defines only for cells, beside the
  standard `draw:text-rotate-angle`; its sign has not been checked against another application. (214, 227)
- Connector glue output (`p:cxnSp`, `draw:connector`, the automatic-end extension) has not been opened in PowerPoint,
  Excel or the reference suite here. (213, 214)
- Deck's Connector Ends dialog (shared `ConnectorSitesDialog`) has not been seen live; it is now also in the slide
  object context menu. (225, 227, 231)
- The slide drag indicator and threshold in filmstrip and sorter, outline cut and paste identity, and the Rename/Remove
  Empty Section Here commands, have not been seen in the running app. (245, 249)
- The last-block outline cut widens the selection to the line break before it from `CuttingToClipboardEvent`; it
  relies on the text box copying the selection before raising the event and deleting it after. Not seen in the
  running app. (253)
- The leading unsectioned slides are written to PPTX in a "Default Section" with a fixed identifier that reads back
  as no section. ODP empty sections are `urn:pulseoffice:presentation-section` elements inside `draw:page`. None of
  this has been opened in PowerPoint or the reference suite here. (229, 231)
- "Number slides from" 0 is written to ODP as our extension attribute and as `text:page-adjust="-1"` on every slide
  number field (also read back alone); not opened in another application. (231, 234)
## Charts
- Pivot chart links (XLSX `c:pivotSource` with the `[]` name form and multi-area `multiLvlStrRef` formulas; ODS
  the vendor extension attribute `data-pilot-source`) have not been opened in other applications; ODS pivot charts carry only leaf category
  labels for other readers. (182, 190, 195)
- Pivot chart categories keep the one-column `c:multiLvlStrRef` with the full multi-level cache (round 209 research:
  applications rebuild a chart linked by `c:pivotSource` from the pivot table, not from the category formula, and no
  cell range of a compact pivot holds the outer labels beside the leaves). Not yet opened in Excel to confirm the cached
  levels are kept until the first refresh. (195, 199, 209)
- Inline chart category levels are now native ODF label columns before the leaf column; other applications have not
  opened such a chart here. (199)

## Folio
- DOCX still loses line number restarts that no body section can carry: inside tables, frames, notes and comments, on a
  paragraph that defers a page style change, and any restart when numbering restarts on every page (DOCX248). A restart
  on the first paragraph is now the first section's starting number (235). Not yet opened in Word. (204, 209, 212)
- Folio notes (232, 250): a page style run starts only at a top-level paragraph with a page style (not at a table); Word
  has one footnote separator per document, so a page style's own line is declared (DOCX273). Continuation notices are
  selectable read-only text taken in by drags, Shift+click and Select All (260); copying body text copies the body
  only, as in Word (265). None of this has been opened in Word or the reference suite. (250, 254, 260, 265)
- RTF has no Hiragana list numbering (the `\levelnfc` codes 12, 13, 20 and 21 are all Katakana): Hiragana lists are
  written with Katakana numbers (RTF007). (199, 209)
- Folio object positions (235): vertical inside/outside alignment (Word only) is laid out as top/bottom and written to
  ODT as top/bottom; the vertical top-margin relation is written to ODT as `page` plus our own `pt:vertical-rel`, so
  other applications measure a top-margin offset from the page edge correctly but align within the whole page; the
  alignment relative to a paragraph now settles on the wrapped height in up to four passes (246) and, for a paragraph
  broken across pages, on its last line on the anchor page (250). Not opened in Word.
- Folio objects (251, 254, 260): bottom-margin and vertical outside-margin offsets are written to ODT from the page top
  (`page` relation, offset plus page height less bottom margin, our offset kept in `pt:y`), using the initial page
  style, so an object in a section with another page size gets an approximate native offset in other applications;
  inside and outside relations are native only for odd pages (even pages mirror in Word but not in the ODT form);
  aligned (not offset) objects in the bottom, inside or outside margin still align within the whole page in other
  applications. Allow-overlap goes to ODT only as our attribute. Not opened in Word or the reference suite. (254, 260)

- Folio TOC (259): `\t` is written with commas; not checked in Word under a semicolon list separator. (259)
- Folio tables (269): dragging a column edge of a percent table adds the dragged distance to the stored (unscaled)
  width, so on a table scaled to its percent the edge lands off the pointer by the scale factor. (269)
- Folio tables (269): Folio has no table style property editor (the style gallery picks presets); the first and second
  band shadings and their axes can only come from files or presets. (269)
- Folio tables (269): automatic width uses the stored columns as preferred widths and the measured longest word and
  unwrapped text of single-column cells; spanning cells, nested tables and Word's preferred cell widths (`tcW`) do not
  take part, and the result has not been compared with Word. (269)
- Folio tables (269): border conflicts: a `nil` side reads as no line and loses to the neighbour's line, a cell
  border against a table border follows the side-by-side merge on reading; a merged cell whose segments resolve to
  different lines keeps one line for its whole side, drawn over the neighbours' segments. Not compared with Word. (269)
- Folio TOC (269): the TOC styles tab lists the styles used in the document or already assigned; a style not yet used
  cannot be assigned from the dialog. Not seen live. (269)

## Shell and formats
- The Windows path of the private file spool has not run on a real Windows. (192)
- Tagged PDF: no PDF/UA identifier is written; the output has not been run through a PDF/UA checker, which is the
  condition for writing it. (239, 243)
- Tagged PDF: Deck text boxes and shape text (columns and rotated shapes included since 268) are tagged by the painted
  line boxes; not checked with a PDF/UA checker. (243, 247, 260, 265, 268)
- Delete the emptied `src/PulseOffice.Presentations.UI/Rendering/SlideTextLines.cs` at the join (audit agents cannot
  remove files). (265, 268)
- Shape autofit: "resize shape to fit text" grows Deck shapes only; Ledger and Folio shapes keep their size (they shrink
  text on overflow through the shared renderer). Shrink in shape text drawn through the rich composer (lists, vertical
  or stacked text) is not applied; text box shrink uses its stored font scale. The resize assumes the text area is the
  whole bounding box, not the inner text rectangle of the preset. (268)
- Shape autofit has not been opened in PowerPoint or the reference suite; ODP writes both `draw:fit-to-size` and
  `style:shrink-to-fit`. (268)
- Deck shape text list keys are tested on the model service (`ShapeListKeys`); not walked through the live editor in a
  headless key test or in the running app. Tab on a plain (non-list) paragraph still moves focus instead of inserting
  a tab. (268)
- PDF form fields: not yet opened in an external PDF viewer. (244)
- PDF form fields: a widget drawn with an embedded subset holds only the glyphs printed; a viewer that regenerates the
  appearance after the user types new characters may lack them. Not opened in an external viewer. (254)

## Ledger
- Sorted validation lists: XLSX has no sort setting; typed entries are written sorted, entries from cells keep cell
  order in Excel (declared XLSX244). Not opened in Excel or the reference suite. (253)
- Remove duplicates now compares shown text, so the text "1" and the number 1 shown as "1" are duplicates; Excel's
  behaviour for that mixed case has not been checked in Excel. (253)

- The relative-period list of the AutoFilter menu has not been seen in the running app nor walked with the keyboard.
  (216)
- The chart editor's table column picker and the missing-table placeholder and status warning (also announced when a
  workbook opens) have not been seen in the running app. (220, 245)
- XLSX table-reference rule forms with equal range and flat formula are matched by their rank in priority order (241);
  a reordering that changes their relative priority can still swap them. Not opened in Excel. (236, 241)
- Round 236 (Ledger tips and notes), open: the "edit objects and notes" permission rides on our own ODS attribute,
  which other ODF applications ignore (they keep objects locked); Folio's exact character hit skips rotated text frames; none of this was
  built, run or opened in Excel, Word, PowerPoint or the reference suite. (236)

- Round 261 (Ledger pivots), open: manual groups, label and value filters and slicers not modelled; ungrouped dates
  written as serials without format; compact layout keeps empty per-field columns, tabular parents on their own row,
  no blank lines; XLSX `rowItems`/`colItems` not written and not opened in Excel. (261)
- Round 266 (Ledger pivots), open: XLSX grouping (`fieldGroup`, derived years and days levels, date shared items) not
  yet opened in Excel; hidden items of a grouped field still declared (XLSX030); the field settings panel lists items
  and group labels by invariant key (`2024-01`, `2024-T1`, `TRUE`) instead of localized labels; calculated value fields
  are not editable in the dialog and their position among the value fields resets to after the placed ones; the edit
  dialog has no headless UI test of the settings panel. (266)
- Round 264 (Ledger pivots), open: Excel "Months" without years reads as month-and-year items; the x14 show-as
  extension and the `numFmtId` of data fields not yet seen in Excel. (264)
- Round 267 (date epoch), open: the workbook epoch reaches evaluation, typed and edited dates, cell display, TEXT,
  chart text, pivot grouping and Insert Current Date, but not yet the autofilter date groups and dynamic filters
  (`FilterColumnDates`, `FilterValueText`, `XlsxFilterDateGroups`, `XlsxDynamicFilterCodec`, `RelativeDatePeriods`),
  series fill by months (`SeriesFill`, `SeriesView`), `SheetDataTools`, query values (`QueryValueConverter`), cached
  date items of external pivot sources (`XlsxPivotExternalSources`) or ODF relative periods; display outside
  evaluation follows the active workbook's epoch, so a second open workbook with another epoch printed or exported in
  the background shows its dates in the active one's system. Nothing was built, run or opened in Excel or the
  reference suite. (267)
- Round 267 (Ledger pivots), open: ODS hidden members and named base items carry item names, so a text item that reads
  as a number or boolean ("5", "TRUE") cannot be told from the number or boolean there (the reference suite conflates
  them too); hidden members named "TRUE" or "5" are read as hiding both. GETPIVOTDATA canonicalises items only for row
  and column fields and the reduced table of filter fields; percentOfParentCol, next and named base items not opened in
  Excel; the pivot dialog does not offer the new modes or base items yet (left to round 266's dialog work). (267)

## Accessibility
- Accessible names were verified by source scan only; no screen reader (VoiceOver, Narrator, Orca) has been run over
  the ribbon, dialogs or panels, nor over the new polite live region of the status bar. (201, 203)
- Key tips: on Windows and Linux the in-window menu bar is closed when the tips open, because Avalonia's access-key
  handler opens it on the same Alt release; check live that the two never flicker together. Third-level tips cover the
  ribbon's own popovers (menus, galleries, split-button lists, collapsed and overflow groups) but not combo box
  drop-downs, the font family list, the submenus of the title-bar menus, or a popover opened outside the ribbon; badges
  outside the ribbon sit in the adorner layer, which was not checked live inside separate popup windows. (208, 211)
- Settings written by another running instance are reloaded through a file watcher; this was not run with two live
  instances, and whether an open settings dialog refreshes its editors on such a change was not checked. (225)
- The high-contrast theme has not been seen live. Colour literals in interface code are frozen at their round 225
  counts by `Round225HardCodedColourTests`; the remaining ones (document paper, previews of document content, shape
  shades) were not each reviewed for high contrast. The theme does not follow the operating system's
  own high-contrast setting. The cell edit is translated to the new formula separators on a language switch only for
  formulas; plain entries keep the text as typed. Not run live. (220)
