using System.Globalization;
using System.Text;
using Avalonia;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Text.Rich;

public interface ITextFlowFrame
{
    Rect Box(Rect bounds, TextLayout layout);

    string Stacked(string content);
}

public sealed class TextFlowFrame : ITextFlowFrame
{
    public Rect Box(Rect bounds, TextLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Flow is not (TextFlow.Vertical or TextFlow.Vertical270)) return bounds;
        var centre = bounds.Center;
        return new Rect(centre.X - bounds.Height / 2, centre.Y - bounds.Width / 2, bounds.Height, bounds.Width);
    }

    public string Stacked(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var stacked = new StringBuilder(content.Length * 2);
        var elements = StringInfo.GetTextElementEnumerator(content);
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            if (stacked.Length > 0 && stacked[^1] != '\n' && element != "\n") stacked.Append('\n');
            stacked.Append(element);
        }
        return stacked.ToString();
    }
}
