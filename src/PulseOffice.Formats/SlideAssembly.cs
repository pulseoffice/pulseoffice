using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats;

public sealed class SlideAssembly<TFrameKey>(Slide slide, IReadOnlyList<PlaceholderRole> roles)
    where TFrameKey : notnull
{
    private readonly Dictionary<TFrameKey, string> _elementByFrame = [];

    public Slide Slide => slide;

    public IReadOnlyList<PlaceholderRole> Roles => roles;

    public IReadOnlyDictionary<TFrameKey, string> ElementByFrame => _elementByFrame;

    public int Order { get; private set; }

    public string NextId(string kind) => $"{slide.Id}.{kind}{Order}";

    public void Add(SlideElement element)
    {
        slide.Add(element);
        Order++;
    }

    public void Remember(TFrameKey? frame, string elementId)
    {
        if (frame is not null) _elementByFrame[frame] = elementId;
    }
}
