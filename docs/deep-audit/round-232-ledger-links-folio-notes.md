# Round 232: Ledger links and notes, Folio notes

Area: the round-228 Ledger item and the round-223 Folio notes item of the audit queue. No tests or builds were run in
this round (the coordinator builds and tests after the join).

## Done: Ledger links and notes (round 228 item)

| # | Item | What changed |
|---|---|---|
| 1 | Link click target | `GridCellLinkTarget` applies the conditional formats of the cell (a hidden value has no link, an icon set reserves its width) and, when the pointer is over an empty cell, looks up to 32 columns left and right for a linked text cell whose overflowing text covers the point (`ICellLinkHitArea.Spilled`, clamped to the `ICellOverflow` span). `Round232CellLinkSpillTests`. |
| 2 | Cursors | Hand cursor over clickable link text; over a shown note the move cursor inside and the side or corner cursor within 5 px of an edge. |
| 3 | Note undo state | `NoteBoxChange.Before` is the stored box, null for a note that had none, so undo gives the note back its default placement instead of a fixed box. |
| 4 | Mention picker | `IMentionQuery`/`MentionQuery` (token before the caret, candidates, insertion) and `MentionPicker` in `PulseOffice.UI.Controls.Review` over the shared `ChoiceListPopup`; the review panel's reply box lists people when `@` is typed. `IReviewSource.People` (default empty); Ledger returns comment and reply authors, mentioned people and the current user. `Round232MentionQueryTests`. |
| 5 | Annotated range | Highlighted in the grid by `CellValuePainter` (theme warning colour at 30 % opacity). `IAnnotatedSpanFollowing` moves, grows or drops the range when the cell text is edited, in the same undo step as the edit (`LedgerCellWriter.RecordWrite`). `Round232AnnotatedSpanTests`. |
| 6 | Printed comment wrapping | `IPrintTextMeasure` in `PulseOffice.Printing` (canvas adapter, average-advance fallback) and `StandardFontTextMeasure` in `PulseOffice.Formats.Pdf` (standard font metrics, no canvas); `SheetCommentPrinter` takes the measure, Desktop registers the metrics one and the shown note boxes measure with the page canvas. `Round232CommentPrintMeasureTests`. |
| 7 | Object link tips | `AnchoredObject.HyperlinkTip` and `PlacedObject.HyperlinkTip`: DOCX and XLSX `a:hlinkClick/@tooltip`, ODT and ODS `office:title` on the `draw:a` around the object. The link dialogs of Folio objects and Ledger objects show the tip field. `Round232ObjectLinkTipTests`. |
| 8 | Tips on hover | Shared `ILinkTips` (`PulseOffice.UI.Controls.Links`): Folio shows the tip of text links and linked objects under the pointer (`IFolioLinkTips`), Ledger objects show theirs in the grid tooltip, the Deck show shows the tip of hyperlink click actions. |

## Done: Folio notes (round 223 item)

| # | Item | What changed |
|---|---|---|
| 1 | Section end per Word section | Footnotes and endnotes placed at the section end are collected at the end of each page style run (a top-level paragraph that sets a page style starts a run, as a DOCX `w:sectPr` does): `NoteNumbers.RunBreaks` maps the paragraph starting a run to the run that ends before it, and `FlowBuilder.Build` emits the run's notes there. The last run ends with the document. Text sections keep their own collection. `Round232SectionNotesLayoutTests`. |
| 2 | Endnotes per section | `SectionBlock.CollectsEndnotes` (null follows the document); ODT writes `text:notes-configuration note-class="endnote"` in each section that collects and reads it per section, `ISectionNoteCollection.Settle` turns "every section collects" back into the document setting; DOCX carries it on our section extension (`endnotes="collect|keep"`), since Word only has the document-wide `w:endnotePr/w:pos`. The section dialog has a "Collect endnotes at the end of this section" check box (five languages). `Round232OdtSectionNotesTests`, `Round232DocxSectionNotesTests`. |
| 3 | Separator per page style | `PageStyle.FootnoteSeparator` overrides the document line; the paginator asks for the page's style (`PaginationRequest.PageSeparator`). ODT writes each page layout's own `style:footnote-sep` and reads back the ones that differ from the document line. Word has a single separator note: DOCX writes the document line and declares DOCX273. |

## Research notes

ECMA-376 allows `w:pos` only in the document-wide `w:footnotePr`/`w:endnotePr` of the settings part; a section's
properties carry numbering only, so per-section endnote collection has no Word form and rides on our extension. ODF 1.3
puts `text:notes-configuration` in `style:section-properties` to collect notes at a section's end and a
`style:footnote-sep` in every `style:page-layout-properties`. `office:title` is an allowed attribute of `draw:a`.

## Open

See the round 232 lines of `docs/audit-queue.md`.
