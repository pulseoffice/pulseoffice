# Round 148: Ledger HTML export of merges over hidden rows and columns (2026-11-16)

Focused code audit picking up the minor items left open by round 147.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Minor | In the Ledger HTML export (file and clipboard fragment), a merged block whose first cell lay in a hidden row or column lost its text: the anchor was skipped with its hidden row or column and every visible cell of the block was treated as covered, so the block vanished from the table. | `HtmlSpreadsheetWriter` computes the visible part of each merge (clipped to the exported range, hidden edge rows and columns trimmed); the first visible cell carries the anchor's value, style and the spans of the visible part. Test: `Round148HtmlMergeTests`. |

## Left open

- Minor: a CSV text that looks like a number reads back as a number. The reference suite behaves the same without
  "quote all text"; changing it means quoting text on export, a user-visible format choice, not a cheap fix.

## Verified / not verified

- Verified: `tools/run-tests.py --changed work`.
- Not verified: the export opened in a browser or pasted into another application.
