# Round 199: list formats, move display and chart category levels from the audit queue

Not built nor run in this round (the coordinator builds and tests after the join).

## 1. RTF list number formats

- The reader already turned `\levelnfc`/`\levelnfcn` into `NumberFormat` through `IRtfVocabulary.ListFormat`, the table
  the writer uses for `ListCode`, but then picked one of three default list styles by the level's format, so ordinal,
  spelled-out, Hebrew and the other extended formats came back as "1, 2, 3".
- `RtfReadSession.ListStyleFor` keeps a default style (bullets, "1, 2, 3", "a, b, c", "i, ii, iii") only when every read
  level matches it; otherwise it adds one list style per RTF list (`RTF List <id>`) with the read format of each level
  (deeper levels repeat the last read one), and every paragraph of that list uses it.
- Hiragana: RTF (like Word's numbering formats) has no Hiragana list code. The writer uses the nearest one, Katakana
  (`\levelnfc20`), and `CheckFidelity` declares it as `RTF007` (info).

Tests: `Round199RtfListFormatTests` (Formats.Interchange).

## 2. List dialog preview language

- `ListFormatRequest.Language` carries the document language; `ListSampleView`, `IListSampleRows.Rows`,
  `IListLabeler.Label` and `Number` pass it to `INumberSystems.Render`, which already spelled numbers per language.
  Folio's bullets-and-numbering, chapter numbering and list style dialogs pass `TextDocument.Language`.

Tests: `Round199ListPreviewLanguageTests` (UI.Controls).

## 3. Tracked move display

- `IMoveDisplayOptions` (Documents.Layout) gives `MoveDisplay`: colour (null = the author's colour, the same colour as
  that author's insertions and deletions), moved-from mark (double or single strikethrough) and moved-to mark (double
  underline, underline or bold). The default, `StandardMoveDisplayOptions`, is Word's green with double lines.
- `FolioChangeSettings` adds three settings under Documents › Tracked changes (`folio.changes.moveColor`,
  `folio.changes.movedFrom`, `folio.changes.movedTo`), texts in the five languages; `SettingsMoveDisplayOptions` reads
  them. `ChangeMarkup` uses the options for moved text and the inline moved-from words, and the display is part of its
  cache key.
- Open: a change of the setting is drawn on the next layout of a paragraph; open documents are not relaid out at once.

Tests: `Round199MoveDisplayTests` (Documents.Layout).

## 4. XLSX formula of model-only category levels (not changed)

- Model-only levels (`ChartDefinition.CategoryLevels`) are set only for pivot charts. The pivot renderer puts every
  parent item on its own row in every layout, so no rectangle of cells holds the outer labels on the leaf rows: a
  formula over several columns would make an application that refreshes from cells read empty or misplaced outer
  labels. The one-column `c:multiLvlStrRef` with the full multi-level cache is also the form written for compact pivot
  layouts. The item stays in the queue, reworded, until another application has opened such a file.

## 5. Native ODF inline category levels

- ODF lets `chart:categories` name a range of several columns; the label columns before the leaf column are the outer
  levels. The inline local table now writes the outer levels first (outermost in column A), the leaves after them, then
  the series and the hidden columns, all shifted by the number of levels, and writes `chart:categories` over the label
  columns. Custom error references are shifted with the columns before writing (`ChartInlineErrorValues.Remap`).
- The `pulseoffice:category-level-column`/`-count` attributes are no longer written; the reader still accepts them for
  older files, and when the levels come from a native range starting at column A it removes the shift from hidden
  columns so they keep their logical numbers.

Tests: `Round199OdfCategoryLevelTests` (Formats.OpenXml, through the ODP chart mapper); the existing
`Round195ChartCategoryLevelTests` ODF case covers levels with hidden columns and error references.
