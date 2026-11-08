# Deep audit round 31: presentation format fidelity (PPTX and ODP)

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Formats.OpenXml/PptxInteractionMapper.cs | 151/151 | defect D1 (run-program and OLE-verb actions dropped silently) |
| src/PulseOffice.Formats.Odf/OdfInteractionMapper.cs | 66/66 | defect D2 (`execute` and `verb` actions dropped silently) |
| src/PulseOffice.Platform/ShellOpening.cs | 77/77 | clean: only http and https are opened |
| src/PulseOffice.Presentations.UI/PresentationWindow.cs | 440-540 | clean: playback follows only http, https, mailto and slide jumps; macros never run from a click |
| src/PulseOffice.Formats.OpenXml/PptxReader.cs | 169/169 | read losses list extended (D1) |
| src/PulseOffice.Formats.OpenXml/Presentation/Reading/PptxReadLosses.cs | 31/31 | read losses list extended (D1) |
| src/PulseOffice.Formats.Odf/OdpReader.cs | 100-243 | read losses list extended (D2) |

## Defects

| Id | Severity | Where | Description | Status |
|---|---|---|---|---|
| D1 | medium | src/PulseOffice.Formats.OpenXml/PptxInteractionMapper.cs:57 | `ppaction://program` and `ppaction://ole` click actions match no `PptxClickAction` and vanish without a diagnostic | fixed: read loss `PptxUnsupportedClickActions` (PPTX185) in `PptxReadLosses.cs`; test `PptxRoundThirtyOneAuditTests` failed before |
| D2 | medium | src/PulseOffice.Formats.Odf/OdfInteractionMapper.cs:42 | `presentation:action="execute"` and `"verb"` map to `None` and vanish without a diagnostic | fixed: `OdpUnsupportedClickActions` (ODP216) wired into `OdpReader`; test `OdpRoundThirtyOneAuditTests` failed before |

Dropping the actions is deliberate and safe (no program is ever started from a presentation); the defect was the silence.

## Security review

- Hyperlinks with `javascript:` or `file:` targets are kept in the model on read, but playback (`PresentationWindow.Follow`)
  launches only http, https and mailto, and `ShellOpener.OpenUrl` only http and https. Macros attached to click actions are
  carried for round trip and never run during a show. No consent bypass found.

## Remaining (not read this round)

Animation, transition, media, comment, table and section mappers of both formats were not re-read in this round.

## Live checks

Open a PPTX with an action button set to "Run program" and an ODP with a "Run program" interaction: the import
warning must appear and clicking the shape in the show must do nothing.
