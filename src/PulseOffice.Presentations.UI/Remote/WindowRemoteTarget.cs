using PulseOffice.Presentations.Remote;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI.Remote;

public sealed class WindowRemoteTarget(PresentationWindow window) : IRemoteTarget
{
    public int Position => window.SlidePosition;

    public int Count => window.SlideCount;

    public string Title => window.CurrentSlide?.Placeholder(PlaceholderRole.Title)?.Text.PlainText.Trim() ?? string.Empty;

    public string Notes => window.CurrentSlide?.Notes ?? string.Empty;

    public void Next() => window.AdvanceExternal();

    public void Previous() => window.BackExternal();

    public void First() => window.FirstExternal();

    public void Last() => window.LastExternal();
}
