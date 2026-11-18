# Audit queue

Open items reported by audit rounds, waiting for the next cycle (user rule, 2026-11-18). An item leaves the queue when
the round that fixes it is joined and the join run is green.

## Deck
- The pipe audio players on Linux (paplay, pw-cat, aplay, ffplay) report no device position: the played time is
  estimated from the bytes written less the pipe capacity and the requested player latency, not measured. (194)
- Media "start automatically" is checked only by round trips in our own reader and writer; PowerPoint and the
  reference suite have not opened such a file here. (194)
- The built-in media player has not run live on any system (Windows and macOS cannot be tested here). (193)
- East Asian, Mongolian and left-to-right vertical modes are shown as plain vertical text (PPTX226, ODP217). (214)
- Shape text columns are cut at a line height measured on the base font, so runs of other sizes can be split at a
  column edge; stacked shape text is edited in the plain overlay text box, whose caret does not follow the stacked
  glyphs; up and down keys in stacked text boxes move one character. (218)
- ODF has no native 270-degree flow, upright text or text box rotation: they use our `urn:pulseoffice:shape-text`
  extension; shapes still also write `style:text-rotation-angle`, which ODF defines only for cells, beside the
  standard `draw:text-rotate-angle`. The sign of `draw:text-rotate-angle` follows the ODF counter-clockwise convention
  and has not been checked against another application. (214)
- Connector glue output (`p:cxnSp`, `draw:connector`, the automatic-end extension) has not been opened in PowerPoint,
  Excel or the reference suite here. (213, 214)
- Ledger has no command to choose a connector's sites: `SheetConnectors.Connect` takes them, but the insert command
  always creates automatic ends. (218)

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
- Folio notes: footnotes placed below the text or collected at the document or section end, and endnotes at each
  section end, have no model and are read at the page bottom and document end (declared ODT290, DOCX272). (219)
- Folio notes: continuation notices are kept in both formats but not drawn when a footnote continues on the next page;
  the footnote separator line (ODT `style:footnote-sep`, DOCX separator note content) is fixed and not read. (219)
- Folio notes: removing a note reference with Backspace while changes are recorded removes the note directly instead
  of recording a tracked deletion. (219)
- RTF has no Hiragana list numbering (the `\levelnfc` codes 12, 13, 20 and 21 are all Katakana): Hiragana lists are
  written with Katakana numbers (RTF007). (199, 209)

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
- Round 221 (Ledger comments and cell links), open: comments have no threads, replies, resolved state or mentions
  (XLSX threaded comments are flattened with XLSX235, and only legacy notes are written); notes have no size or
  position of their own (XLSX VML box is fixed, ODS `svg:x/y/width/height` of the annotation are ignored); there is no
  show/hide command for a single note, only for all of them; find and replace does not search comment text; printing
  of comments (at end of sheet or as displayed) is kept in the page setup and both formats but is neither offered in
  the page setup dialog nor rendered by the print document; the link dialog cannot edit a cell hyperlink's screen tip
  (kept in both formats, shown on hover); cell hyperlinks open from the context command only, not with a click;
  the fill handle does not copy comments; ODS `office:annotation-end` ranges are not read.

## Accessibility
- Accessible names were verified by source scan only; no screen reader (VoiceOver, Narrator, Orca) has been run over
  the ribbon, dialogs or panels, nor over the new polite live region of the status bar. (201, 203)
- Key tips: on Windows and Linux the in-window menu bar is closed when the tips open, because Avalonia's access-key
  handler opens it on the same Alt release; check live that the two never flicker together. Third-level tips cover the
  ribbon's own popovers (menus, galleries, split-button lists, collapsed and overflow groups) but not combo box
  drop-downs, the font family list, the submenus of the title-bar menus, or a popover opened outside the ribbon; badges
  outside the ribbon sit in the adorner layer, which was not checked live inside separate popup windows. (208, 211)
- A settings store does not reload values another running instance wrote until restart (writes no longer overwrite
  them). (217)
- The high-contrast theme has not been seen live; drawn surfaces that hard-code colours instead of Palette tokens, and
  module accent fills behind white text, were not audited for it. The theme does not follow the operating system's
  own high-contrast setting. The cell edit is translated to the new formula separators on a language switch only for
  formulas; plain entries keep the text as typed. Not run live. (220)
