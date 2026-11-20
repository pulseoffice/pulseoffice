# Round 337: shortcut editor and keyboard shortcuts

Area: the shortcut editor, the object nudge commands, and a fresh pass over keyboard shortcuts in the three modules
(defaults against Word, Excel and PowerPoint, Ctrl and Cmd mapping, global against module bindings, menus and tooltips,
the customization dialog, key tips). No builds or tests were run in the round, per the audit cycle rule.

## Defects found

Serious
- Ctrl+N (Cmd+N on macOS) did nothing: the three "New … document" commands had no gesture, and no command answered the
  key every office application uses for a new document. Fixed: `shell.file.newBlank` (File › New, Ctrl+N) creates a
  blank document of the module in use, Folio from the welcome page.

Medium
- The object nudge commands (move, fine move, resize, fine resize in four directions, per module) were listed in the
  shortcut editor but `CommandRegistry.Rebind` refused every view-routed command, so an edit was accepted by the dialog,
  dropped on Apply without a word, and the views kept reading fixed arrow keys from `ObjectNudgeKeys`. Fixed: rebinding
  is allowed for view-routed commands (they are still never dispatched by the shell); `ObjectNudgeKeys` now reads the
  bound gesture per module through `IObjectNudgeBindings` (`RegistryObjectNudgeBindings` in the app,
  `FactoryObjectNudgeBindings` standalone), with Alt, Control or Super standing for the primary modifier as a fine-step
  alias, as before. Ledger and Deck receive the injected reader (they built their own); `IObjectNudgeCommands.IdOf`
  owns the module prefixes.
- The shortcut editor's Apply ignored refused edits. `IShortcutCustomization.Apply` now returns the ids it could not
  apply (unknown command or unreadable gesture), does not save them, and the status bar says how many were changed and
  how many were not and why (`customize.refused`, five languages).
- Find and Replace keys differed by module: Folio had Ctrl+F for the find pane and Ctrl+Shift+H for Replace, Ledger and
  Deck had Ctrl+F on the Replace dialog and no key for the find pane. All three now use Ctrl+F for Find and
  Ctrl+Shift+H for Replace.

Minor (queued)
- No export or import of shortcut customizations.
- Replace on Ctrl+Shift+H rather than Office's Ctrl+H (Control+H on macOS, where Cmd+H is reserved).
- Ledger Ctrl+Plus and Ctrl+Minus zoom instead of inserting and deleting cells; Ctrl+Shift+A sorts.
- Several Office defaults not found among command defaults (listed in the queue).
- Nudges can only be reassigned to chords with a command modifier.

## Checked without defects
- Menus and ribbon tooltips show the current binding (`DefaultGesture` after `Rebind`; surface trees are keyed on the
  registry version) in the system notation (⌘ on macOS).
- Global against module bindings: the module command wins in `ActiveGestures`, and `ShortcutAudit` plus the existing
  test that no factory gesture is reserved on either system hold.
- The editor's search by name and by key, restore one and restore all, conflict warnings with reassign, needs-modifier
  and reserved-key messages.
- Key tips: letters from the titles, spare letters, then `0`-prefixed pairs; `0` is never a single tip.

## Tests
- `Round337NudgeRebindingTests` (UI.Controls): a rebound and a removed nudge in Ledger, Deck untouched, Alt alias.
- `Round332ViewRoutedGestureTests` now expects rebinding to succeed and dispatch to stay off.
- `ShortcutEditorLogicTests.Round337_an_edit_that_cannot_be_applied_is_reported_and_not_saved`.
- `Round337ShortcutDefaultsTests` (Desktop, headless collection): Ctrl+N, find and replace keys in the three modules,
  and a rebound Deck nudge read through the app's injected reader.
