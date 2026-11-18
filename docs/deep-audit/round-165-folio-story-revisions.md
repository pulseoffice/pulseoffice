# Round 165: Folio tracked changes outside the body, and round 162's open items

Area: tracked insertions, deletions and format changes in headers, footers, notes, text boxes, table cells and
comments, and the open items of round 162. Tests and builds are run by the coordinator after the four audits of the
cycle are joined; none was run during this round.

## Defects fixed

1. **Medium: ODT lost the previous formatting of tracked format changes.** The writer emitted an empty
   `text:format-change`, so after saving to ODT and reopening, rejecting a character or paragraph format change only
   removed the mark (declared as `ODT002`, but a native-compatible extension was available, against rule 7's "declaring
   is the last resort"). The previous runs, or the previous paragraph, are now written as a paragraph inside our
   `pt:previous-formatting` child of `text:format-change` (other readers ignore it) and read back. `ODT002` now covers
   only changes whose previous formatting was never known (format changes read from other applications' ODT files).
   This applies in every story, since headers and footers embed their regions with the same writer. Test:
   `Round165OdtFormatChangeTests.The_previous_formatting_of_a_tracked_format_change_survives_the_round_trip`.
2. **Medium: rejecting a character format change spanning several paragraphs did not restore the old formatting.**
   Such a region appears after pressing Enter inside a tracked format change, or from a file. `RevisionResolver` now
   walks the covered paragraphs of the story in order, slices the previous runs over them and restyles each segment
   inside the verdict's history step; if the covered text no longer matches the previous text in length, only the
   mark is removed, as before. Test:
   `Round165StoryRevisionTests.Rejecting_a_format_change_over_two_paragraphs_restores_the_previous_formatting_of_both`.

## Checked clean

- Header and footer revisions are written inside their own region of `styles.xml` and read back (existing
  `OdtHeaderChangesTests`); DOCX run property changes in every part go through the same inline reader, which closes a
  format change at each paragraph end.
- Accept and reject resolve paragraphs through `IStoryCatalog`, which covers body, notes, comments, object stories and
  every header and footer variant.

## Open

- Moves are still not modelled (`DOCX224`, from round 162).
- Recording, display and navigation of changes inside text boxes, table cells and comments were reviewed only through
  the shared story catalog; a live check in the app was not done this round.

## Status

Two defects fixed (both medium). Awaiting the coordinator's build and test run.
