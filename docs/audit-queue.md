# Audit queue

Open items reported by audit rounds, waiting for the next cycle (user rule, 2026-11-18). An item leaves the queue when
the round that fixes it is joined and the join run is green.

## Deck
- The pipe audio players on Linux (paplay, pw-cat, aplay, ffplay) report no device position: the played time is
  estimated from the bytes written less the pipe capacity and the requested player latency, not measured. (194)
- Media "start automatically" is checked only by round trips in our own reader and writer; PowerPoint and the
  reference suite have not opened such a file here. (194)
- The built-in media player has not run live on any system (Windows and macOS cannot be tested here). (193)

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
  when numbering restarts on every page (DOCX248). Not yet opened in Word. (204, 209)
- RTF has no Hiragana list numbering (the `\levelnfc` codes 12, 13, 20 and 21 are all Katakana): Hiragana lists are
  written with Katakana numbers (RTF007). (199, 209)

## Shell and formats
- The Windows path of the private file spool has not run on a real Windows. (192)

## Ledger

- Round 206 (data tools 2), open: a scenario captured over whole columns keeps only the cells inside the used area,
  so cells beyond it that are filled later are not cleared when the scenario is shown; applying a scenario on a
  protected sheet relies on the command effect guard instead of a check at the source; dynamic XLSX filters for
  relative periods written without `val`/`maxVal` by other producers are still dropped; the remove duplicates dialog
  has no case-sensitivity option (`DuplicateRemovalRequest.CaseSensitive` is never set).
- Structured table references (207): no command renames a table (`TableRenaming` exists in the engine only);
  `[#This Row]` outside the data rows gives #REF! where the other applications give #VALUE!; table references in
  conditional formats, validations, defined names and chart sources are not kept structured (ODS) nor rewritten on
  renames; the formula bar accepts but does not show the `[@Column]` short form (it shows `[[#This Row],[Column]]`);
  header renames are not followed in manual calculation mode until the next edit of that header row.

## Accessibility
- Accessible names were verified by source scan only; no screen reader (VoiceOver, Narrator, Orca) has been run over
  the ribbon, dialogs or panels, nor over the new polite live region of the status bar. (201, 203)
- Ribbon key tips cover tabs and the commands shown on the chosen tab; they do not reach a third level (the items of an
  open gallery, menu or collapsed-group popover), the title-bar menus, the document tabs or the status bar. On Windows
  and Linux the in-window menu bar is closed when the tips open, because Avalonia's access-key handler opens it on the
  same Alt release; check live that the two never flicker together. (208)
- Chart plot marks that are not text (axis lines, grid, meter needle, box-plot whiskers) keep fixed grey and black; the
  Ledger grid draws charts on a fixed white paper with black ink. (208)
- The keyboard walk of the shape style dialog was not written (it needs the full font picker services); the other
  dialog walks of round 208 were written without running them. (208)
