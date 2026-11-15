# Round 146: automatic corpus round on Linux

On Linux (Fedora), 2026-11-15, branch `audit-round-146` from `work`.

## Method

1. The external reference clone was updated (`git pull --depth 1 --rebase`; a plain pull refused the divergent
   shallow history).
2. `tools/corpus-audit` was built in Release and `run` over the whole versioned list (`corpus.txt`, 5016 files) with
   one worker at a time (`run … 1 120`), inside a `systemd-run` scope capped at 6 GB. One worker at a time takes
   about four hours instead of the eight minutes of eight workers.
3. The new `results.tsv` was compared with round 145's with `diff`.
4. The two minor items left open by round 145 were fixed with regression tests seen red first.
5. After the fixes, the rebuilt tool was run with `one` on 325 files (every file with a row in `results.tsv` and every
   fifteenth ODP, DOCX and ODS of the list): the DECL and LOSS rows are identical to the full round's.
6. After each fix, the affected test projects except the three command sweeps were run with `tools/run-tests.py`.

## Numbers

| | Round 145 | Round 146 |
|---|---|---|
| Files | 5016 | 5016 |
| Copies written | 9948 | 9946 |
| LOSS | 0 | 0 |
| DECL | 127 | 125 |
| READFAIL | 28 | 28 |
| NOTZIP | 14 | 14 |
| MISSING | – | 1 |

The only changed file is one ODT of the list: the update of the clone removed it, so its
two DECL rows (images, `ODF176`, to ODT and DOCX) went away. The tool reported the missing file as a crash
(`CRASH:exit134`, an unhandled `FileNotFoundException`); it now reports it as `MISSING` and leaves it out of the
failures. The list was not regenerated, so the rounds stay comparable; no new LOSS, no crash, no refusal.

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | minor | `OdpGroupFlattener` (ODP reader) | Round 145 open item 1: OnlyOffice writes the same `xml:id` on several SmartArt shapes of a slide; group membership resolved by id pointed every group at the last shape read, so shapes left their group in the PPTX copy. | A shape id already used on the page is replaced, inside its group, by a group-local id. |
| 2 | minor | `UnanchoredAnnotations` (DOCX reading) | Round 145 open item 2: a DOCX comment with only `w:commentRangeEnd` kept a lone end mark; the ODT writer wrote an `office:annotation-end` without its annotation and the comment was lost in the ODT copy. | A comment that ends without a start is anchored as a point where its range ends. |
| 3 | medium | `OdsGroupedObjects` (ODS reader) | The check for unsupported objects in sheet groups walked every descendant of every nested group: quadratic, 4 min 17 s for the 100,000-level nesting test, whose chunk the 5-minute idle watchdog killed. | Only outermost groups are walked (13 s); the test now bounds the reading time. |
| 4 | minor (tool) | `corpus-audit` | A list entry no longer in the clone was reported as a crash. | New `MISSING` status. |

## Not verified

- The two round 145 fixes were verified with unit tests reproducing OnlyOffice's markup, not by converting the
  original OnlyOffice files again (they were in a temporary folder that no longer exists).
- Opening the copies in Word, Excel and PowerPoint, as in every round.
- The three command sweep projects were not run (by instruction).

**Status: one medium found and fixed; no serious or medium open.**
