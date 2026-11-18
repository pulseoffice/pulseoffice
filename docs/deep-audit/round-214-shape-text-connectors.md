# Round 214: shape text layout and connector glue

Area: the round-213 shape items of the audit queue. Code was written without building or running tests; the
coordinator builds and runs the tests at the join.

## Fixed

1. **Vertical, stacked, rotated and multi-column shape text is carried.** A shared `TextLayout` record
   (`PulseOffice.Text.Rich`: flow horizontal, vertical, vertical 270 or stacked; upright; columns; column spacing) sits
   on `ShapeTextStyle` (shapes of all three modules) and on the Deck `TextFrame`, which also gained
   `RotationDegrees`. PPTX and XLSX read and write `bodyPr` `vert`, `upright`, `rot`, `numCol` and `spcCol` through
   the shared `DrawingTextLayouts`; ODP and ODS write `style:writing-mode="tb-rl"`, `style:direction="ttb"` for
   stacked text and `style:columns` with `fo:column-count`/`fo:column-gap` through the shared `OdfTextLayouts`, and
   shapes write `draw:text-rotate-angle` on their enhanced geometry. ODF has no native form for 270-degree flow,
   upright text or text box rotation: they use our `urn:pulseoffice:shape-text` extension (`text-flow`, `upright`,
   `text-rotate-angle`), which other readers ignore while still seeing vertical text; `bt-lr` writing modes from
   other producers are read as 270-degree flow. Shapes draw the flow and rotation (`TextFlowFrame` swaps the box
   for vertical flow and stacks the characters), text boxes draw flow, rotation and columns (`TextColumns` places
   the lines column by column). The shared shape format dialog and the Deck text frame dialog offer direction,
   rotation, columns and spacing in the five languages; the shape dialog applies its result as one step.
   PPTX226 and ODP217 now name only what is still approximated: East Asian and Mongolian vertical text,
   right-to-left stacked text and left-to-right vertical writing. Tests: `Round214ShapeTextLayoutTests`.
2. **Automatic connector ends stay automatic and grouped connectors keep their glue.** PowerPoint always writes a
   connection site index, so an automatic end is also marked in an `a:extLst` of `p:cNvCxnSpPr`
   (`urn:pulseoffice:connector-glue`, `automatic start/end`), read back by the shared `OoxmlAutomaticGlue`; ODP
   leaves the glue point out, which ODF readers treat as automatic. Connectors inside a group object now find
   their targets among the group's pieces by content id in both writers (`PptxGlueTargets`, and the ODP connector
   writer maps content ids to written piece ids). Tests: `Round214ConnectorGlueTests`.
3. **Ledger connectors keep their chosen sites.** XLSX writes the chosen `stCxn`/`endCxn` index (automatic ends keep
   the old bottom/top indices plus the automatic-end extension) and reads the index back; ODS writes and reads
   `draw:start-glue-point`/`draw:end-glue-point` with the standard glue points 0 to 3. Tests:
   `Round214SheetConnectorSiteTests`.
4. **ODP shrink-on-overflow no longer writes `style:shrink-to-fit`**, which ODF defines only for table cells: it
   writes `draw:fit-to-size="shrink-to-fit"` (the ODF 1.3 value) and still reads both forms.

## Open items

Listed in `docs/audit-queue.md` (Deck, round 214).
