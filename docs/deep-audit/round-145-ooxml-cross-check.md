# Round 145: OOXML cross-check with OnlyOffice and the reference suite

On Linux (Fedora). The reference suite is now installed only as a flatpak, and OnlyOffice Desktop Editors is installed
as a flatpak too.

## Tooling

- `tools/reference-suite/run` starts a native installation of the reference suite when one exists (macOS bundle,
  `/usr/bin`, `/usr/local/bin`, `/opt`), otherwise its flatpak with `--filesystem=/tmp`: the flatpak's `/tmp` is
  private, so the throw-away profiles and work folders the scripts create under `/tmp` were invisible to it ("source
  file could not be loaded"). The fixture scripts (`tools/*-fixtures.sh`), `verify-signatures` and `verify-encrypted`
  use it by default; `SOFFICE` and `REFERENCE_SUITE` still override it.
- `verify-signatures.sh` ran under zsh, which is not installed here; it now runs under bash.
- `tools/onlyoffice/x2t` runs OnlyOffice's bundled converter (`/app/bin/opt/onlyoffice/desktopeditors/converter/x2t`)
  headless inside the flatpak: `x2t input output`, the format follows the output extension. DOCX, XLSX and PPTX to
  ODT, ODS and ODP, and the reverse, work; exit code 0 means OnlyOffice read the input. PDF and image output fail
  (code 80): they need the editor's JavaScript renderer, which the desktop app configures at start-up. See
  `tools/onlyoffice/README.md`.

## Method

1. **Our OOXML read by both**: 51 corpus files (ODF and OOXML; tracked changes, comments, charts, floating tables,
   numbering, pivot tables, conditional formats, merged cells, shapes with text, SmartArt groups, animations) were read
   by PulseOffice and written to DOCX, XLSX and PPTX with `corpus-audit` (`CORPUS_AUDIT_KEEP`). Each OOXML copy was
   converted to ODF by OnlyOffice (`x2t`) and by the reference suite (headless `--convert-to`). The words of each
   conversion were compared with those of PulseOffice's own ODF copy of the same model.
2. **Their files read by us**: the same 51 files converted by OnlyOffice to the other family (ODF to OOXML and back),
   then run through `corpus-audit` (read, write ODF and OOXML, count features).

## Results

- Neither application refused any OOXML file written by PulseOffice (51 of 51 read by both).
- Text comparison: no word of our DOCX, XLSX or PPTX was missing in both conversions except metadata artefacts of the
  extraction (comment and change dates). Single-converter gaps were checked: OnlyOffice drops the text of a floating
  table split across a footer, chart titles and hidden shapes' text in its own ODF output, which the reference suite
  keeps from the same file (OnlyOffice's converter); the reference suite rewrites pivot output with its own captions
  and the locale's number format and recomputes it.
- One pivot table in the reference suite's conversion changed value (42.5 instead of 25.5): its ODS source range has a
  standard filter, which PulseOffice already declares as not kept (ODS reader). Its second pivot changed from tabular
  to compact layout: defect 1.
- OnlyOffice-written files: 12 of 51 were refused by PulseOffice (defects 2 and 3). After the fixes all 51 are read
  and written (102 copies). The remaining LOSS rows (17) come from what OnlyOffice itself wrote: `draw:g` elements
  without children (SmartArt and a floating table), pivot tables whose every attribute is "--" with no source range,
  SmartArt shapes sharing one `xml:id` (open item 1) and a comment with only `w:commentRangeEnd` (open item 2).

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | medium | `OdfPivotMapper` (ODS reader) | A pivot table without `table:data-pilot-layout-info` was read as compact, but ODF's default `table:layout-mode` is tabular: pivots from other applications turned compact when written to XLSX or refreshed. | The default is tabular; our writer always writes the layout info. |
| 2 | serious | OOXML package opening (DOCX, XLSX, PPTX) | A core-properties date with more than seven fractional digits (OnlyOffice writes nanoseconds, e.g. `10:37:02.406879919`) made System.IO.Packaging throw, and the whole file was refused (`DOCX107`); 10 of the 12 refusals. | New package repair step `CorePropertiesDateRepair` trims the fraction to ticks before the package opens. |
| 3 | serious | `OdpGroupFlattener` (ODP reader) | Two groups with the same `xml:id` on one slide (OnlyOffice writes them for SmartArt) threw a duplicate-key exception and the whole presentation was refused (`ODP101`). | A repeated group id is read as an anonymous group; both groups and their members are kept. |

## Open (minor)

1. OnlyOffice writes the same `xml:id` on several SmartArt shapes of one slide (invalid ODF). PulseOffice keeps every
   shape, but group membership resolves by id, so in the PPTX copy two shapes leave their group.
2. OnlyOffice writes a DOCX comment with only `w:commentRangeEnd` (no start, no `w:commentReference`). The DOCX copy
   keeps it; the ODT copy drops it. With a start and a reference the comment survives to ODT.

## Not verified

- PDF output of OnlyOffice (its renderer is not usable headless from the flatpak).
- Opening in Word, Excel and PowerPoint, as before.
- The cross-check covered 51 corpus files, not the whole corpus; the comparison is by words, so formatting differences
  were not measured.
