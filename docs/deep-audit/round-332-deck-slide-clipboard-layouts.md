# Round 332 (2026-11-20): Deck slide clipboard, layout names and further layouts, object arrow keys as commands

Area: the Deck (329) queue items: the slide clipboard, PPTX standard layout names, the decoration of further layouts
of a type in ODP (ODP107), and the arrow keys on objects that were not commands. No builds or tests were run in the
round (audit cycle rule).

## Defects found

| # | Severity | Defect | Fix |
|---|---|---|---|
| 1 | Medium | Copy and Cut were disabled in the slide sorter unless an object was selected, so the slide clipboard of round 329 could only run from the palette. | Copy and Cut are enabled when the view copies slides (`DeckTarget.CopiesSlides`). |
| 2 | Medium | Only the current slide could be copied, only in the sorter; no multi-slide selection, no copy from the normal view's filmstrip. | `SlideSelection` (Ctrl or Cmd click toggles, Shift click extends from the anchor, plain click collapses), held by the view state, used by the sorter and the filmstrip and highlighted on the frames; `DeckTarget.SelectedSlides` and `SlidePaneFocused`; with the filmstrip focused and no object selected Ctrl+C/X copy the selected slides. Cutting several slides is one undo step (`DeckActions.DeleteSlides`). The slide part of the exchange moved to the injectable `IDeckSlideClipboard`. Tests: `Round332SlideClipboardTests`. |
| 3 | Medium | Slides pasted into another presentation always took the destination theme; PowerPoint offers "Keep source formatting". | New `ExchangePasteMode.KeepSourceFormatting` (translated in the five languages): slides from another deck offer "As in the original" (destination theme, the default paste) and "Keep source formatting" in Paste Special, which adopts the source masters as Insert Slides from File does. Test: `Round332SlideClipboardTests`. |
| 4 | Medium | PPTX standard layouts lost their PowerPoint name: the reader replaced it with our standard name and the writer wrote the type name. | `SlideLayoutTemplate.HasOwnName` (a name other than the standard key or type name); the reader keeps `p:cSld/@name` of every layout, the writer writes it whenever the layout has its own name, the layout gallery shows it instead of the localized standard name. Test: `Round332PptxLayoutNameTests`. |
| 5 | Medium | ODP: the decoration of a further layout of a type came back as that of the first layout of its type (ODP107). | The further layout's own decoration is written inside its `layouts` catalog entry (same shape, image and SVG writers as master pages, marked `own-decoration`) and read back from there; ODP107 now only says that other applications show those layouts as the first of their type. Test: `Round332OdpFurtherLayoutDecorationTests`. |
| 6 | Minor | The arrow keys on objects (move, fine move, resize, fine resize) were not commands and appeared in no in-app shortcut list in Ledger, Deck or Folio. | Shared `IObjectNudgeCommands` builds sixteen palette commands per module from one table; `ObjectNudgeCommandSets` (Desktop) wires them to new nudge hooks on `SheetTarget`/`IActiveSheetContext`, `DeckTarget` and `FolioTarget`. `CommandDefinition.GestureRoutedByView` keeps them listed but never dispatched by the shell nor rebindable, so the view keeps owning the keys. Cmd counts as a fine modifier on macOS so the listed gesture is right. Tests: `Round332ObjectNudgeCommandsTests`, `Round332ViewRoutedGestureTests`, `SharedObjectViewTests`. |

## Research

PowerPoint pastes slides with the destination theme by default and offers "Keep Source Formatting" in the paste options;
within one presentation both give the same result, so only one mode is offered there. PowerPoint stores the visible
layout name in `p:cSld/@name` of every layout, including standard ones ("Title and Content"), and the reference suite
reads it as the layout name too. ODF 1.3 has one page layout per presentation layout type and no way to name further
layouts, so the full layout stays in our `urn:pulseoffice:presentation-master` extension, which other readers ignore.

## Still open (queued)

- Nothing of this round has been seen in the running app (slide selection, highlight, filmstrip clipboard, Paste
  Special mode, nudge commands).
- No floating paste options button after a slide paste; no keyboard extension of the slide selection; dragging moves
  only one slide.
- The selection highlight uses the `Pressed` token; not checked in high contrast.
- PowerPoint names of standard layouts are shown as written (an English name stays English in another interface
  language), as in PowerPoint; not verified against PowerPoint.
- The nudge commands cannot be reassigned and the shortcut editor does not explain why.
