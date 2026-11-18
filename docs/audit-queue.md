# Audit queue

Open items reported by audit rounds, waiting for the next cycle (user rule, 2026-11-18). An item leaves the queue when
the round that fixes it is joined and the join run is green.

## Deck
- The pipe audio players on Linux (paplay, pw-cat, aplay, ffplay) report no device position: the played time is
  estimated from the bytes written less the pipe capacity and the requested player latency, not measured. (194)
- Media "start automatically" is checked only by round trips in our own reader and writer; PowerPoint and the
  reference suite have not opened such a file here. (194)
- The built-in media player has not run live on any system (Windows and macOS cannot be tested here). (193)
- Upright East Asian glyphs are centred in the advance of their cluster in the line, which is narrower than the line
  height, so neighbouring upright glyphs can overlap slightly; vertical punctuation forms are not substituted. Mongolian
  lines are not mirrored inside shape or text box columns, and list labels of mirrored Mongolian paragraphs stay at the
  paragraph top. None of the East Asian, stacked or Mongolian modes has been seen live. (225, 230)
- Shape text in vertical, 270-degree and upright flows (other than stacked) is still edited in the turned overlay text
  box; arrow keys in turned text boxes now follow the drawn directions but Folio has no shape text flows. Shape text
  column breaks are probed per character on the composed text, which costs a geometry per character for long texts.
  (218, 230)
- ODF has no form for upright text or text box rotation: they keep our `urn:pulseoffice:shape-text` extension; the
  270-degree flow is now written as the extension writing mode `bt-lr` beside `tb-rl`, not yet opened in another
  application. Shapes still also write `style:text-rotation-angle`, which ODF defines only for cells, beside the
  standard `draw:text-rotate-angle`; its sign has not been checked against another application. (214, 227)
- Connector glue output (`p:cxnSp`, `draw:connector`, the automatic-end extension) has not been opened in PowerPoint,
  Excel or the reference suite here. (213, 214)
- Deck's Connector Ends dialog (shared `ConnectorSitesDialog`) has not been seen live; it is now also in the slide
  object context menu. (225, 227, 231)
- Outline edits keep paragraph formatting only through the text splice, and titles with several paragraphs are shown
  joined on one line. Slides are matched to outline lines by title, so two slides with the same title whose blocks
  are swapped by hand may keep their old order.
  (229, 231)
- Empty sections are kept in the model, PPTX and ODP, but the filmstrip and sorter do not show them, so they cannot
  be renamed or removed in the app; moving a slide that an empty section precedes moves the empty section with it.
  The leading unsectioned slides are written to PPTX in a "Default Section" with a fixed identifier that reads back
  as no section. ODP empty sections are `urn:pulseoffice:presentation-section` elements inside `draw:page`. None of
  this has been opened in PowerPoint or the reference suite here. (229, 231)
- "Number slides from" 0 is written to ODP only as our extension attribute (`style:first-page-number` takes positive
  integers); slide number fields do not write `text:page-adjust`. Not opened in another application. (231)
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
- DOCX still loses line number restarts that no body section can carry: on the first paragraph (other than the start
  value), inside tables, frames, notes and comments, on a paragraph that defers a page style change, and any restart
  when numbering restarts on every page (DOCX248). Not yet opened in Word. (204, 209, reviewed again in 212: the remaining cases have no Word
  section form)
- Folio notes: continuation notices are not selectable text and use the footnote paragraph style (223). A page style run
  starts only at a top-level paragraph with a page style (not at a table); Word has one footnote separator per document,
  so a page style's own line is declared (DOCX273); none of this has been opened in Word or the reference suite. (232)
- RTF has no Hiragana list numbering (the `\levelnfc` codes 12, 13, 20 and 21 are all Katakana): Hiragana lists are
  written with Katakana numbers (RTF007). (199, 209)
- Folio object positions: the vertical "margin" relation means the paragraph area in ODT (`paragraph-content`), the
  top margin in DOCX (`topMargin`) and the text area in the layout; the three need one meaning. DOCX reads the column
  relation back as paragraph. The position dialog does not offer the new aligned positions (left, centre, right, top,
  middle, bottom), which come only from files; ODF `inside`/`outside` and Word mirrored-page alignment are read as
  offsets. Aligned positions relative to the paragraph or line can only be aligned at the top. (233)
- Deck reads and writes the ODP `fo:clip` of cropped images against the frame size, as Folio did before round 233; it
  should share the original-image measure of `OdtImageClip`. The same image inserted twice is stored twice in ODT and
  DOCX. (233)


## Shell and formats
- The Windows path of the private file spool has not run on a real Windows. (192)

## Ledger

- The relative-period list of the AutoFilter menu has not been seen in the running app nor walked with the keyboard.
  (216)
- The chart editor's table column picker and the missing-table placeholder and status warning have not been seen in
  the running app; the warning is checked after each history step, so a table lost outside the history (file reload)
  is not announced, only drawn as the placeholder. (220)
- XLSX table-reference rule forms are matched by priority and ordinal within range and priority, then by range and
  flat formula; a rule Excel renumbered and edited keeps the flat form (by design). Not opened in Excel. (220)
- Round 232 (Ledger links and notes), open: dragging a note is refused as a whole on a protected sheet; the mention
  picker is in the review panel only, not in the cell note editor; the grid highlight of an annotated range is drawn only
  when the shown text equals the cell value (not for formatted numbers); the Ledger grid keeps its own tooltip code
  beside the shared `ILinkTips`; Folio hover tips use the nearest text position, Deck shows tips on hover only during
  the show (object click actions), not in the editor nor for text run links; group member links have no tip. Not
  opened in Excel, Word, PowerPoint or the reference suite. (232)

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
