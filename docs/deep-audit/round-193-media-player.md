# Round 193: in-app media player for the slide show

Area: the open item of rounds 173 and 187. The slide show handed every audio and video to the system player, so the
media options (repeat, volume, mute, trim from start and end, fade in and out) saved in the file were ignored in the
show. This round adds a built-in player behind a new `PulseOffice.Platform` interface.
No builds or tests were run by the agent; the coordinator runs them after the join.

## Design

- `IMediaPlayback` (`PulseOffice.Platform.Media`): open a `MediaSource` (a file, or a stream with its extension, which
  the backend spills to a private temporary folder), play, pause, stop, seek, live volume, position and duration,
  `PositionChanged` (every 40 ms while playing), `Ended`, and `FrameReady` with video frames as BGRA `MediaFrame`s.
  Audio is played by the platform. `IMediaPlaybackFactory` says whether a backend exists and creates players;
  `UnavailableMediaPlaybackFactory` is the empty one.
- The core timing logic is platform-free: `MediaVolumeCurve` turns the trims into a playback window (an impossible
  trim keeps the whole medium) and computes the gain at a position (volume, mute, linear fade-in ramp from the window
  start, fade-out ramp to the window end). `MediaSession` drives an `IMediaPlayback` with `MediaPlaybackSettings`: start
  seeks to the trim start, every position update sets the volume from the curve, reaching the window end (or the
  backend's end) seeks back to the trim start when looping or pauses and raises `Finished` once; play/pause toggles,
  and after the end it starts again; the seek bar maps onto the trimmed window.
- `DecodedMediaPlayback` is the one player: an `IMediaDecoder` (probe, an audio stream of 16-bit stereo 48 kHz PCM, a
  video stream of BGRA frames, both opened at a position) and an `IAudioOutputFactory`. The clock is a stopwatch from
  the seek origin; the audio thread scales samples by the current volume (so fades and mute are sample-exact and need
  no platform mixer) and writes to the output, which blocks when its buffers are full; the video thread presents each
  frame when the clock reaches its time. Seeking and pausing restart the streams at the new origin.
- `MediaView` (`PulseOffice.UI.Controls.Objects`, shared) draws the frames into a `WriteableBitmap` (only the latest
  pending frame is presented, so a slow UI thread drops frames rather than queueing them) with a minimal bar: a
  play/pause button and a seek slider over the trimmed window. Over video the bar shows on hover; for audio it stays.
  Clicking the picture toggles play/pause.
- Deck: `IShowMediaSessions` (`ShowMediaSessions`) builds a session from a `MediaElement` (embedded data, or a linked
  file that exists) with its options. `ShowMediaStage` keeps the playing media of the current slide on a layer above
  the slide, placed on the element's bounds; clicking a medium starts it, clicking it again toggles it, changing slide
  or closing the show disposes the players. When no backend exists, the file cannot be decoded, or the medium is a
  web address, blocked or missing, the show falls back to `ShowMediaPlayer` (system player, with the existing
  warnings). `MediaFileExtensions` is shared by both paths.
- The media options dialog explanation now says the options are applied by the show's built-in player, and that only
  the system-player fallback ignores them (five languages).

## Backends

| System | Decoder | Audio output | Status |
|---|---|---|---|
| Linux | `FfmpegMediaDecoder`: `ffprobe` for duration and size, two `ffmpeg` processes piping raw `s16le` PCM and `bgra` frames (25 fps, at most 1280 px wide) | `ProcessAudioOutputFactory`: the first of `paplay`, `pw-cat`, `aplay`, `ffplay` found, fed raw PCM on stdin | written; not run live in this round |
| Windows | `MediaFoundationMediaDecoder`: Media Foundation source readers (Vortice, already a dependency), RGB32 with video processing and PCM output types | `WaveOutAudioOutput`: winmm `waveOut` with four rotating buffers | written; not verified on a real Windows |
| macOS | `AvFoundationMediaDecoder`: `AVAssetReader` track outputs (BGRA pixel buffers, linear PCM) through the Objective-C runtime, as the other macOS interop does; falls back to the ffmpeg decoder when AVFoundation cannot be loaded | `AudioQueueOutput`: AudioToolbox output queue with four buffers | written; not verified on a real Mac |

The backend is chosen once by `MediaPlaybackProvider` (Desktop composition) from `IHostSystem`, as the video export
encoder is. No new package or native binding was added.

## Tests

`Round193MediaSessionTests` (Presentations.UI.Tests), with a fake playback and no real media: trim window and the
impossible trim, the fade ramps and mute as a volume curve, start at the trim start at the faded-in volume, stop once
at the trim end without loop, seek back with loop, toggle after the end, seek bar mapping, an embedded medium opened
with its options, and the fallback when there is no backend or the medium cannot be opened.

## Open items

- The model has no "start automatically" media setting (the PPTX media timing command and the ODP auto-play flag are
  not read), so media start on click only.
- Audio and video are paced by the wall clock, not by the audio device position: on Linux the pipe and sound server
  buffers can put audio a few tenths of a second behind the picture.
- Live verification of the three backends, above all Windows and macOS, which cannot run on this Linux machine.
