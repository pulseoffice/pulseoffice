# Audit queue

Open items reported by audit rounds, waiting for the next cycle (user rule, 2026-11-18). An item leaves the queue when
the round that fixes it is joined and the join run is green.

## Deck
- The pipe audio players on Linux (paplay, pw-cat, aplay, ffplay) report no device position: the played time is
  estimated from the bytes written less the pipe capacity and the requested player latency, not measured. (194)
- Media "start automatically" is checked only by round trips in our own reader and writer; PowerPoint and the
  reference suite have not opened such a file here. (194)
- The built-in media player has not run live on any system (Windows and macOS cannot be tested here). (193)
- Vertical and stacked text, text rotated inside a shape and text columns are not in the shape text model: they are
  declared on import (PPTX226, ODP217), not carried. (213)
- Connector glue: automatic ends are written to PPTX as the site they face and re-read as fixed sites on rectangular
  targets; connectors inside groups are written and read without glue; PowerPoint and the reference suite have not
  opened the new `p:cxnSp` custom-geometry and `draw:connector` output here. (213)
- Ledger connectors lose their chosen sites: XLSX always writes indices 2 and 0 and reads none, ODS writes no glue
  points. (213)
- ODP shrink-on-overflow also writes `style:shrink-to-fit` on graphic properties, which ODF defines only for cells. (213)

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
- A paragraph cannot switch off the list its paragraph style carries: DOCX `w:numId w:val="0"` and ODF
  `style:list-style-name=""` are read as "inherit", so such paragraphs come out numbered (medium). The model needs an
  explicit "no list" value through the resolver, layout and both codecs. (212)
- DOCX linked paragraph and character styles (`w:link`) are not modelled: the pair is read as two unrelated styles and
  written without the link. (212)
- A new page style made from an existing one copies its settings but not its header and footer text. (212)
- RTF has no Hiragana list numbering (the `\levelnfc` codes 12, 13, 20 and 21 are all Katakana): Hiragana lists are
  written with Katakana numbers (RTF007). (199, 209)

## Shell and formats
- The Windows path of the private file spool has not run on a real Windows. (192)

## Ledger

- Round 210 (Ledger queue), open: chart series sources holding table references are not kept structured nor renamed;
  XLSX writes table references in conditional formats and validations as they are, which Excel does not accept there
  (they need flattening plus an extension that keeps the structured form); colour-scale and data-bar threshold
  formulas are not kept structured in ODS; relative-period filters have no entry in the AutoFilter menu (they come only
  from files), and other ODF readers see the bounds of the day the file was saved; `CalculationEngine.cs` is still
  over 800 lines (1028).

## Accessibility
- Accessible names were verified by source scan only; no screen reader (VoiceOver, Narrator, Orca) has been run over
  the ribbon, dialogs or panels, nor over the new polite live region of the status bar. (201, 203)
- Key tips: on Windows and Linux the in-window menu bar is closed when the tips open, because Avalonia's access-key
  handler opens it on the same Alt release; check live that the two never flicker together. Third-level tips cover the
  ribbon's own popovers (menus, galleries, split-button lists, collapsed and overflow groups) but not combo box
  drop-downs, the font family list, the submenus of the title-bar menus, or a popover opened outside the ribbon; badges
  outside the ribbon sit in the adorner layer, which was not checked live inside separate popup windows. (208, 211)
- Chart marks drawn in series-derived colours (trend lines, error bars, the Pareto line, rising and falling waterfall
  bars) keep their fixed hues; their contrast on dark paper was not measured. (211)
- The keyboard walks of rounds 208 and 211 (shape style dialog included) were written without running them. (208, 211)
