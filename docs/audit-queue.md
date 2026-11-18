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
- Multi-level category labels of a compact sheet chart whose levels come only from the model (pivot charts, whose parent
  items sit on their own rows) are written to XLSX with a one-column `c:multiLvlStrRef` formula: no cell range holds
  the outer labels aligned with the leaves, so a wider formula would refresh to wrong labels. This matches the form
  written for compact pivot layouts; whether other applications keep the cached levels has not been checked. (195, 199)
- Inline chart category levels are now native ODF label columns before the leaf column; other applications have not
  opened such a chart here. (199)

## Folio
- ODT carries the line numbering start value and the per-section restart only in our extension attributes on
  `text:linenumbering-configuration`; other readers number continuously from 1. Line numbers restart where the page style
  changes, so continuous Word sections with the same layout do not restart. (198)
- The gutter is not in the page dialog (kept when the margins are edited); ODT stores it as our extension and folds it
  into the margin for other readers. (198)
- Columns of different widths have no editor: the page and section dialogs keep the widths only while the column count
  is unchanged. (198)
- A Word document mixing mirrored page styles with asymmetric non-mirrored ones still loses the mirroring (DOCX037);
  reading `w:mirrorMargins` marks every page style mirrored, symmetric ones included. (198)
- A change of the tracked-move colour or marks in the settings is drawn when a paragraph is laid out again; open
  documents are not relaid out at once. (199)
- RTF has no Hiragana list numbering: Hiragana lists are written with Katakana numbers (RTF007). (199)

## Shell and formats
- The Windows path of the private file spool has not run on a real Windows. (192)

## Ledger

- Round 200 (formula engine), not yet reviewed: XLOOKUP with an array lookup value (per-element results), spill ranges
  moving with row and column edits, A1# references across XLSX `cm`/metadata and ODS round trips, iteration settings
  with circular references, structured table references.

## Accessibility
- The border dialog's schema and the chart previews paint a fixed white canvas with black text in dark and
  high-contrast themes (they mimic the page, but are not themed). (201)
- Accessible names were verified by source scan only; no screen reader (VoiceOver, Narrator, Orca) has been run over
  the ribbon, dialogs or panels. (201)
- Keyboard-only walk of every dialog (Tab order, focus on open and return to the editor) not yet done for this
  round's area beyond the icon-only buttons. (201)
