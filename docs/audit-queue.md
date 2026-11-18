# Audit queue

Open items reported by audit rounds, waiting for the next cycle (user rule, 2026-11-18). An item leaves the queue when
the round that fixes it is joined and the join run is green.

## Deck
- The pipe audio players on Linux (paplay, pw-cat, aplay, ffplay) report no device position: the played time is
  estimated from the bytes written less the pipe capacity and the requested player latency, not measured. (194)
- Media "start automatically" is checked only by round trips in our own reader and writer; PowerPoint and the
  reference suite have not opened such a file here. (194)
- The built-in media player has not run live on any system (Windows and macOS cannot be tested here). (193)
- Shape text layout drawing is partial: shapes draw flow and rotation but not columns or upright text; text boxes
  draw flow, rotation and columns but not stacked or upright text; editing a rotated or vertical text box keeps the
  caret and hit testing in the unrotated frame, and hit testing ignores columns. East Asian, Mongolian and
  left-to-right vertical modes are shown as plain vertical text (PPTX226, ODP217). (214)
- ODF has no native 270-degree flow, upright text or text box rotation: they use our `urn:pulseoffice:shape-text`
  extension; shapes still also write `style:text-rotation-angle`, which ODF defines only for cells, beside the
  standard `draw:text-rotate-angle`. The sign of `draw:text-rotate-angle` follows the ODF counter-clockwise convention
  and has not been checked against another application. (214)
- Connector glue output (`p:cxnSp`, `draw:connector`, the automatic-end extension) has not been opened in PowerPoint,
  Excel or the reference suite here. (213, 214)
- Ledger connector routing ignores the chosen sites now carried by XLSX and ODS: `SheetConnectors` routes between
  facing sides. (214)

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
- Linked styles: Word creates the "… Char" partner when a linked paragraph style is first applied to a partial
  selection; we only use a link that already exists, and the styles panel has no way to create or edit a link. (215)
- Pasting or loading styles from another document does not check that a linked partner travels with the style; a
  dangling link is ignored when applied but kept in the model. (215)
- RTF has no Hiragana list numbering (the `\levelnfc` codes 12, 13, 20 and 21 are all Katakana): Hiragana lists are
  written with Katakana numbers (RTF007). (199, 209)

## Shell and formats
- The Windows path of the private file spool has not run on a real Windows. (192)

## Ledger

- Round 216 (Ledger table references), open: the chart editor shows and edits table-reference series sources as text
  but offers no table/column picker; a table reference whose table is deleted leaves the chart series at `#REF!` with no
  diagnostic; XLSX restores structured conditional-format and validation formulas by matching range and flat formula,
  so a file edited in Excel after saving keeps the flat form (by design) and two identical rules on one range may
  exchange their structured forms; the relative-period list of the AutoFilter menu has not been seen in the running
  app nor walked with the keyboard.

## Accessibility
- Accessible names were verified by source scan only; no screen reader (VoiceOver, Narrator, Orca) has been run over
  the ribbon, dialogs or panels, nor over the new polite live region of the status bar. (201, 203)
- Key tips: on Windows and Linux the in-window menu bar is closed when the tips open, because Avalonia's access-key
  handler opens it on the same Alt release; check live that the two never flicker together. Third-level tips cover the
  ribbon's own popovers (menus, galleries, split-button lists, collapsed and overflow groups) but not combo box
  drop-downs, the font family list, the submenus of the title-bar menus, or a popover opened outside the ribbon; badges
  outside the ribbon sit in the adorner layer, which was not checked live inside separate popup windows. (208, 211)
- Settings and languages: switching the interface language while a Ledger cell is being edited leaves the edit text in
  the old formula separators, which the new convention then parses; not reproduced live. There is no high-contrast
  theme, only light, dark and system. A settings store does not reload values another running instance wrote until
  restart (writes no longer overwrite them). (217)
