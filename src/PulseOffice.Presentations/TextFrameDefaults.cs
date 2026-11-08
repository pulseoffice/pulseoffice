using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public interface ITextFrameDefaults
{
    TextFrame For(SlideLayout layout, PlaceholderRole role);
}

public sealed class TextFrameDefaults : ITextFrameDefaults
{
    public TextFrame For(SlideLayout layout, PlaceholderRole role) => role switch
    {
        PlaceholderRole.Title when layout == SlideLayout.TitleSlide => TextFrame.Default with { Anchor = TextAnchor.Bottom },
        PlaceholderRole.Title when layout == SlideLayout.SectionHeader => TextFrame.Default with { Anchor = TextAnchor.Bottom },
        PlaceholderRole.Title => TextFrame.Default with { Anchor = TextAnchor.Middle },
        _ => TextFrame.Default,
    };
}
