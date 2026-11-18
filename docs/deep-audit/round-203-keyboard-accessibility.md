# Round 203: keyboard walk of dialogs, focus visuals, themed page previews, announcements

## Scope
The Accessibility items left by round 201: keyboard-only use of the shared dialog infrastructure (`DialogHost`,
`DialogFooter`, `ChoicePromptView`) and of representative dialogs, ribbon and menu reachability, media controls and
the show keypad, the fixed white canvas of the border diagram and chart previews, focus visuals on shared controls,
and announcement of status changes.

## What already held
- `DialogHost` traps the focus (Tab from outside goes back in, focus moved outside is pulled back), Escape cancels
  unless a drop-down is open, the frame carries the dialog title as its automation name, and on close the focus goes
  back to the element that had it, or to the document.
- `DialogFooter` marks the primary button `IsDefault` and the cancel button `IsCancel`, so Enter runs the default
  action and Escape cancels; footer order is cancel on the left, actions on the right, after the content.
- The ribbon is reached with F6 (region cycle), its tab strip moves with the arrow keys, Home and End and draws its own
  focus ring; Escape inside the ribbon returns to the document. The command palette (Ctrl+Shift+P) is in the menu and
  its query box is named.
- Avalonia 12.1.2 has `AutomationProperties.LiveSetting` (`AutomationLiveSetting`), already used by toasts and some
  panels.

## Defects
- **Medium**: a dialog without a text field or list focused its frame, so a choice prompt with only footer buttons
  had nothing focused and the posted focus pass could take the focus away from the default card that
  `ChoicePromptView` had set. `DialogHost` now keeps a focus the content already placed inside the dialog, and
  otherwise tries an editable text box, then a list, toggle, number box or slider, then the default button (footer
  included), then the first button, and only then the frame. A nested dialog closing gives the focus back to the
  field of the parent that opened it instead of the parent's first field, and a collapsed dialog that expands again
  takes the focus back. `Round203DialogKeyboardTests`.
- **Medium**: the media play button and position slider were not focusable and had no automation name, so video and
  audio could not be played or sought from the keyboard. Both are now Tab stops, named (the play/pause text and the
  new `media.view.position` in the five languages), the slider seeks on arrow, Page and Home/End keys, and the control
  bar stays visible while it has the keyboard focus. `Round203MediaControlsTests`.
- **Medium**: the pivot dialog rebuilt its field list and zone chips on every change, so toggling a field with Space
  or moving a chip with Alt + arrows left the focus on a removed control. The rebuild now refocuses the matching field
  or chip. `Round203PivotKeyboardTests`.
- **Low**: the border diagram and the chart previews painted fixed white. New theme tokens `PulsePage`,
  `PulsePageInk`, `PulsePageGuide` and `PulsePageSample` keep the page look (white in the light theme, a dimmed paper
  in the dark one) and the diagram's guides and sample lines follow them. `Round203PageTokenTests`.
- **Low**: transient status bar messages (protection refusals, dialog answers turned into cancels) were not announced.
  They are now a polite live region carrying the message as their name. `Round203StatusAnnouncementTests`.
- **Low**: the colour spectrum surfaces were focusable without a focus ring; they now take the shared accent ring.
  `Round203FocusVisualTests`.

## Open items
Listed in `docs/audit-queue.md`, section Accessibility: key tips, show screen announcements, chart renderer ink in
high contrast, a live walk with a virtual display and a screen reader.
