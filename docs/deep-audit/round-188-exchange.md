# Round 188: cross-module Exchange

Area: copy, cut, paste, paste special and drag and drop between Folio, Ledger and Deck through `PulseOffice.Exchange`;
representation choice; the platform clipboard; protected and read-only targets; undo; list items pasted into Folio
(open item of round 183).

## Defects

1. **Medium: multi-line plain text pasted into Deck stayed one paragraph.** `DeckExchange` passed the whole clipboard
   text as one `TextRun`, so text copied from another application with several lines became a single paragraph whose
   run held line feeds (and carriage returns on Windows text), appended to the last paragraph of a selected text box or
   placed in one paragraph of a new box. The lines were not paragraphs in the model, so bullets, alignment and the
   formats' paragraph elements did not apply to them. Line endings are now normalised and each line becomes a
   paragraph through the paragraph insertion path (one undo step, as before); typing at the caret gets the normalised
   text. Test: `Round188DeckPlainTextPasteTests`.

## Reviewed without defects

- `ObjectExchange`: package recognition by id, mode negotiation, refusal of modes a participant does not offer.
- `ExchangeContentGuard`: cell range and merge validation, object size caps.
- Folio receive paths: read-only refusal, protected content netted by `FolioProtectedContentGuard`.
- List items pasted into Folio (round 183 open item): HTML and RTF readers build list threads and restart segments;
  `FragmentRemapper` gives every pasted thread a fresh name, so pasted lists never merge with the target's lists by
  name collision. Pasted items start their own list rather than joining a surrounding list; this matches the default
  of keeping source formatting and is not treated as a defect.

## Open items

- Nothing was built or run in this round: the coordinator builds and tests after the join.
- Charts with error bars and pivot links, groups and drag and drop were reviewed only at the participant level.
