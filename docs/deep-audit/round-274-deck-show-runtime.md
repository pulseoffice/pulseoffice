# Round 274: Deck slide advance, repeat, iteration, sounds and the slide show runtime (2026-11-19)

## Scope
The two open items of round 273 and the parts it did not audit: the ODP animation round trip (`OdfAnimationMapper`,
`OdfAnimationBehaviourCodec`, iterate by word and letter), PPTX iterate by word and letter, transition sounds in PPTX
and ODP, and the slide show runtime (`PresentationWindow`, `PresentationController`, `DeckShowActions`, `DeckCommands`,
`ShowPlaylist`, `ShowSetupView`): rehearse timings, loop, kiosk, hidden slides, custom shows.

## Defects
- Medium, fixed: `advClick="0"` without `advTm` read as advance on click. `AdvanceMode.TriggerOnly` is new: PPTX writes
  `advClick="0"` without a time, ODP writes `presentation:transition-type="manual"` with our
  `pulsetransition:advance-on-click="false"` (other readers see manual advance), and the transition editor offers it.
  Test: `Round274AdvanceAndRepeatTests`.
- Medium, fixed: a mouse click changed the slide even when the transition said not to advance on click (after time
  only, or trigger only). `PresentationController.ClickAdvances` lets a click reveal pending steps but not leave the
  slide; the keyboard still advances, the show setup's manual advance overrides. Test: `Round274ClickAdvanceTests`.
- Medium, fixed: rehearsing timings turned every slide into "after time only", so with the fix above clicks would no
  longer have advanced rehearsed slides. Rehearsal now keeps the click choice (`SlideTransition.WithRehearsedTime`), as
  PowerPoint does.
- Medium, fixed: repeat until next click and until end of slide had no model value. `AnimationEffect.RepeatUntil`
  (`AnimationRepeatEnd`): PPTX `repeatCount="indefinite"` with an `onNext` end condition for the next click, without one
  for the end of slide; ODP `smil:repeatCount="indefinite"` with `smil:end="next"` for the next click. The native
  carriage compares it too. The animation pane has a "Repeat until" choice. Test: `Round274AdvanceAndRepeatTests`.
- Medium, fixed: PPTX `p:iterate` with `p:tmPct` (the form PowerPoint writes for "delay between letters/words in
  percent") was read as no interval, so letters or words all started at once after an edit or in ODP. Read as the
  percentage of the iterated element's duration. Test: `Round274IterateIntervalTests`.
- Minor, fixed: ODF `anim:iterate-interval` written as an XML Schema duration (`PT0.1S`) was read as 0. Test: same.
- Serious, fixed: the slide show never played a transition sound, and a "play sound" action did nothing and advanced
  the slide instead. Transition sounds now play through the built-in media player (`IShowMediaSessions.OpenSoundAsync`,
  `ShowMediaStage.PlaySound`), looping when "loop until next sound" is set, stopped when another sound starts or the
  show ends; a sound action plays its sound and is handled. Test: `Round193MediaSessionTests` (Round274 cases).
- Medium, fixed: the show type (speaker, window, kiosk) and "show this custom show" were lost in both formats and
  absent from the model and the setup dialog. `ShowSetup.Kind` and `ShowSetup.CustomShowName`: PPTX `p:present`,
  `p:browse`, `p:kiosk`, `p:custShow`; ODP `presentation:full-screen="false"`, `presentation:show`, and our
  `pulseshow:kind="kiosk"` (with `presentation:endless`, since a kiosk loops). The kiosk loops and ignores clicks
  for advancing; the playlist follows the chosen custom show. Test: `Round274ShowSetupTests`.
- Medium, fixed: ODP lost the end of the slide range and the pen colour (declared as ODP012). Researched: ODF 1.3 has
  only `presentation:start-page`, and the reference suite keeps the pen in its view settings. They are now our
  `pulseshow:end-page` and `pulseshow:pen-color` attributes on `presentation:settings`; ODP012 is removed.
- Minor, fixed: an ODP start page was looked up by slide id only, so a named slide written by its page name was not
  found.

## Still open (queued)
- Indefinite repeats are scheduled as one cycle in the show: the effect does not keep looping until the next click or
  the end of the slide.
- Animation effect sounds and the sound attached to a non-sound action (hyperlink, go to slide) are not played in the
  show; "stop previous sound" (`p:stSnd`) has no model value and is dropped.
- ODP `presentation:play-full="false"` is read as "loop until next sound"; the two are not the same thing and the
  mapping needs checking against the reference suite.
- In a kiosk the keyboard still advances; PowerPoint allows only Esc and hyperlinks. The kiosk restart delay
  (`p:kiosk/@restart`) and the ODF pause between loops (`presentation:pause`) have no model value.
- Iterated text (by word and by letter) is drawn as a whole: the show does not reveal it word by word or letter by
  letter; `p:iterate/@backwards` is not modelled.
- From current slide on a hidden slide starts from the next visible one; PowerPoint shows the hidden slide.
