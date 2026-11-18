# Audit queue

Open items reported by audit rounds, waiting for the next cycle (user rule, 2026-11-18). An item leaves the queue when
the round that fixes it is joined and the join run is green.

## Deck
- The shared table style gallery offers only header row and banded rows; first/last column, last row and banded
  columns exist only in Deck's own dialog; Folio and Ledger tables lack them. (173)
- Text pasted at the caret while typing is recorded under the typing step's name, not "Paste". (189)
- The table insert dialog offers only heading row and banded rows; Deck's own table style options dialog duplicates
  the four options now in the shared gallery. (190)
- Slide show: a link or action pointing to a hidden slide does nothing (hidden slides are left out of the playlist). (187)
- mp4 export is one still image per slide: transitions and animations take time but are not drawn. (187)
- Slide show keys: B/W black or white screen, number + Enter jump, black screen at the end of the show. (187)
- PDF export: option to include hidden slides. (187)
- Paste into Deck is recorded in the history under the "insert symbol" name. (188)
- In-app media player for loop, volume, trim and fades in the show (needs a PulseOffice.Platform media interface). (173, 187)

## Charts
- Pivot charts: multi-level category labels are not modelled (leaf labels only); the XLSX and ODS links are not yet
  checked in other applications. (182, 190)

## Ledger

## Folio
- Paragraph marks moved with a Word tracked move are still a separate deletion and insertion (DOCX224). (191)
- Spelled-out list numbers (cardinal and ordinal text) are English only; RTF and HTML export the new number formats as Arabic. (191)
- The hyperlink dialog's heading places still link by heading text (`#text|outline`). (191)
- The review panel and the change markup do not tell a move apart from a plain deletion and insertion. (191)
- DOCX tblLook noHBand is not read: a Word table that turns banding off over a banded style still shows bands. (186)
- List level text with different separators between placeholder pairs keeps only the first. (183)
- The list dialog cannot edit the per-level separator. (183)
- Word number formats not in the model (zero-padded, ordinal, spelled-out, non-Latin) are declared, not supported. (179)
- DOCX moved text is declared (DOCX224), not modelled. (162)
- Indexes and caption lists include text pending as a tracked deletion. (167)
- Caption cross-references look at the first sequence field of a paragraph. (167)
- Contents entries link to headings by text, so equal headings link to the first. (167)

## Exchange

## Macros and agent

## Shell and formats
