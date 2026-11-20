# Round 326: Folio find and replace leftovers

Area: the "Folio find and replace (325)" queue item, read in full: paragraph marks and breaks, format and style
search and replace, a find bar with highlighting, comments and unused headers, tracked deleted and hidden text, Word
wildcards, replace with formatting.

## Design

- The search no longer matches paragraph by paragraph. `ISearchTextBuilder` flattens each story into one string with
  a mark per paragraph end (`\n`) and a mark before a paragraph that starts with a page break (`\f`), a column break
  (U+000E) or a page style change, which is Folio's section break (U+001D); every character keeps its document
  position, paragraph and run. `DocumentSearch.Matches` returns `SearchMatch` (range, found text, breaks inside it,
  comment, regex match). Literal queries keep culture-aware matching and never cross a mark.
- `ISearchStories` gives the stories in search order: the caret's story, the body, notes, text boxes, the headers and
  footers that print (enabled, of used page styles followed through their next styles, first and even variants only
  when the style shows them), then comments.
- `ISearchReplacing` replaces one match: it deletes the range, clears a page or column break or section start found
  at the start of a surviving paragraph, and inserts the replacement splitting paragraphs at `^p` and setting page
  (`^m`) or column (`^n`) breaks; an empty replacement with a format only formats the found text, as in Word.
- `IWordWildcards` translates Word wildcards to .NET patterns (`?`, `*`, `@`, `{n,m}` or `{n;m}`, `<`, `>`, `[ ]`,
  `[!x-y]`, groups, `\` escapes, `^p`/`^13`-style codes) and `\1`, `^&` in the replacement; wildcard searches are case
  sensitive and ignore whole words, as in Word.
- Shared find panel (`FindPanelView`, `IFindPanelSource`, `IFindPanelPages`, `TargetFindPanelSource` in
  `UI.Controls/Search`): a non-modal side panel page with query, options, Previous, Next and Replace…; Folio highlights
  every match on the page (`IFolioFindHighlights`, recomputed per document revision, drawn by `FolioTextOverlays`),
  Ledger (`ledger.data.find`) and Deck (`deck.edit.find`) reuse it over their existing find targets. Folio's
  `folio.edit.find` takes Ctrl+F; the find and replace dialog moves to Ctrl+Shift+H (Ctrl+H is reserved).
- The shared dialog gained format choices (`FindChoice`, find and replace columns in a "Format and style" expander);
  Folio offers paragraph style, character style and bold/italic/underline (and their negations).

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Medium | `^p`, `^m`, `^n`, `^b` could not be searched or inserted. | Story-wide search text with marks; replacer splits and sets breaks. |
| Medium | No format or style search and replace. | `SearchFormat` on the query and on the replacement; dialog choices. |
| Medium | No non-modal find bar, no highlighting of all matches. | Shared find panel, Folio highlights; wired in Ledger and Deck. |
| Medium | Comments not searched, headers of unused page styles and hidden first or even variants searched. | `ISearchStories`; a match in a comment selects the comment's anchor and says so. |
| Medium | Hidden text matched. | Hidden runs are left out of the search text. |
| Medium | Word wildcard syntax not available. | "Use wildcards" option with the translator. |
| Minor | No replace with formatting. | Replace format choices; empty replacement formats only. |
| None | Tracked deleted text matched (queue claim). | Verified not a defect: deleted text lives in the deletion's own container, outside paragraph text; nothing to change. |

## Still open (queued)

- Format matching reads direct run and paragraph properties and style names; bold or italic inherited from a style
  is not resolved.
- The find panel in Ledger and Deck does not highlight matches (their sources do no highlighting yet).
- Section breaks are Folio page style changes; a `SectionBlock` boundary is not a `^b` match. `^b` cannot be inserted.
- Matches inside comments are counted, replaced and walked (via their anchor) but cannot be selected inside the
  comment text, since comments are edited in a dialog.
- Not tried in the running app: the panel, highlights and the dialog's format expander.
