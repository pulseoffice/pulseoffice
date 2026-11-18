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
- DOCX writes a Folio line number restart only as a continuous Word section at the top level of the body with the
  document's start value; restarts inside sections or tables, or with another value, are declared lost (DOCX248). (204)
- The section dialog seeds and scales uneven column widths against a 17 cm text width: it does not know the text width
  of the page the section sits on. (204)
- `FolioEditor` is over 800 lines (927) and should be split. (204)
- RTF has no Hiragana list numbering: Hiragana lists are written with Katakana numbers (RTF007). (199)

## Shell and formats
- The Windows path of the private file spool has not run on a real Windows. (192)

## Ledger

- Round 205 (data tools), open: remove duplicates has no dialog (always no header, all columns; `KeyColumns` is never
  set) and reports a protected sheet with the format-protection text; text to columns overwrites cells to the right
  without asking; a left-to-right sort remembered on an AutoFilter is dropped (`AutoFilterState.Remember` checks the
  column count only, the ODS reader keeps only downward sorts); XLSX `customList` items containing a comma split on
  read; XLSX dynamic month/quarter filters (no stored bounds) are still dropped on read; the sort gate checks merges
  as if the first row were a header (the engine then refuses silently when it is not).
- Structured table references (`Table1[Column]`, `[@Column]`, `[#Totals]`) are resolved to absolute A1 ranges when
  an XLSX is read and are not kept in the model: the formulas round-trip as A1 references, and renaming a table or a
  column does not touch them. ODS has no structured references. (202)
- XLOOKUP with an array lookup value and a multi-column return range keeps only the first column per element; check
  what the other applications give (nested array). (202)
- A1# on an anchor holding a single-value formula gives #REF!; check whether the other applications return the value.
  (202)
- The circular-reference status notice refreshes on selection and view changes, not on every recalculation (the
  engine raises no event); entering a formula with Enter moves the selection, so the usual path is covered. (202)

## Accessibility
- Accessible names were verified by source scan only; no screen reader (VoiceOver, Narrator, Orca) has been run over
  the ribbon, dialogs or panels, nor over the new polite live region of the status bar. (201, 203)
- The ribbon has no key tips (Alt + letter badges on tabs and commands); it is reached with F6 and walked with the arrow
  keys and Tab. (203)
- The show's black, white and end screens and the slide number typed on the keypad are not announced to assistive
  technology. (203)
- Chart text is still drawn in fixed black by the chart renderer; on the dimmed dark-theme page token it reads, but a
  high-contrast theme would need the renderer to take the page ink. (203)
- Keyboard walk of the chart editor tabs, table look options, PDF export, page setup, find and replace, styles and
  media options was done by reading the source against the shared DialogHost behaviour, not live with a virtual
  display. (203)
