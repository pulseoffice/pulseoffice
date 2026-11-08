# Deep audit round 98: Folio styles and numbering

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Documents/Styles/StyleResolver.cs | 137/137 | cycle-safe chain; lookups were linear (see D1) |
| src/PulseOffice.Documents/Styles/StyleSheet.cs | 168/168 | D1 fixed |
| src/PulseOffice.Documents/Styles/Styles.cs | 95-139 of 266 | list levels clamped by `Level` |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxNumberingCodec.cs | 207/207 | levels and starts bounded; abstract/num aliasing sound |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxParagraphCodec.cs | 95-130 | D3, D4 fixed |
| src/PulseOffice.Formats.Odf/Odt/OdtBodyReading.cs | 140-200 | list depth capped, nesting bounded by `MaximumDepth` |
| src/PulseOffice.Documents.Layout/Flow/ListLabeling.cs | 151/151 | D2, D5 fixed |

Not covered this round: style dialogs and commands in Documents.UI, ODT `text:list-style`/`outline-style`
writers, `continue-numbering`/`xml:id` list linking, numbering in headers. They remain for a follow-up round.

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| D1 | medium | StyleSheet.cs `StyleCollection.Find`/`Add` | every lookup scanned the list: resolution was O(styles x chain depth) per paragraph and per run, loading n styles was O(n^2) | name index (`Dictionary`, first entry wins as before) kept in step by Add/Replace/ReplaceAll/Remove/CopyFrom |
| D2 | medium | ListLabeling.cs `Styled` | a level starting at 0 (or restarted at 0) never advanced: "0 is not started" was encoded with 0 | counters are `int?`; null means not started |
| D3 | medium | DocxParagraphCodec.cs `Extras` | `w:ilvl` taken unbounded (1000 or negative) into `BulletLevel` | clamped to 0..LevelCount-1 |
| D4 | minor | DocxParagraphCodec.cs `Extras` | negative `w:outlineLvl` produced outline level <= 0 | only 0..8 are read |
| D5 | minor | ListLabeling.cs | parent labels used `max(count, start)`, hiding a parent restart below its start value; unstyled items indented by an unbounded level | parent uses its counter or its start; level clamped |

## Tests

- tests/PulseOffice.Documents.Tests/StyleCollectionRound98Tests.cs
- tests/PulseOffice.Documents.Layout.Tests/ListNumberingRound98Tests.cs
- tests/PulseOffice.Formats.OpenXml.Tests/DocxHostileNumberingRound98Tests.cs

Documents.Tests 366/366, Documents.Layout.Tests 244/244, Formats.OpenXml.Tests 2418/2418 green.
