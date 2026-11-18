# Round 228: Ledger notes and links

Area: the Ledger items left open by rounds 221 and 224 (cell link clicks, note geometry, mentions, ODS threads,
annotated ranges, fill commands, ribbon, printed comments) and the ScreenTip of Folio and Deck links.

## Done

| # | Item | What changed |
|---|---|---|
| 1 | Cell links open with a click | A quick plain click on the text of a linked cell follows the link (`GridCellLinkTarget`, `ICellLinkHitArea`: the span of the drawn text, the whole cell when wrapped, rotated or overfull); a click beside the text, a drag beyond 4 px or a press held 500 ms selects. Ctrl/Cmd+click follows cell links whatever the setting. Setting `ledger.links.followOnClick` (Documents and saving > Spreadsheets, on by default, `ICellLinkClicking`). The context command stays. `Round228CellLinkClickTests`. |
| 2 | Notes moved and resized | Shown notes are hit-tested above objects; dragging inside moves, within 5 px of an edge or corner resizes (`ICommentBoxDrag`/`CommentBoxDrag` in the core, minimum 12 pt), previewed through `GridDisplayOptions.NotePreview` and committed as one undo step "Move or resize note" (refused on a protected sheet). The box already persisted in XLSX (VML anchor) and ODS (svg geometry). `Round228CommentBoxDragTests`. |
| 3 | Mentions | `CommentMention` (start, length, person) on comments and replies; `ICommentMentions` keeps only mentions whose range still names the person and detects `@name` of people already in the workbook when replying or editing from the review panel. XLSX writes `xltc:mentions` with person ids from the persons part (mentioned people are added to it) and reads them back; a comment with mentions is written as a thread. `Round228XlsxMentionTests`, `Round228CommentMentionTests`. |
| 4 | ODS threads | A thread is one `office:annotation`: the root text, then for each reply a header paragraph ("Author, yyyy-MM-dd HH:mm:") and its paragraphs, marked with our `urn:pulseoffice:spreadsheet` `reply`, `reply-header`, `creator` and `date` attributes; mentions are our `mention` elements. Other readers show the whole thread as the note's text. Reading accepts this form and the round-224 form (extra annotations with `parent-name`). `Round228OdsThreadTests`. |
| 5 | Annotated ranges | ODS `office:annotation` and `office:annotation-end` are placed and read in any paragraph of the cell, with offsets over the whole cell text (paragraphs joined by a newline). The cell editor highlights the range with the shared `TextRangeHighlight` (`PulseOffice.UI.Controls.Text`). `Round228OdsAnnotatedRangeTests`. |
| 6 | Fill commands copy comments | Fill down, right, up and left use `ISheetFill.Comments` like the fill handle, in the command's undo step, not on a protected sheet. `Round228FillCommandCommentTests`. |
| 7 | Ribbon | `ledger.comment.toggleShown` is on Review > Comments, with its existing icon. |
| 8 | Printed comments wrap | Comments at the end of the sheet wrap to the content width (continuation lines keep the indent), shown note boxes wrap to their width; widths are estimated at 0.55 em per character because pagination has no canvas. `Round228CommentPrintWrapTests`. |
| 9 | ScreenTips in Folio and Deck | `TextRun.HyperlinkTip` and `InteractiveAction.Tip`; the shared link dialog shows the tip for Folio text links and Deck object links. DOCX `w:hyperlink/@w:tooltip`, PPTX `a:hlinkClick/@tooltip` (runs and objects), ODT/ODP `office:title` on `text:a`; an ODP object click action has no title attribute in ODF, so the tip is our `urn:pulseoffice:interaction` `title` on `presentation:event-listener`. `Round228LinkTipTests`. |

Items 3 and 4 landed in one commit because the ODS thread form carries the mentions.

## Research notes

ODF 1.3 allows one annotation per table cell and has no reply relation, so the thread becomes paragraphs of that
annotation, readable everywhere, with our attributes keeping the structure. Excel stores mentions as character ranges of
the threaded comment text pointing at person ids of the workbook persons part. A presentation event listener in ODF
has no title, which is why the object tip uses our own attribute.

No tests or builds were run in this round (the coordinator builds and tests after the join).
