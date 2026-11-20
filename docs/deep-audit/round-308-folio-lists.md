# Round 308: Folio styles, headers and lists

Area: the Folio styles (305) and Folio headers (305) queue items, then Folio lists and numbering.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Medium | DOCX styles with built-in Word names were written as `customStyle` without `w:uiPriority` or `w:qFormat`, so Word's gallery did not list them | `DocxBuiltInStyles` (injected into `DocxStylesCodec`) holds Word's priority and primary-format flag for built-in names (headings, toc levels, Title, Quote, caption, Strong, header, footer, Hyperlink, ...); those styles are written without `customStyle`, with `w:uiPriority` and, when Word shows them in the gallery and they are not hidden, `w:qFormat` |
| Medium | A DOCX heading style numbered by a second list, while the outline already came from another list, lost its own list and took the outline | `DocxListMap.OutlineSource` remembers the list the outline was taken from; a heading using another list keeps that list and level |
| Medium | `SECTION` and `SECTIONPAGES` counted only runs of one page style; continuous sections did not count | `SectionPageRuns` also counts top-level body sections that become Word sections (every section except a single-column generated index), from the flow; the section in effect at the top of the page decides |
| Minor | The header height reserved before pagination used the stored text of section fields | `PageFrames.Height` resolves them with a placeholder section of the document's page-count width |
| Medium | What follows a list label (tab, space, nothing) was dropped on reading both DOCX (`w:suff`) and ODT (`text:label-followed-by`), silently | `ListLevel.Follow` in the model, laid out (space appended, no tab), written and read in both formats |

## Checked without defects

List restart and continue in ODT (`text:continue-numbering`, `text:continue-list`), DOCX `lvlOverride` start overrides
and full level overrides, legal numbering in both formats, outline numbering through `pStyle` in the outline's
abstract numbering.

## Open (queued)

Word `w:lvlJc` and ODF level alignment are not in the model and are not declared; `w:lvlRestart` is not read; levels
missing from an `abstractNum` read as Arabic; no editor choice for the label follow; a DOCX continuous section with no
column or line-numbering change is not kept. Tests: `Round308StyleTests`, `Round308ListFollowTests`,
`Round308SectionRunTests`, `Round308SectionHeaderHeightTests`.
