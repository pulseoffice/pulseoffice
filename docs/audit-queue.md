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
- Multi-level category labels of a compact sheet chart whose levels come only from the model (not from a category
  range of several columns) are written to XLSX with a one-column formula, so another application refreshing the
  formula keeps only the leaves. (195)
- Inline chart category levels in ODF live in our extension columns of the local table (`pulseoffice:category-level-*`);
  other readers show leaf labels only. (195)

## Folio
- Page-number parity after a section restart is taken from the displayed number, not the physical page, for even
  headers and mirrored margins alike; renumbered pages keep the side they were laid out with. (197)
- Columns of different widths are not modelled: DOCX declares them (DOCX245); ODT `style:column` relative widths are
  read as equal columns without a diagnostic. (197)
- Word line numbering restarted per section and its start value are read as continuous numbering from 1. (197)
- Word's gutter is folded into the left (or top) margin on reading; the model has no separate gutter, so it is not
  written back as `w:gutter`. (197)
- A table inserted on a page uses the document's first page style, not the page style where the caret is, and ignores
  page columns. (197)
- Hiragana page numbers have no Word page number format and are written as Arabic in DOCX without a diagnostic. (197)
- Mirrored margins are written to DOCX only when every used page style is mirrored (Word has one document setting). (197)
- RTF import maps every numbered list to the default "1, 2, 3" style, so the extended number formats now written to
  `\levelnfc` (ordinal, spelled-out, Hebrew, ...) are read back as Arabic; Hiragana has no RTF code and is written as
  Arabic. (196)
- The list dialog's preview spells cardinal and ordinal text in English whatever the document language. (196)
- Tracked moves are always drawn in Word's default green; there is no option for the move colour. (196)

## Shell and formats
- The Windows path of the private file spool has not run on a real Windows. (192)
