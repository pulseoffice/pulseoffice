# uiprobe — live interface tests

Three tools to drive the application as a person would: clicks, drags and
typing. They are needed because **tests without an interface cannot see a whole class of
defects**, and in this project they found several that no test would have caught: the
inert title bar, modals that did not appear, the arbitrary order of the menus.

macOS only for now. The equivalent for Windows and Linux will be needed when we reach
Phase 7 of the [plan](../../docs/plan.md).

## Building

```bash
cd tools/uiprobe && ./build.sh
```

## Usage

```bash
./click 1400 500                    # click at the given screen coordinates
./click 1400 500 --move             # move only: needed for pop-up tooltips
./click 1400 500 --right            # right click: context menus
./click 1400 500 --scroll -3        # wheel: scrolling panes
./click 1400 500 --mods shift       # click with modifiers: multiple object selection
./drag 1200 450 1450 560            # drag
./keys "=2+3"                       # types the text
./keys --key 36                     # presses a key (36 = Return, 53 = Esc, 122 = F1)
./keys --key 44 --mods cmd,shift    # presses a key with modifiers
./windowshot PulseOffice out.png     # captures ONLY the main window of the process
./windowshot PulseOffice out.png --popup  # captures the open popup (context menu) of the same process
```

**Capture the window, never the screen.** `screencapture` without `-l` takes the whole screen,
including windows of the user's other applications, which must not end up in a test.
`windowshot` uses `CGWindowListCopyWindowInfo` to find the largest window of the given process
(level 0, so not tooltips or menus) and passes its number to `screencapture -l`. The
image coordinates are in points, with the same origin as the window: added to its
position they give the coordinates to pass to `click`.

**The first typed character was lost** — fixed. The event that opened the editor or entered
a freshly focused field arrived before the Unicode substitution took effect, and
the application saw the key code (virtual 0, i.e. `a`) or nothing. Now `keys`
first sends a **Shift** press, which produces no characters and wakes the event
queue. It was not an app defect: typing into an already warm field the text arrived whole.

**The first character that opens an editor became "a"** — fixed in the tool. `keys` sent every
character with virtual code 0 (the `A` key) and the Unicode string on top: when the first key opens an
editor (a Ledger cell, a Deck text box), the new field re-reads the key from the code and sees `a`
("Ricavi" became "aicavi"). Now `keys` uses `UCKeyTranslate`, on the keyboard layout in use, to find
the key (and Shift) that produces each character; code 0 remains only for characters the
layout lacks. It was not an app defect: with a real keyboard code and character match.

**No warm-up "a" and Backspace unless requested.** `keys` used to send, before every text, an "a"
followed by Backspace, to open a cell's editor: with an object selected in Deck that Backspace
**deleted the object** ("Delete object" in the history), and it looked like an app defect. Now the
warm-up is requested with `--prime` (`./keys "text" --prime`), only where it is really needed.

**Modifiers stayed pressed** — fixed in the tool. `keys --mods` set the flags only on the key event,
without pressing and releasing the modifier keys: macOS kept Shift and Command "pressed" in the
session state, and subsequent clicks and drags reached the app with those modifiers (in Deck a drag
became Shift+click and removed the object from the selection instead of moving it). Now `keys` sends
`flagsChanged` press and release events for the modifiers around the key. If it still happens, `./keys --key 56 --mods shift`
and `./keys --key 55 --mods cmd` release them.

**Do not send ⌘A to an open editor**: it is the sheet's "select all" shortcut and the
cell ends up emptied. On a new document it is not needed.

**Key codes are physical positions, not characters.** They depend on the keyboard
layout: on this Mac, with the Italian layout, code 27 (which on a US keyboard
is `-`) reaches the application as **`'`**, and `-` is at code 44. A
shortcut that seems not to work is almost always this, not an app defect: to
be sure, just log the key that actually arrives.

Coordinates are screen coordinates with origin at the top left, the same used by
`screencapture -R`. The window position is read like this:

```bash
osascript -e 'tell application "System Events" to tell process "PulseOffice" to get position of window 1'
```

## The trap that costs half an hour

**Before the press, a move event must be sent to the destination point.**
The native layer resolves the press at the *current* pointer position, not at the one
declared in the event: without the move, the click lands where the pointer was
before, usually outside the window. The effect is indistinguishable from "synthetic events
do not reach the application", which is exactly the wrong conclusion one reaches.

The tools in here already do the move. If another one is written, it must do it too.

## Known limit of text typing

`keys "text"` replaces the event text leaving the key code at zero. It works
when focus is already inside a text field, **not** for the first character that *opens* an
edit — for example the first letter typed in a cell, which starts the editor: that
path reads the key code and receives an `a`. For that case use `--code` with the
real key code.

Verified: `--code 17` followed by `--code 18` writes `t1` in a cell, while
`keys "Trimestre"` writes `arimestre`. The defect is the tool's, not the application's.

## Permissions

The process sending the events needs the **Accessibility** permission in
System Settings. To find out whether it has it:

```bash
osascript -e 'tell application "System Events" to get UI elements enabled'
```

**The light band at the top of full-screen captures is not the app's.** On macOS a full-screen window
keeps its title bar in a separate window, placed above the screen edge (`Y = -44`) and shown
only when the pointer goes up to the top; `screencapture -l` includes it and the capture is 1484 points tall on a
1440 screen. The window content starts at `Y = 0` and is black up to the edge: on screen the band is not visible.

## Before every click or key: `topmost`

```bash
./topmost <pid> <x> <y>    # 0 if process pid is frontmost and its window is on top at the point
```

Live tests are done only on **our own** instance (throwaway data folder, started by the test) and a click or a
key is sent only if `topmost` answers 0: the frontmost app (`NSWorkspace.frontmostApplication`) is that of the pid, and
in the z-ordered window list (`CGWindowListCopyWindowInfo`, the first that contains the point) the topmost window
at the click point belongs to the same pid (the system's transparent layers above the screen — Dock, Window
Server, Control Center — do not count: they always cover everything and do not receive the click). It exits with 3 if another app is frontmost, with 4 if at the point there is
someone else's window; in those cases no clicks or keys. For keys a point on the title bar of our window
is checked.

**If the app does not come to the front.** macOS 26/27 restricts "cooperative" activation: an app does not take the
front if the active one does not yield it, and System Events' `set frontmost` or `NSRunningApplication.activate` may be
ignored. In the twenty-fifth round the instance started from the command line came to the front by itself (no other
app was holding it); if that does not happen, the only allowed gesture is a first click on the **title bar of our
window**, and only if `topmost <pid> <x> <y> --ignore-front` confirms that at that point the topmost window
is ours.
