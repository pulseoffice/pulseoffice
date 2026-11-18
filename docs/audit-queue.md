# Audit queue

Open items reported by audit rounds, waiting for the next cycle (user rule, 2026-11-18). An item leaves the queue when
the round that fixes it is joined and the join run is green.

## Deck
- Slide show: a link or action pointing to a hidden slide does nothing (hidden slides are left out of the playlist). (187)
- mp4 export is one still image per slide: transitions and animations take time but are not drawn. (187)
- Slide show keys: B/W black or white screen, number + Enter jump, black screen at the end of the show. (187)
- PDF export: option to include hidden slides. (187)
- Paste into Deck is recorded in the history under the "insert symbol" name. (188)
- The shared table style gallery offers only header row and banded rows; first/last column, last row and banded
  columns exist only in Deck's own dialog; Folio and Ledger tables lack them. (173)
- In-app media player for loop, volume, trim and fades in the show (needs a PulseOffice.Platform media interface). (173, 187)

## Charts
- Statistical, financial and radar plots ignore the reversed category axis. (184)
- No test for the chart loss declarations in DOCX, PPTX, ODT and ODP. (172)
- Folio and Deck charts keep cached custom error values (data inside the chart). (178)
- Pivot charts: with several row fields, parent and subtotal rows fall inside the series; with several column
  fields only the last header level names a series; charts re-derived only on pivot apply, not on open; XLSX
  c:name without the [file] prefix; links not yet checked in other applications. (182)
- The unused "chart.scale" text key. (181)

## Ledger
- Pivot dialog preview uses fixed light colours instead of theme tokens. (177)

## Folio
- List level text with different separators between placeholder pairs keeps only the first. (183)
- The list dialog cannot edit the per-level separator. (183)
- Word number formats not in the model (zero-padded, ordinal, spelled-out, non-Latin) are declared, not supported. (179)
- DOCX moved text is declared (DOCX224), not modelled. (162)
- Indexes and caption lists include text pending as a tracked deletion. (167)
- Caption cross-references look at the first sequence field of a paragraph. (167)
- Contents entries link to headings by text, so equal headings link to the first. (167)

- DOCX tblLook noHBand is not read: a Word table that turns banding off over a banded style still shows bands. (186)

## Exchange
- Charts with error bars and pivot links, shape groups and drag and drop exercised only by reading code. (188)

## Macros and agent
- Ledger sheet.Find ignores case while sheet.Replace matches case; API help does not say so. (185)
- Ledger sheet.Freeze is not refused on a read-only workbook. (185)
- A script hitting its time cap inside a host call might finish that call after the agent's undo step closed. (185)

## Shell and formats
- In-memory spreadsheet spool holds the whole body while saving very large workbooks. (180)
