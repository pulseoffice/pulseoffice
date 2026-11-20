# Round 325: Folio and Deck leftovers, Folio find and replace

## Leftovers fixed

| Severity | Defect | Fix |
|---|---|---|
| Medium | Page columns made in Folio or read from ODT carried no "balance before a section" mark and nothing could set it, while the DOCX written from them balances in Word before a continuous section. | The page dialog's Columns tab has a switch "Balance the columns before a section, as Word does" (`balanceBeforeSection`, five languages); one column clears the mark. DOCX038 is now also raised when multi-column page columns are not marked and a section follows, since Word always balances there and has no per-section switch (researched: `w:noColumnBalance` is a document-wide compatibility setting that would also stop marked and section columns from balancing). |
| Medium | The ODT next-column start of a section whose first block is a table or a nested section relied on our `pt:starts-in-next-column` only. | `OdtNextColumnBreak` writes the native `fo:break-before="column"`: on the table's `style:table-properties` (a new automatic style derived from the table's), or on the first paragraph of the innermost leading nested section; `pt:next-column-break="written"` marks it, and the reader strips the paragraph break through nested sections. A table whose style carries a page style, or a leading paragraph that already breaks, is left alone. |
| Minor | The Deck data table renderer's plain-text path (cells without rich text) drew justified text as left, in one ellipsised line. | Justified plain cells go through the rich cell layout (paragraphs split at line feeds), which wraps and justifies. |

## Find and replace audit

| Severity | Defect | Fix |
|---|---|---|
| Medium | Find and Replace All searched only the story holding the caret: from the body, text in headers, footers, notes and text boxes was never found or replaced (tables were covered, being part of the story). | `IParagraphOrder.SearchOrder`: the caret's story first, then every other story (body, notes, text boxes, headers and footers), comments excluded. Find next wraps across them; Replace All replaces in all of them in one undo step. |
| Medium | No regular expressions. | "Regular expression" option (shared `find.regex` text); .NET regular expressions with a one-second match timeout; `$1`-style groups in the replacement; whole words also filter regex matches; an invalid pattern finds nothing and the summary says so (`folio.find.invalidPattern`). |
| Medium | No special characters. | Word-style caret codes in the query: `^t` tab, `^l` line break, `^s` no-break space, `^~` no-break hyphen, `^-` soft hyphen, `^^` caret, and the wildcards `^?` any character, `^#` digit, `^$` letter, `^w` white space; in the replacement the same characters plus `^&` for the found text. Literal queries keep culture-aware matching (ignorable characters such as soft hyphens still match inside words). |
| Medium | No single Replace: only Replace All. | `IFindReplaceTarget.SupportsReplace`/`Replace` (default members, Ledger and Deck unchanged); the shared dialog shows Replace when supported. Folio replaces the selection when it is a match, as one undo step, then selects the next match. |
| Minor | The dialog showed no match count. | The summary counts matches over all searched stories as the query or options change. |

## Still open (queued)

- Paragraph marks (`^p`) and page, column and section breaks cannot be searched or inserted: matching is per paragraph.
- No search or replacement by character or paragraph format or by style.
- No find bar and no highlighting of all matches in the page.
- Comments are not searched; headers and footers of unused page styles and of hidden first or even variants are.
- Tracked deleted text and hidden text are matched like visible text.
- Word wildcard syntax (`<`, `>`, `@`, `{n,m}`, `[!x]`) is not translated: the regular expression option uses .NET syntax.
- Replacing inserts text with the formatting at the match start (the deleted text's), there is no "replace with formatting".
