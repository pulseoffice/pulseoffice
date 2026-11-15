# uiprobe-linux — live interface tests on Linux

The Linux counterpart of [uiprobe](../uiprobe/README.md). PulseOffice runs on an invisible **Xvfb** display (`:99`,
1600×1000 by default) with a throwaway `PULSEOFFICE_DATA_HOME` and its own D-Bus session, so it never touches the
user's desktop, mouse or keyboard, nor their preferences and recent files. **xdotool** drives it and ImageMagick
`import` captures the display; since only PulseOffice lives on that display, capturing it whole is safe.

Requires `Xvfb`, `xdotool`, ImageMagick and `dbus-run-session` (Fedora: `dnf install xorg-x11-server-Xvfb xdotool`).
The app is started from the Debug build of `src/PulseOffice.Desktop`; `start --build` builds it first.

## Usage

```bash
./uiprobe start [--build]              # display + app; the window is placed at 0,0 and sized to the display
./uiprobe shot out.png                 # capture; image pixels are display coordinates
./uiprobe click 714 540                # click (also --right, --double, --move, --scroll N, --mods ctrl,shift)
./uiprobe drag 100 300 400 500         # drag in twelve steps (also --mods)
./uiprobe type "=2+3"                  # types text
./uiprobe key Return ctrl+s Escape     # xdotool key names
./uiprobe log 80                       # tail of this run's app log
./uiprobe run -- xdotool getwindowfocus  # any command against the display
./uiprobe stop
```

Variables: `UIPROBE_DISPLAY` (default `:99`), `UIPROBE_SIZE` (default `1600x1000`), `UIPROBE_STATE` (pid files),
`UIPROBE_DATA` (reuse a data folder, for instance to restart after a simulated crash; default a new temporary one).
The app runs with `XDG_CURRENT_DESKTOP` set to a non-GNOME value, so the desktop portal uses its GTK back end and the
system file chooser appears on the virtual display; focus it with `xdotool windowfocus` before typing a path.

## Notes

- The app is started with `WAYLAND_DISPLAY` unset, so Avalonia uses X11 with its drawn decorations, as on a GNOME
  session through XWayland. There is no window manager and no compositor: the drawn shadow margin shows as a plain
  band, and the window is moved and sized by the tool instead of by a window manager.
- Every command first gives keyboard focus to the PulseOffice window, so keys never land elsewhere.
- Unlike the macOS tool, no warm-up key is needed: the first typed character reaches a freshly opened cell editor.
