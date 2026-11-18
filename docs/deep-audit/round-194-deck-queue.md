# Round 194: Deck items of the audit queue

Area: the four Deck items queued by rounds 189, 190 and 193. No builds or tests were run by the agent; the coordinator
runs them after the join.

## 1. Paste while typing is its own history step

`DeckExchange` pasted plain text at the caret by writing it into the inline editor (`TypeAtCaret`), so it became part
of the typing that the editor commits as one "Edit text" step. Now pasting goes through `IDeckTextInsertion` with the
"Paste" label in every case: when typing, `InsertParagraphsAtCaret(paragraphs, labelKey)` commits the typing first
(its own step) and then records the insertion under the given label. A symbol or field inserted without a label still
joins the typing as before. `DeckTarget.InsertParagraphsAtCaret` now takes the label.

## 2. All five banding options, one shared options control

- `TableInsertChoice` and `TableInsertRequest` carry a `TableBanding` instead of a single banded-rows flag;
  `TableInsertDesigner.Banding` is settable and drives the gallery thumbnails and the preview. Folio, Ledger and Deck
  apply `choice.Banding` on insertion.
- `TableLookOptions` (`PulseOffice.UI.Controls.Tables`) is the one set of check boxes (heading row, banded rows,
  banded columns, first column, last column, total row). The shared insert dialog, the shared style gallery and the
  new shared `TableLookOptionsDialog` (options without a style, `ITableLookOptionsDialog`) all use it.
- Deck's "Table Style Options" command used its own form with four check boxes; it now opens the shared options
  dialog, and `DeckTableStyleOptions` works on `TableLookOptionsChoice` (heading row and all five banding options,
  re-deriving bold and band fills of own-look tables when row or column banding changes). Its five Deck-only
  interface texts were replaced by one shared explanation, `table.style.optionsExplanation`, in the five languages.

## 3. Media that start automatically

- Model: `MediaElement.AutoPlay`.
- Files: both formats already read and wrote media play commands as animations of the `Media` category
  (PPTX `presetClass="mediacall"` with `playFrom(0.0)`, ODP `media-call` with the play command). Research: PowerPoint
  stores "Start: Automatically" exactly as a play command at the head of the slide's main sequence, triggered after
  or with the previous item, and the ODP equivalent is the same command at the head of the main sequence, so no new
  attribute or extension is needed. `MediaAutoPlay` (`PulseOffice.Presentations`) is shared by both codecs: writers
  prepend one play command per automatic medium (the first after previous, the others with previous); readers absorb
  the leading non-click, undelayed play commands that target a medium into `AutoPlay` and remove them from the
  animations. A play command after a click stays an animation. The round-trip test of round 8 now expects the flag.
- Media options dialog: a "Start automatically when the slide appears" check box (five languages).
- Show: `ShowMediaStage.StartAutomatic` starts the visible automatic media of the slide in the built-in player when
  the slide appears (from `PresentationWindow.FreezeFront`); it never opens the system player on its own.

## 4. Picture timed by the sound

`DecodedMediaPlayback` timed audio and video by a stopwatch. `IAudioOutput.Played` now reports what the output has
played: `waveOutGetPosition` in samples on Windows, the bytes of the buffers AudioQueue has released on macOS, and on
Linux, whose pipe players expose no position, the bytes written less the pipe capacity (64 KiB) and the player's
latency, which is now requested explicitly (100 ms for paplay, pw-cat and aplay; ffplay assumed 250 ms).
`MediaTimeline` turns this into the position: once the audio has advanced it anchors to the played time and
interpolates by the wall clock between updates; before any sound it holds the start for up to 500 ms; when the audio
ends or fails it continues from the last anchor; it never runs backwards. Video without audio keeps the wall clock.

## Tests

`Round194DeckPasteStepTests`, `Round194TableLookOptionsTests`, `Round194MediaTimelineTests` (Presentations.UI.Tests),
`Round194MediaAutoPlayModelTests` (Presentations.Tests) and `Round194MediaAutoPlayTests` (Formats.OpenXml.Tests: PPTX
and ODP round trip, the PPTX command at the head of the sequence, a manual medium stays manual). All use fakes or
in-memory files; no real media are played.

## Open items

- The Linux pipe players report no device position, so the played time there is an estimate.
- Files with automatic media have not been opened in PowerPoint or the reference suite here.
