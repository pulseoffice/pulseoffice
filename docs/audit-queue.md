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
- Folio tables (263): a percent-width table keeps its percent when columns are resized or the table width is set in
  the table dialog; the dialog has no percent or automatic width control, and the layout scales the stored columns
  rather than running Word's automatic-fit algorithm for `AutoWidth` tables. (263)
- Folio tables (263): the first-band shading has no control in the table style editor (only the second band is
  edited); a style that shades only `band1Vert` also shades rows when row banding is on. (263)
- Folio tables (263): border conflicts follow width times style number, style rank and colour brightness; Word's
  special cases (a `nil` border, a cell border against a table border, conflicts at merged cells along spans other than
  the first column or row) are not modelled, and the result has not been compared with Word. (263)
- Folio TOC (263): the TOC dialog lists every visible paragraph style as a number field on its own tab; with many
  styles the tab is long and the style names are shown as stored (not localized). Not seen live. (263)

- Folio tables (join of 262-265): with Word's border conflict rules a shared edge between two cells is drawn by both
  cells at identical bounds; draw it once. `TableAppearanceLayoutTests.IBordiPropriDellaCellaSiDisegnanoLatoPerLato`
  was updated to the new rules at the join and should be reviewed with that change.

## Shell and formats
- The Windows path of the private file spool has not run on a real Windows. (192)
- Tagged PDF: no PDF/UA identifier is written; the output has not been run through a PDF/UA checker, which is the
  condition for writing it. (239, 243)
- Tagged PDF: Deck text boxes and shape text are tagged by the painted line boxes (265); shape text in columns falls
  back to text matching, and a shape rotated inside its element is located by axis-aligned boxes. Not checked with a
  PDF/UA checker. (243, 247, 260, 265)
- Deck shapes have no autofit (shrink text, resize shape to fit); check whether PPTX `spAutoFit`/`normAutofit` and ODP
  `draw:auto-grow-height`/`draw:fit-to-size` on shapes are lost silently and map them. (265)
- Deck shape text lists: Tab/Shift+Tab level change and Enter on an empty item not walked in the running app. (265)
- `src/PulseOffice.Presentations.UI/Rendering/SlideTextLines.cs` is unused since round 265; delete it at the join. (265)
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

- ODS `table:null-date` other than 1899-12-30 is ignored and XLSX 1904 workbooks shift only date-formatted constants
  (plain numbers used as dates by formulas are not): both need a workbook date epoch in `CalculationSettings`, read
  and written by both codecs, honoured by the `IDateSystem` consumers through the evaluation context and by the format
  renderer, replacing the 1904 load shift. (258, 262)
- Round 261 (Ledger pivots), open: no interface to edit an existing pivot (show values as, base field, number format,
  grouping, sorting, filters, layout, totals, repeat labels), to be built on the creation dialog (261, 264); XLSX
  grouping still not written (XLSX030, needs `fieldGroup`/`rangePr`/`groupItems` with derived years for months across
  years, checked in Excel; reading is done) (261, 264); manual groups, label and value filters and slicers not
  modelled; ungrouped dates written as serials without format; compact layout keeps empty per-field columns, tabular
  parents on their own row, no blank lines; XLSX `rowItems`/`colItems` not written and not opened in Excel. (261)
- Round 264 (Ledger pivots), open: a text item "TRUE"/"FALSE" shares the boolean's key and renders as a boolean;
  subtotal labels of boolean items show the invariant key; GETPIVOTDATA does not accept localized date labels; base
  item other than "previous" not modelled; Excel percentOfParentCol reads as row parent; Excel "Months" without years
  reads as month-and-year items; the x14 show-as extension and the `numFmtId` of data fields not yet seen in Excel.
  (264)

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
