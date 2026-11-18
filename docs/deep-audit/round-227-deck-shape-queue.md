# Round 227: Deck shape queue

Scope: the Deck code items of the audit queue left by rounds 218-225. No build or test was run in this round (the
coordinator builds and tests after the join).

## Done

1. **Connector Ends in Deck.** `IDeckConnectors.Resite` changes the start and end sites of a connector and reroutes it
   against its glued objects (unglued ends keep their points). The new `deck.connector.sites` command, enabled when a
   single connector is selected, asks with the shared `IConnectorSitesDialog` (the same one Ledger uses) and applies
   the result through `DeckActions.ReplaceElement`, one undo step labelled with `history.connector.sites`. Title and
   description in the five languages; menu placement through the existing `deck.connector.` prefix (Insert, Draw);
   icon shared with Ledger's. Tests: `Round227DeckConnectorSitesTests`.
2. **ODF form for the 270-degree flow.** ODF 1.3 defines `style:writing-mode` on graphic properties only with
   `lr-tb`, `rl-tb`, `tb-rl`, `tb-lr`, `lr`, `rl`, `tb` and `page`; there is no bottom-to-top value. The extension
   namespace already read by the reference suite carries `writing-mode="bt-lr"`, the value proposed for the next ODF
   revision, so the writer now emits it beside `style:writing-mode="tb-rl"` (readers that ignore it see plain vertical
   text) instead of our `text-flow="vertical-270"`; the reader prefers the extension writing mode and still reads the
   old value. Upright text and text box rotation have no ODF form in 1.3 nor in the extension namespace, so they keep
   `urn:pulseoffice:shape-text`. Tests: `Round227OdfFlowFormsTests`.

## Not done (still queued)

- East Asian characters upright in East Asian vertical text: needs a per-character composition in
  `TextFrameComposer` (upright CJK clusters stacked, Latin runs rotated), with caret and hit-testing to match; PPTX226
  and ODP217 still fire.
- In-place editing of stacked text (caret, selection, up and down keys over stacked glyphs and mirrored Mongolian
  lines) in the three modules.
