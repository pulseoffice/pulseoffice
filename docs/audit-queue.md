# Audit queue

Open items reported by audit rounds, waiting for the next cycle (user rule, 2026-11-18). An item leaves the queue when
the round that fixes it is joined and the join run is green.

## Deck
- The pipe audio players on Linux (paplay, pw-cat, aplay, ffplay) report no device position: the played time is
  estimated from the bytes written less the pipe capacity and the requested player latency, not measured. (194)
- Media "start automatically" is checked only by round trips in our own reader and writer; PowerPoint and the
  reference suite have not opened such a file here. (194)
- The built-in media player has not run live on any system (Windows and macOS cannot be tested here). (193)
- East Asian vertical text keeps its East Asian characters turned with the line instead of upright (PPTX226, ODP217,
  declared only when such characters are present); Mongolian lines are mirrored in single-column shape text and text
  boxes but not inside shape text columns; right-to-left stacked text is mirrored in text boxes only. Not seen live. (225)
- Stacked shape text is edited in the plain overlay text box, whose caret does not follow the stacked glyphs; up and
  down keys in stacked text boxes move one character; up and down keys in Mongolian text boxes follow the stored line
  order, not the mirrored one. Shape text column breaks are probed per character on the composed text, which costs a
  geometry per character for long texts. (218, 225)
- ODF has no native 270-degree flow, upright text or text box rotation: they use our `urn:pulseoffice:shape-text`
  extension; shapes still also write `style:text-rotation-angle`, which ODF defines only for cells, beside the
  standard `draw:text-rotate-angle`. The sign of `draw:text-rotate-angle` follows the ODF counter-clockwise convention
  and has not been checked against another application. (214)
- Connector glue output (`p:cxnSp`, `draw:connector`, the automatic-end extension) has not been opened in PowerPoint,
  Excel or the reference suite here. (213, 214)
- Deck has no command to choose a connector's sites after creation (Ledger's Connector Ends dialog is shared in
  `PulseOffice.UI.Controls.Shapes` but wired only into Ledger); the dialog has not been seen live. (225)

## Charts
- Pivot chart links (XLSX `c:pivotSource` with the `[]` name form and multi-area `multiLvlStrRef` formulas; ODS
  `loext:data-pilot-source`) have not been opened in other applications; ODS pivot charts carry only leaf category
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
- Folio notes: "section end" placement collects at the end of each top-level text section (ODT `text:section`), not of
  each Word section (page style run); endnote collection is one setting for the whole document, while ODT can set it per
  section; the separator line is one document setting, while ODT stores it per page layout; continuation notices are not
  selectable text and use the footnote paragraph style. (223)
- RTF has no Hiragana list numbering (the `\levelnfc` codes 12, 13, 20 and 21 are all Katakana): Hiragana lists are
  written with Katakana numbers (RTF007). (199, 209)

- `FolioCanvas.cs` (about 1,150 lines) and `Paginator.cs` (about 1,300 lines) are over the 800-line cap: split them along coherent responsibilities. (223, 225 join)

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
- Round 221 (Ledger comments and cell links), open: cell hyperlinks open from the context command or Ctrl+click
  only, not with a plain click. (221)
- Round 224 (Ledger comments), open: note boxes cannot be moved or resized with the mouse (they come from files and are
  drawn and printed where the model says); mentions are plain names in the text (XLSX `mentions` elements are neither
  written nor read, so the person link is lost); ODS replies are extra `office:annotation` elements of the same cell,
  which other readers either ignore or show as separate notes; the annotated text range of `office:annotation-end`
  is kept only within the cell's first paragraph and is not shown in the grid; Fill down/right/up/left commands do not
  copy comments (only the fill handle does); the single-note show command has no ribbon placement; comments printed at
  the end are not wrapped to the page width; only Ledger's use of the shared link dialog shows the ScreenTip field
  (Folio and Deck links have no tip in the model). Not opened in Excel or the reference suite. (224)

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
