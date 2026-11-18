# Round 202: Ledger formula engine, second pass

Scope: the items round 200 left unreviewed: XLOOKUP and XMATCH with an array lookup value, spill ranges under
structure edits and range moves, A1# spill references and their XLSX and ODS crossing, iteration settings with
circular references, structured table references.

## Defects fixed

- **Medium: a blocked spill formula lost its dynamic-array nature in XLSX and ODS.** A formula whose array result was
  blocked (#SPILL!) had no spill range and recorded no array use, so both writers stored it as a plain formula
  (no `cm` / dynamic-array metadata in XLSX, no spill mark in ODS) and the readers loaded it back with implicit
  intersection: after reopening and clearing the obstacle it showed one value instead of spilling. The engine now
  records array use whenever a non-intersecting formula produces a multi-cell result.
- **Medium: no circular-reference warning.** A circular formula gave an error with nothing saying where the cycle
  was. Ledger's status bar now names the first cell of the cycle (`CircularReferenceReport`) and clears when it is
  resolved; with iterations on there is no notice, as in the other applications. Text `status.circular` in the five
  languages.
- **Minor: A1# on a blocked anchor gave #REF!** instead of propagating #SPILL!; it now gives #SPILL! and recovers when
  the obstacle is removed.

Tests: `Round202FormulaEngineTests` in `PulseOffice.Sheets.Engine.Tests`.

## Reviewed without defects

- XLOOKUP and XMATCH lift a multi-cell lookup value element by element through the shared argument lifting
  (`FunctionBuilder.Register` with `ArgumentReader.LiftablePositions`), including the not-found value.
- Spills under row and column insertion and deletion and range moves: spilled cells are never copied as constants,
  anchors move with their formula, stale spill cells are cleared by the full recalculation; undo snapshots mark
  spilled cells so the restore re-spills.
- A1# rewriting under structure edits and moves (`StructureRewriter`, `FormulaRewriter`), deletion of the anchor
  gives #REF!; XLSX `_xlfn.ANCHORARRAY` export and import with sheet-qualified and quoted names; ODS export and import.
- XLSX `cm` attribute and `metadata.xml` (`XLDAPR`, `fDynamic`) writing and reading.
- Iteration settings: XLSX `calcPr` iterate, iterateCount, iterateDelta and ODS `table:iteration` status, steps,
  minimum-difference, both read and written with the same defaults; the iteration loop stops at the maximum change
  or count.
- Structured references in XLSX: table, column, column span, `#All`, `#Data`, `#Headers`, `#Totals`, `#This Row`,
  `@` and escaped names resolve to the right ranges on reading.

## Open items

Listed under «Ledger» in docs/audit-queue.md.

## Follow-up after the cycle 12 join

- The XLOOKUP and XMATCH array-lookup tests wrote column vectors as `{2;3;7}`, which in the canonical model form is a
  row (`;` between columns, `|` between rows): the results spilled to the right and the cells below were empty. The
  tests now use `{2|3|7}`; the engine was right.
- Engine defect: clearing the obstacle of a blocked spill recalculated the anchor but not the formulas reading it
  through `A1#` (they depend on the anchor, and the spilled cells did not exist yet), so they kept #SPILL!. The
  recalculation now adds the transitive dependents of every unblocked anchor.
