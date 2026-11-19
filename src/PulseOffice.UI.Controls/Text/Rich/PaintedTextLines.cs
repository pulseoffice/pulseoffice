namespace PulseOffice.UI.Controls.Text.Rich;

public interface IPaintedTextLines
{
    void Painted(IReadOnlyList<RichTextLineBox> lines);
}

public sealed class PaintedTextLineLog : IPaintedTextLines
{
    private readonly List<RichTextLineBox> _lines = [];

    public IReadOnlyList<RichTextLineBox> Lines => _lines;

    public void Painted(IReadOnlyList<RichTextLineBox> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _lines.AddRange(lines);
    }

    public void Clear() => _lines.Clear();
}
