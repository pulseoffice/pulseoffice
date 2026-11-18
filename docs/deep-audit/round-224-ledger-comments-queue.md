# Round 224: Ledger comments queue (from round 221)

Area: the open Ledger comment items of round 221: threads, note geometry, single-note visibility, searching comments,
printing comments, the hyperlink ScreenTip in the link dialog, the fill handle and ODS annotation ranges.

## Done

| # | Item | What changed |
|---|---|---|
| 1 | Threaded comments | `CellComment` has `Replies` (`CommentReply`: text, author, date) and `IsResolved`; mentions stay plain names in the text. XLSX writes a thread (a comment with replies or resolved) as `threadedComments` plus the workbook `persons` part, and keeps a legacy note beside it with the `tc={id}` author and the usual fallback text, so older readers still see a note; plain notes stay legacy notes only, because Excel shows threaded comments as a different kind of object. Reading rebuilds replies, resolved state and dates; XLSX235 is removed from the code and the five language files. XLSX243 (lost dates) now counts only plain notes. ODS writes each reply as a following `office:annotation` of the same cell carrying our `urn:pulseoffice:spreadsheet` `parent-name` (the root has `office:name`), and the resolved state as our `resolved` attribute. The review panel lists replies under their comment, replies, edits, deletes and resolves (one undo step each); the hover tip shows the whole thread. `Round224XlsxCommentTests`, `Round224OdsCommentTests`, `Round224LedgerReviewTests`, updated `CommentsRound113Tests`. |
| 2 | Note size and position | `CommentBox` (offset from the cell's top-right corner and size, in points) through `ICommentBoxGeometry`/`CommentBoxGeometry` (core). XLSX reads the VML `x:Anchor` and writes anchor and style from the box; ODS reads and writes `svg:x/y/width/height` of the annotation. Shown notes are drawn in the grid by `GridNotePainter`. `Round224CommentBoxGeometryTests`. |
| 3 | Show or hide one note | `ledger.comment.toggleShown` (Sheet › Comments and the cell context menu, checked when shown, one undo step, refused on a protected sheet). `Round224LedgerNoteCommandTests`. |
| 4 | Find in comments | "In comments instead of cells" option of the find and replace dialog (`SearchOptions.InComments`, `CommentSearch`): comment and reply text are found and replaced in one undo step; replacing refuses on a protected sheet. `Round224CommentSearchTests`. |
| 5 | Printing comments | The page setup dialog's Sheet tab chooses none, at end of sheet or as displayed; `SheetCommentPrinter` adds pages listing the comments (cell, author, text, replies) or draws the shown notes in their boxes. `Round224CommentPrintTests`. |
| 6 | Hyperlink ScreenTip | `LinkRequest.ShowsTip/Tip` and `LinkChoice.Tip` in the shared link dialog; Ledger's insert hyperlink shows and edits `Cell.HyperlinkTip`. `Round224LedgerLinkTipTests`. |
| 7 | Fill handle | The fill handle copies the seed cells' comments in a cycle and clears the comments of filled cells whose seed has none, in the fill's undo step; comments are left alone on a protected sheet. Chosen because Excel's fill handle copies notes the same way, and it matches the round-221 rule for paste (the pasted area's uncopied comments are cleared). `ISheetFill.Comments`, `Round224FillCommentTests`. |
| 8 | ODS annotation ranges | `CellComment.AnnotatedText` (`CommentTextSpan`): a named annotation with a matching `office:annotation-end` in the cell's first paragraph is read as a character range; writing places the annotation at the start offset (or before the paragraph when it starts at 0) and the end marker at the end offset. |

Items 1, 2 (format part) and 8 landed in one commit because they share the model and both codecs.

## Research notes

Excel keeps a legacy note for every threaded comment (author `tc={thread id}`, a fixed explanatory text followed by
the comment and replies) so that older versions can still read it; we do the same. Person ids are derived from the
author name so repeated saves are stable. ODF 1.3 allows a single annotation per table cell and has no reply
relation, so replies use our own attribute and the extra annotations degrade to separate notes elsewhere.

No tests or builds were run in this round (the coordinator runs them after the join).
