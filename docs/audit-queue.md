# Audit queue

Open items reported by audit rounds, waiting for the next cycle (user rule, 2026-11-18). An item leaves the queue when
the round that fixes it is joined and the join run is green.

## Deck
- Find (336): a notes match is selected in the notes box, but the other notes matches have no background
  highlight; whole words, regular expressions, table cell selection and notes replacement not tried live. (331, 336)
- The iterated reveal, the pause countdown and the last-slide sound cut extension have not been seen live or opened in
  PowerPoint. (286)
- An iterated zoom still scales each unit about the element's centre, not the unit's own centre: the reveal works on
  character ranges and the renderer has no per-unit glyph bounds to scale about (needs the composed line geometry of
  the text painters). (292, 297)
- Deck table cell rich editing: shared editor, overlay, per-paragraph alignment, struck runs and turned or stacked cell
  editing not seen live. (311, 316)
- The PPTX "PulseOffice Grid" table style and the ODP grid template have not been opened in PowerPoint or the
  reference suite; an ODP grid table from another producer keeps the default accent (only our own template names
  carry it). (302, 306)
- Turned and stacked Deck table cell text and the new cell layout dialog have not been seen live. (298)
- Per-unit fly and zoom of iterated text have not been seen live. (292)
- Iterated text, the sound cut, the pause screen and the new dialog fields have not been seen live. (283)
- Looping repeats, effect sounds, kiosk keys and timings and the hidden start slide have not been seen live. (278)
- Transition sounds and sound actions in the show, the trigger-only advance, the show type and custom show settings
  have not been seen live or opened in PowerPoint or the reference suite. (274)
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
- Deck (329, 332): keyboard resizing, the master picker and name box, the Insert Slides from File formatting prompt,
  the slide clipboard (sorter and filmstrip, Ctrl and Shift clicks, the selected-slide highlight, Paste Special's
  "Keep source formatting") and the object nudge commands have not been seen in the running app. (329, 332)
- Deck (332, 336): no paste options button after pasting slides (PowerPoint's floating Ctrl menu); the choice is only
  in Paste Special. Needs a shared floating paste-options component wired into the three modules. Shift+Up/Down
  extension and group drag (336) not tried live. (332, 336)
- Deck (332): the slide selection is highlighted with the `Pressed` token; not checked in high contrast. (332)
- Deck (332): PPTX layout names written by other applications for standard layouts are now kept and shown instead of
  the localized standard name; a file whose standard layouts carry an English PowerPoint name shows that English name
  in another interface language, as PowerPoint does. Not verified against PowerPoint itself. (332)

- Shortcuts (337): the shortcut editor has no export or import of customizations to a file (Office offers it for
  ribbon customizations); medium. (337)
- Shortcuts (337): Replace is Ctrl+Shift+H in all three modules, while Word, Excel and PowerPoint use Ctrl+H on Windows
  and Linux and Control+H on macOS (Cmd+H is reserved there); a per-system default needs the host in the module command
  sets. Minor. (337)
- Shortcuts (337): Ledger binds Ctrl+Plus and Ctrl+Minus to zoom, where Excel and the reference suite insert and delete
  cells; Ctrl+Shift+A sorts ascending, which Excel uses for argument names. Minor, needs a user decision on defaults. (337)
- Shortcuts (337): Office defaults not found among the command defaults (some may live in view key maps; check
  first): Deck Ctrl+M new slide (Cmd+M is reserved on macOS), Folio Ctrl+Space
  clear character format, Ctrl+= and Ctrl+Shift+= subscript and superscript, Ctrl+] and Ctrl+[ font size, Ledger Ctrl+5
  strikethrough, Ctrl+9 hide rows, Alt+= AutoSum, Ctrl+Shift+$ and Ctrl+Shift+% number formats. Minor. (337)
- Shortcuts (337): a nudge can be reassigned only to a chord with a command modifier, so the factory plain-arrow and
  Shift+arrow gestures can be restored but not chosen again by key capture. Minor. (337)
- Shortcuts (337): the rebound object nudges and Ctrl+N have not been tried in the running app. (337)

## Charts
- Per-point formats (`c:dPt`, `chart:data-point`) have not been opened in Excel or the reference suite. (318)
- Best fit data labels avoid earlier labels and the plot marks with a greedy pass (no global search); ODS writes them
  as `chart:label-position="avoid-overlap"`, not opened in another application. (323, 327, 333)
- Pie of pie split settings (XLSX `c:splitType`/`c:splitPos`/`c:custSplit`/`c:secondPieSize`, ODS our extension
  attributes) and XLSX open-high-low-close tick markers have not been opened in Excel or the reference suite. (327)
- The native chartex part written for treemap, sunburst, histogram, Pareto, box plot, waterfall and funnel (behind
  `mc:AlternateContent Requires="cx1"`/`"cx2"`) has not been opened in Excel. (328, 330, 334)
- No editor for the statistical layout (binning, quartile method, mean and outlier visibility, subtotals, parent
  labels): they only come from files. (334)
- Waterfall subtotal indices do not follow row insertion or deletion in the source. (334)
- Style parts of a chart read only from a chartex part (Excel file without our classic fallback) are not kept. (334)
- The chartex category dimension written from inline data carries only the leaf level. (334)
- Treemap labels do not wrap; the overlapping parent label sits in the middle of the parent. (334)
- Exclusive quartiles clamp for very small samples; Excel's drawing of such boxes not checked. (334)
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
- Folio tables (271): border conflicts between two explicit sides follow the ECMA weight rule, not compared with Word;
  automatic width with nested percent tables and percent `tcW` not compared with Word. (271)
- Folio tables (271): the TOC "Add a style" field and the table style editor have not been seen live. (271)
- Folio tables (279): corner-cell formats go to ODT only as our `pt:table-region` (ODF 1.3 templates have no corner
  parts). (279)
- Folio tables (285): DOCX has no "no font" or "no size" in a conditional format, so a cleared region font or size in a
  derived style is not written to DOCX and Word shows the parent's value. (285)
- Folio tables (285): template part paragraph styles not yet opened in another ODF application. (285)
- Folio tables (279): the native table templates have not been opened in another ODF application; the new editor
  fields (italic, font, size, line) not seen live. (279)
- Folio tables (275): percent `tcW` without a grid is resolved against a nominal 6.5 in text width; the layout scales
  the result to the page, but the proportions are not compared with Word. (275)
- Folio tables (275): the new style editor tabs (total row, columns, corners) and "no colour" in derived styles have
  not been seen live. (275)

- Folio mail merge (291, 296): the DOCX `w:mailMerge` source (ODBC `DSN=` connect string, `w:query`, connection id
  and recipient selection in reserved document variables) has not been opened in Word; the wizard steps (records,
  recipients with and/or conditions and sorting, output) have not been seen live. (291, 296)
- Folio mail merge (305): the key column for excluded recipients is offered only when every record has a distinct,
  non-empty value; without one the record hash remains, with its limits (edited records forgotten, identical records
  excluded together). The new key chooser has not been seen live. (305)
- Folio fields (291, 296, 301): none of the round 289, 291, 296 and 301 field changes opened in Word or the reference
  suite. Word has no quarter or week-of-year date picture: since round 305 the DOCX picture quotes them (Word shows
  the letters) and `w:fldData` carries our picture; not opened in Word, and a MERGEFIELD date picture with `Q` or `W`
  still has no field data. The ODF page field rule (`style:num-format="1"`
  forces Arabic, absent follows the page style, as ODF 1.3 says) still needs a look at real producers' files. (289,
  291, 296, 301)
- Folio headers (305, 308): ODF has no section fields: `SECTIONPAGES` is written as `text:page-count` with our marker
  (other readers show the document page count) and `SECTION` as our own element (other readers show its stored text).
  No insert command offers them yet. Since round 308 body sections that become continuous Word sections count as
  sections, but a DOCX continuous section that changes neither columns nor line numbering is still not kept in the
  model, so it neither counts nor round-trips; a section starting mid-page counts from the next page. (305, 308)
- Folio styles (308): built-in Word style names are now written as built-in with Word's `w:uiPriority` and
  `w:qFormat`; not checked in Word's gallery. A model style whose name differs from Word's only in case (for example
  "Header") is treated as built-in while keeping its own spelling. (308)
- Folio lists (315): ODF has no native restart level for list levels (researched in ODF 1.3 list level styles and
  `text:list` attributes); our `pt:restart-after` stays, so other applications restart after any higher level. (313, 315)
- Folio columns (320, 325): the ODT next-column start writes `fo:break-before="column"` on the section's first paragraph,
  leading table or nested section's first paragraph; not opened in other applications. Page columns marked to balance
  before a section not opened in Word. (320, 325)
- Folio find and replace (331, 336): replacing a `^b` at a `SectionBlock` boundary now dissolves the section (336),
  not compared live with Word; a match inside a comment is marked in the comment
  balloon while the anchor is selected, the comment text itself is still not editable in place. (326, 331)
- Find panel (326, 331): the panel, Folio, Ledger and Deck highlights, the comment balloon marks and the dialog's
  format expander have not been tried in the running app. (326, 331)
- Folio pages (320): the blank page before a right or left page style keeps the previous style (Word places it at the
  end of the previous section, the reference suite likewise); not compared live in Word. (315, 320)

- Images (282): a watermark tone with extra brightness or contrast is written to DrawingML as one combined `a:lum`
  and reads back as a standard tone with the same look. (282)
- Images (282): the picture corrections panel has not been seen live; Word and PowerPoint have not opened our
  `a:biLevel` and washout pictures. (282)
- Images (287): the aspect ratio lock flags (`picLocks`, `spLocks`, `graphicFrameLocks`) have not been opened in Word,
  PowerPoint or Excel. (287)
- Images (287): compressing all pictures and the Size dialog lock checkbox are tested at service and format level, not
  through a headless walk of the three modules' commands. (287)

- Folio rendering of the complex DOCX/ODT report (after the footnote citation and ODT header height fixes): the DOCX
  path draws the text of a vertically merged first-column cell outside the table's left edge (R1 at 55 pt with the
  table at 72 pt); ODT tables start at 72.25 pt and their rows sit about 1 pt lower than DOCX; a landscape ODT page
  reports two column areas though it is single-column; legal-article list labels are drawn glued to their text
  ("Art. 1Definizioni") in both formats; after reading a dynamic-spacing ODT header, the spacing/height split is not
  kept on the next save. (Folio render diff)

## Shell and formats
- The Windows path of the private file spool has not run on a real Windows. (192)
- Shell (314): the notification history is application-wide by decision. (314)
- Shell (322): the per-window module contexts are covered by headless two-window tests; the switch between windows has
  not been tried in the running app on the three systems. (322)
- Shell (329): module context slots are now per tab (window and view); the tab switch was covered headless only, not
  tried in the running app. (329)
- Shell (321): activating a window re-claims the module context and keeps a torn-off window's maximized state, but the
  multi-window context switch has not been tried in the running app. (321)
- Shell (310): multi-window behaviour (tab drops on a scrolled strip, side panel, status, placement) not yet tried in
  the running app on the three systems, nor the native macOS menu with two windows. (310)
- Shell (303): the keyboard tab move (Ctrl+Shift+PageUp/PageDown) takes the gesture Ledger would use to select sheet
  groups; Ledger has no sheet group selection yet, revisit when it gets one. (303)
- Tagged PDF: no PDF/UA identifier is written; the output has not been run through a PDF/UA checker, which is the
  condition for writing it. (239, 243)
- Tagged PDF: Deck text boxes and shape text (columns and rotated shapes included since 268) are tagged by the painted
  line boxes; not checked with a PDF/UA checker. (243, 247, 260, 265, 268)

- Shape autofit (276): Folio shape stories have no vertical text layout, so vertical text in a Folio shape is not
  resized; the stored size settles only when an editor lays the document out (a headless conversion keeps the saved
  size). (276)
- Shape autofit has not been opened in PowerPoint or the reference suite; ODP writes both `draw:fit-to-size` and
  `style:shrink-to-fit`. (268)
- Shape text keys (272): Tab, Shift+Tab, Enter and Backspace are walked through the live shared editor in a headless
  test; not yet tried in the running app. (268, 272, 276)
- PDF form fields: not yet opened in an external PDF viewer. (244)
- PDF form fields: a widget drawn with an embedded subset holds only the glyphs printed; a viewer that regenerates the
  appearance after the user types new characters may lack them. Not opened in an external viewer. (254)

## Ledger
- Find and replace (331, 336): values found through the displayed text are replaced in the stored text, so a match on
  formatting characters (thousands separator, currency) replaces nothing; the find highlights ignore the selection
  restriction (336); text in drawn
  shapes is neither searched nor highlighted; highlights inside frozen panes use the scrolled visible range; the
  format filter cache of the highlights follows cell revisions, not style-only changes. (331)
- Sparklines (335): Edit Group rewrites a group as one block laid out from the bounding ranges of its data and cells; a
  sparkline whose source sheet or rows are deleted is dropped instead of kept with a `#REF!` source as in Excel (undo
  restores it); Group and Ungroup are not in the cell context menu. (335)
- Sparkline options (markers, axis, empty cells, hidden data, custom bounds, date axis, foreign sources) not yet opened
  in Excel or the reference suite; the ODS date range is our own attribute. (333)
- Conditional formatting paste with Transpose turns relative references by swapping their row and column offsets from
  the rule anchor; references with mixed absolute parts are only shifted. To compare with Excel and the reference
  suite (299, 304).
- XLSX validation lists on the very hidden helper sheet not yet opened in Excel or the reference suite (316).
- Validation alert modal and input message note box not yet seen in the running app (312).
- The shared input help note in Folio not yet seen live (316).
- Grouped icon gallery of the conditional rule editor and the invalid data circles not yet seen in the running app (309).
- Top/bottom percent count now rounds down with a minimum of one item; to confirm against real Excel (294).
- ODS conditional format extras (stop if true, reversed icons, deviations, bar borders and direction) live in the `urn:pulseoffice:spreadsheet` extension: other applications ignore them; the ODF020/040/102/086/087 notices remain (294).
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
  yet opened in Excel; hidden items of a grouped field still declared (XLSX030). (266)
- Round 264 (Ledger pivots), open: Excel "Months" without years reads as month-and-year items; the x14 show-as
  extension and the `numFmtId` of data fields not yet seen in Excel. (264)
- Round 267 (date epoch), open: display and pivot grouping outside evaluation follow the active workbook's epoch, so a
  second open workbook with another epoch printed or exported in the background shows its dates in the active one's
  system. Nothing was opened in Excel or the reference suite. (267)
- Round 270 (date epoch), open: 1904 date filters, dynamic filters and pivot date caches not yet opened in Excel. (270)
- Round 280 (Ledger formula entry), open: the AutoComplete list and tip popups were not seen live; their placement
  near the screen edge is unchecked. (280, 284)
- Round 284 (Ledger formula entry), open: localized German and French argument names not compared one by one with
  Excel's. (284)
- Round 267 (Ledger pivots), open: ODS hidden members and named base items carry item names, so a text item that reads
  as a number or boolean ("5", "TRUE") cannot be told from the number or boolean there (the reference suite conflates
  them too); hidden members named "TRUE" or "5" are read as hiding both. GETPIVOTDATA canonicalises items only for row
  and column fields and the reduced table of filter fields; percentOfParentCol, next and named base items not opened in
  Excel. (267)

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
