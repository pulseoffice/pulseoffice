# Round 320: Folio columns and pages leftovers, Deck justified cells

Area: the Folio columns (315) and Folio pages (315) queue items, and Deck justified table cell alignment.
No builds or tests were run (audit cycle rule); the coordinator runs them at the join.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Medium | Word page-level columns followed by a continuous section break were never balanced: the first column filled to the page bottom while Word balances them. | `SectionColumns.BalancedBeforeSection`, set by the DOCX reader on multi-column page styles (span comparisons ignore it so no extra sections appear), written to ODT as `pt:balanced-before-section` on `style:columns`. The page region passes it as `Balanced`; `BalanceRequest.UntilSection` makes the balancer stop at the next section start, and balance only when a section (not a next-column one) follows before a page break or page style change. At the end of the document the page columns fill as before, as in Word. |
| Medium | The shared column editor edited only the separator on/off; style, width, colour, height and position, present in the model, ODT and layout, had no controls. | `ColumnLineFields` (UI.Controls, shared) adds style, width (pt), colour, height (%) and position rows, shown when the separator is on; `ColumnLayout.Line` carries them; `FolioColumnLayouts` maps them both ways and keeps the stored line untouched when unedited. Texts in the five languages. |
| Medium | ODT wrote a next-column section start only as our `pt:starts-in-next-column`, so other applications started the section with no column break. | Research: ODF 1.3 has no section-level break; `fo:break-before="column"` on a paragraph is the native column break, honoured by other ODF readers. The writer now gives the section's first paragraph that break (when it has no page or column break of its own) and marks it `pt:next-column-break="written"`; the reader strips the written break so the model round-trips unchanged. |
| Minor | Deck justified table cell alignment read as left: `CellHorizontalAlign` had no `Justify`. | `CellHorizontalAlign.Justify` mapped in ODP (`fo:text-align="justify"`), PPTX (`algn="just"`, cells and table styles), rich cell rendering, cell shape text editing, the exchange grid and the Deck justify command on cells. |
| Minor | The blank page before a right or left page style: checked against ECMA-376 (`ST_SectionMark` oddPage/evenPage only says the section starts on the next odd or even page) and Word's documented behaviour (the blank page closes the previous section, with its headers). Ours keeps the previous style: no change, pinned by a test. | Test only. |

## Open (queued)

- Page columns from ODT or made in Folio are not marked, so their DOCX balances in Word before a continuous section.
- A next-column section starting with a table or nested section still relies on our attribute only.
- The plain-text path of the data table renderer draws justify as left.
- Nothing seen live in Word or the reference suite.

## Join risks

- `CellHorizontalAlign` gained a member: switches without a default arm would warn; all found have one.
- `SectionColumns` equality now includes `BalancedBeforeSection`; DOCX reader comparisons strip it.
