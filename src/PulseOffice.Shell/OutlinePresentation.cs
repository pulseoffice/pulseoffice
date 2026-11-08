namespace PulseOffice.Shell;

public sealed record OutlineItem(int Level, string Text);

public interface IOutlinePresentation
{
    bool Open(string title, IReadOnlyList<OutlineItem> items);
}
