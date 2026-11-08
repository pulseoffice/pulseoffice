using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public sealed record PresentationOptions(int StartIndex = 0, bool Loop = false, bool WithPresenter = false)
{
    public IReadOnlyList<Slide>? Playlist { get; init; }

    public bool Rehearse { get; init; }

    public bool WithoutAnimation { get; init; }

    public bool ManualAdvance { get; init; }

    public string PenColor { get; init; } = "#E11D2E";

    public int MonitorIndex { get; init; } = -1;

    public PulseOffice.Document.Forms.IFormControlInteraction? Forms { get; init; }

    public Action<Slide, SlideElement>? ControlChanged { get; init; }

    public Func<MediaElement, Task>? PlayMedia { get; init; }
}
