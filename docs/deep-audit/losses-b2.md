# Declared losses, block B2: tool fixes and misattributed drops

Block B2 of [declared-losses-analysis.md](../declared-losses-analysis.md): fix the corpus-audit tool so that a
declaration only covers the element kinds it really speaks about, then fix the real losses that the old keyword
matcher was hiding.

## Real losses found and fixed

| # | Severity | Where | Defect | Fix | Test |
|---|---|---|---|---|---|
| 1 | Serious | `Formats.OpenXml/Wordprocessing/DocxWriter.cs` (`WriteBody`) | Page-anchored objects are queued in `PendingPageObjects` and flushed only by the first body paragraph written through the inline writer. A body with no paragraph (a corpus file: three page-anchored frames, empty body) or whose only paragraphs are bare cell fillers wrote `<w:p/>` and silently dropped every page-anchored object. | After the body loop, if the body is empty or objects are still pending, a carrier paragraph is written through the normal block writer, so the objects are emitted with it. | `DocxPageObjectTests.PageAnchoredBoxesSurviveAnEmptyBody`, `...SurviveABodyWithoutParagraphs` (both failed before) |
| 2 | Serious | `Formats.OpenXml/Wordprocessing/DocxRevisionWriter.cs` (`Deleted`) | Objects inside a tracked deletion (object marks and objects anchored to a removed paragraph) were not written to DOCX; ODT kept them (`redline_fly_at_para_one_paragraph.odt`, a corpus file). | Deleted runs now carry the drawings of the removed paragraphs (`w:del` may hold `w:drawing`, ECMA-376 §17.13.5.14), through `DocxInlineWriter.Drawing`. Reading them back is still DOCX175 (block B7). | `DocxPageObjectTests.PicturesInsideATrackedDeletionAreWrittenAsDeletedDrawings`; corpus rows were LOSS before |
| 3 | Medium | `Formats.OpenXml/Presentation/PptxRunLinks.cs` (`PptxSlideLinkResolver`) | Slide jumps were resolved only by page name or slide id. Targets written by the reference suite as the display name `Slide N` (a corpus file) or as the name of an object on a slide (a corpus file) were silently dropped in PPTX. | New core service `ISlideTargetResolver` / `SlideTargetResolver` (`PulseOffice.Presentations`): page name, id, positional name (`slide N`, `page N`, case-insensitive), then the slide holding an object with that name. | `SlideTargetResolverTests` (8 cases) |
| 4 | Medium | `PptxFidelityRules` | Jumps to slides or objects that do not exist (`badPercentageValue.odp` jumps to pages 2-6 of a one-page deck) were removed silently. | Declared as `PPTX039` (Warning). ODP keeps them. | corpus row now DECL `PPTX039` |
| 6 | Medium | `Formats.OpenXml/Wordprocessing/DocxBlockWriter.cs` (`Paragraph`) | A paragraph with an outline level whose paragraph style is not the heading style of that level (`number-portion-format.odt`: `text:h` with style Standard) got `w:outlineLvl` but no numbering, so its outline number vanished in DOCX. | `OutlineNumbered`: such paragraphs get `numPr` pointing to the outline numbering at their level. | `DocxPageObjectTests.AnOutlineParagraphWithAPlainStyleKeepsItsOutlineNumbering` |
| 5 | Medium | `Formats.OpenXml/Wordprocessing/DocxDrawingCodec.cs` | A linked text box (continuation of a chain, `wps:linkedTxbx`) is read as a plain text box and loses its fill and outline (a corpus file). The model's `TextBoxContent` has no fill/line. | Declared as `DOCX184` (Warning). Plan: give `TextBoxContent` a shape style (fill, line), read/write it in both formats; joins block B7 (group text boxes). | corpus row DECL `DOCX184` |

PPTX020 conclusion: of its six rows, three were real drops (items 3 and 4), one a tool false positive (invisible
empty shapes, below) and two are covered by their own correct codes (`ODP180`, `ODP102`). PPTX020 itself covers no
element kind any more.

DOCX040 conclusion: of its 22 rows, three were real losses (items 1 and 2); the other rows are covered by `DOCX171`
(content in headers of unused page styles, counted from `styles.xml`), `ODF176` (missing picture files) and `ODF190`.
DOCX040 covers no element kind any more.

## Tool fixes (`tools/corpus-audit`)

- **Explicit code-to-feature table.** `DeclarationMatcher` no longer matches keywords in messages. The shared,
  versioned table `declared-features.json` lists, for every diagnostic code, the feature kinds it really declares
  (`*` for codes about whole header/footer content: DOCX045, ODT007, DOCX125, DOCX171). A code missing from the
  table covers nothing, so a new code produces honest LOSS rows until it is added. `DeclarationTable` loads it,
  `DeclarationMatcher` consults it.
- **Per-package rule context.** Rules now receive a `RuleContext` built once per package by `RuleContextFactory`
  (styles parsed from the package), instead of bare namespace keys.
- **DOCX lists via styles (DOCX039 false positive).** A DOCX paragraph is in a list when its own `w:numPr` has a
  non-zero `numId`, or when its paragraph style (following `basedOn`) carries one; styles with `w:outlineLvl` are
  outline numbering and are excluded, as ODF outline headings are.
- **ODT lists via styles (symmetric).** An ODT paragraph or heading outside `text:list` whose paragraph style (with
  parents) has a `style:list-style-name` other than the outline style counts as a list paragraph.
- **Outline numbering.** ODT headings numbered by the outline style (`text:outline-level-style` with a number
  format) count as list paragraphs, as their DOCX twins (heading styles with `numPr`) do.
- **Silent numbering.** DOCX numbering levels with no visible label (`numFmt none` and no literal text, or an empty
  `lvlText`) and ODT list styles whose first level has no format, prefix or suffix are not counted as lists.
- **Invisible shapes.** ODF shapes whose automatic graphic style has `draw:fill="none"` and `draw:stroke="none"` and no
  text are not counted (as PPTX already did); the DrawingML check now applies to `xdr:sp` too and treats a missing
  `a:ln` without a shape style reference as no line.

## Results

Previous run: LOSS 0, DECL 529. First run after the matcher change (before the symmetric list and invisible-shape
fixes): LOSS 73, DECL 508; all 73 were inspected: 68 were tool asymmetries in list counting, 2 in invisible shapes, and
1 the real loss of item 5. Further intermediate runs surfaced the outline and silent-numbering asymmetries and the real
loss of item 6. Final run: **LOSS 0, DECL 503** (`tools/corpus-audit/results.tsv`, `summary.md`).

## Tests run

- `PulseOffice.Formats.OpenXml.Tests` (whole project).
- `PulseOffice.Presentations.Tests` (`SlideTargetResolverTests`).
- `corpus-audit one` on every changed row, and full `corpus-audit run`.
