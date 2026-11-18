# Round 196: Folio queued items (moved paragraph marks, spelled-out numbers, heading links, move markup)

Scope: the four Folio items queued by round 191. One commit per item; tests in the `Round196*Tests` classes. No build
or test run was made in the round's worktree: the coordinator builds and tests after the join.

## Fixed

- **Moved paragraph marks** (191, DOCX224). `DocxParagraphMarkChanges` now also gives the move of a paragraph mark:
  a `w:moveFrom`/`w:moveTo` in the mark's run properties takes the name of the last move range start of its
  paragraph (or the nearest preceding one), the same `move-<name>` id the moved runs get, with the author and date
  fallback when Word wrote no range. The inline reader gives that id to the insertion that continues over the mark
  (and merges it with the moved runs only when the ids match); the paragraph-mark join gives it to the joint deletion
  and absorbs only text deletions of the same move. The writer emits `w:moveFrom`/`w:moveTo` in the paragraph mark
  for deletions and insertions that carry a move id, and `WriteMoves` adds a named move range to a paragraph whose
  mark moved without moved text in it, so the reader links it back. ODT already carries `pt:move-id` on every
  `text:deletion` and `text:insertion`, including those spanning a paragraph break. `DOCX224` is gone: nothing of a
  move is lost any more. Tests `Round196DocxMovedParagraphMarkTests`, `Round196OdtMovedParagraphMarkTests`.
- **Spelled-out numbers in the document language** (191). `INumberSpellers` picks an `INumberSpeller` by the primary
  language subtag: English, Italian, German, Spanish and French, English for any other language. Each spells
  cardinals and ordinals up to 999,999 and gives the numeric ordinal suffix (`1st`, `1º`, `1.`, `1er`/`2e`).
  Defaults where a language inflects: masculine ordinals in Italian, Spanish and French (`Primo`, `Primero`,
  `Premier`), the weak form in German (`Erste`), as Word renders the same formats for a document in those languages;
  Italian accents compound `tré`, elides `ventuno`, `centottanta`; Spanish shortens `veintiún mil`; French writes
  `soixante et onze`, `quatre-vingts`, `deux cents`. `NumberSystems.Render` takes the language; the layout's list
  labeling uses the first run language of the paragraph, else the document language, through
  `INumberFormatter.Render(value, language)`; plain-text export does the same. RTF writes the Word `\levelnfc`
  codes (5 ordinal, 6 cardinal text, 7 ordinal text, 10 ideograph digits, 20/21 katakana, 22 leading zero, 37 Chinese
  counting, 45 Hebrew, 46 Arabic alphabetic, 48 Arabic abjad, 53 Thai letters, 58/59 Russian) from one table shared
  with the reader. HTML writes a CSS `list-style-type` keyword where one exists (`decimal-leading-zero`, `hebrew`,
  `hiragana`, `katakana`, `katakana-iroha`, `cjk-decimal`, `simp-chinese-informal`) and otherwise the literal label
  of each item as a CSS string (`list-style-type:'Primo. '`). Tests `Round196NumberSpellingTests`,
  `Round196ListLanguageLabelTests`, `Round196ListNumberExportTests`.
- **Heading places in the hyperlink dialog** (191). Heading places link to the heading's bookmark (the
  `__RefHeading__` bookmark contents entries and cross-references use; `IReferenceTargets.HeadingAnchor` gives the
  existing one or the free name it would take), so equal headings link to themselves. `IFolioLinks.Settle` creates
  the bookmark when the chosen address names a heading that has none yet; the text and object hyperlink commands call
  it inside the link's own transaction, so the bookmark and the link are one undo step. `#text|outline` links in
  existing documents still resolve. Test `Round196HeadingLinkTests`.
- **Moves in the review panel and markup** (191). Research: Word marks moved text by default in green, moved-from
  text with a double strikethrough and moved-to text with a double underline, distinct from the author colour of
  plain changes; the review pane lists "Moved (from)" and "Moved (to)". Folio follows that: `ChangeMarkup` draws
  moved-to text double underlined and moved-from text (in place or as the inline deleted words) double struck, in
  green; the review panel has the kinds `MovedFrom` and `MovedTo` ("Moved from"/"Moved to" in the five languages),
  struck through and underlined in the list. The double strike is a display-only `TextRun.DoubleStrikethrough`, set
  by the markup on its display copies; the character model has no double strike, so no file can carry it. Tests
  `Round196MoveMarkupTests`, `Round196ReviewMoveTests`.

## Still open (queued)

- RTF import maps numbered lists to the default "1, 2, 3" style, so the extended formats written to `\levelnfc` read
  back as Arabic; Hiragana has no RTF code.
- The list dialog's preview spells numbers in English.
- No option for the move colour.
