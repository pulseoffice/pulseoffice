# Round 171: shell file handling

Area: open, save and save as, atomic writing, encryption, autosave and recovery, recents, templates, properties,
closing with unsaved changes.

## Defects

1. **Medium: saving a document opened through a symbolic link replaced the link with a regular file.**
   `AtomicFileWriter` wrote a temporary file beside the path and moved it over the path, so the link was
   overwritten and the real file kept the old content; the user's next edit through the original location was lost
   from view. The writer now resolves the final link target and commits beside it, keeping the link.
   Test: `Round171AtomicSymlinkTests` (Robustness.Tests).

## Not built or run

Per the cycle's rules the coordinator builds and tests after the join.

## Open items

The rest of the area (encryption re-save, recovery list, recents, templates, multi-window close) was only partly
reviewed in this round and stays for the next cycle.
