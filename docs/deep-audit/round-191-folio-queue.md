# Round 191: Folio queued items (lists, tracked moves, indexes, references)

Scope: the seven Folio items queued by rounds 162, 167, 179 and 183. One commit per item; tests in the
`Round191*Tests` classes. No build or test run was made in the round's worktree: the coordinator builds and tests after
the join.

## Fixed

- **List level text with different separators** (183). `ListLevel.Separators` keeps the text of each gap between two
  shown numbers when the gaps differ (`%1.%2-%3`); `Separator` stays the first (and the only one when all gaps are
  equal), `SeparatorAt(gap)` gives the gap's text. The layout labeler, DOCX (`w:lvlText` read placeholder by
  placeholder and written gap by gap) and ODT (our `pt:level-gap pt:separator` children of the level style, after the
  existing `pt:level-separator`) all use it. ODF's `text:display-levels` has no separator at all, so the extension is
  the only place for it; other readers see the dotted form. Tests `Round191ListGapLabelTests`,
  `Round191ListGapSeparatorTests`, `Round191OdtListGapSeparatorTests`.
- **Separator in the list dialog** (183). The shared list editor has a `ListFeatures.Separator` row ("Separator
  between levels", five languages); Folio offers it for list styles and chapter numbering; the shared labeler joins
  with it. Changing it replaces any per-gap list with the single separator; leaving it untouched keeps the per-gap list
  (same reference, so an unchanged dialog adds no undo step). The change is the dialog's one undo step. Test
  `Round191ListSeparatorDialogTests`.
- **Word number formats** (179). `NumberFormat` (and the dialog's `ListNumbering`, same order) gains zero-padded,
  ordinal, ordinal text, cardinal text, Hebrew, Arabic abjad and alphabetic, Hiragana, Katakana (aiueo and iroha),
  ideograph digits, Chinese counting, Thai letters and Russian lower and upper case. The numbers are rendered by the
  shared `NumberSystems` service (`PulseOffice.Text.Rich`), used by the layout (`SystemNumbers` formatters), the list
  dialog's labeler and plain-text export. DOCX maps them to `decimalZero`, `ordinal`, `ordinalText`, `cardinalText`,
  `hebrew1`, `arabicAbjad`, `arabicAlpha`, `aiueoFullWidth`, `irohaFullWidth`, `ideographDigital`, `chineseCounting`,
  `thaiLetters`, `russianLower`, `russianUpper`; Hiragana has no Word value and is written as `custom` with
  `w:format="あ, い, う, ..."`, as Word 2010 does. ODT writes the sample token other suites use in `style:num-format`
  (`01, 02, 03, ...`, `א, ב, ג, ...`, `ア, イ, ウ, ...`, ...) and, for every extended format, our `pt:num-format` name,
  which the reader prefers; ideograph digits have no common token and degrade to `1` for other readers. DOCX244 and
  ODT183 remain for what is still unknown (hexadecimal, Korean, Hindi, Arabic-Indic digits, ...). Spelled-out numbers
  are English, as Word renders them for an English document; other languages are queued. Tests
  `Round191NumberSystemsTests`, `Round191ListNumberFormatTests`, `Round191OdtListNumberFormatTests`, and the updated
  `Round179FolioListTests`.
- **Tracked moves** (162). `Insertion.MoveId` and `Deletion.MoveId` link the two sides of a move (several pieces may
  share one id). `TrackedChangeLog.MovedWith` gives every piece of a move, and `RevisionResolver` widens each request
  with it, so accepting or rejecting either side settles the whole move in one undo step. DOCX reads `w:moveFrom` and
  `w:moveTo` runs with the name of the nearest preceding `w:moveFromRangeStart`/`w:moveToRangeStart` (author and date
  when Word wrote no range) and writes them back as `w:moveFrom`/`w:moveTo` runs inside named move ranges (the inline
  writer produces insertions and deletions; `DocxRevisionWriter.WriteMoves` turns the registered ones into move
  elements once the parts are built). ODT, which has no moves, carries our `pt:move-id` on `text:insertion` and
  `text:deletion`; other readers see a plain deletion and insertion. Snapshots keep the move id and, newly, the nested
  insertion of a deletion. DOCX224 now covers only moved paragraph marks, still read as a separate deletion and
  insertion. Tests `Round191TrackedMoveTests`, `Round191DocxMoveRoundTripTests`, `Round191OdtMoveTests`, updated
  `Round162DocxMoveTests`.
- **Indexes and pending deletions** (167). Decision: a regenerated index or caption list shows the document as it
  will be once the pending changes are accepted, whether or not markup is shown, so it does not change when the view
  toggles; pending insertions are in, pending deletions are out. Word builds its tables from the text it would keep as
  well (deleted headings disappear from an updated table of contents once the deletion is accepted, and the comparable
  word processors leave deleted text out of generated indexes). Deleted text in Folio already lives in the deletion,
  not in the paragraph, so headings and captions were right; the gap was rows pending a tracked deletion, whose cells
  stay in the table: `IndexScope` now skips their paragraphs. Test `Round191IndexDeletionTests`.
- **Caption references with two sequence fields** (167). `ReferenceTarget.Sequence` carries the chosen category;
  `Prepare` and `Touches` name and check the sequence field of that category, falling back to the first one when none
  matches. The field dialog's edit path passes the category too. Test `Round191CaptionReferenceTests`.
- **Contents links** (167). Outline entries of a contents index no longer link to `#text|outline`: the composer gives
  each heading its heading bookmark (the same `__RefHeading__` bookmark cross-references use, reused when present) in
  the index's own undo step and links to it, so equal headings link to themselves and the link crosses DOCX
  (`w:anchor`) and ODT as a bookmark. `#text|outline` links in existing files still resolve. Test
  `Round191ContentsLinkTests`, updated `IndexTests`.

## Still open (queued)

- Moved paragraph marks (DOCX224).
- Spelled-out numbers in languages other than English; RTF and HTML export of the new number formats.
- Heading places in the hyperlink dialog still link by text.
- The review panel and change markup do not show moves differently from deletions and insertions.
