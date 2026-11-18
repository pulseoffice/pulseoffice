using System.Globalization;
using System.Text;
using Avalonia;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Text.Rich;

public interface ITextFlowFrame
{
    Rect Box(Rect bounds, TextLayout layout);

    string Stacked(string content);

    bool Stacks(TextLayout layout);

    double Degrees(TextLayout layout, double textRotation, double containerRotation);

    Matrix Turn(Rect bounds, double degrees);
}

public sealed class TextFlowFrame : ITextFlowFrame
{
    public Rect Box(Rect bounds, TextLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Upright || !layout.IsVertical) return bounds;
        var centre = bounds.Center;
        return new Rect(centre.X - bounds.Height / 2, centre.Y - bounds.Width / 2, bounds.Height, bounds.Width);
    }

    public bool Stacks(TextLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return layout.IsStacked || (layout.Upright && layout.Flow != TextFlow.Horizontal);
    }

    public double Degrees(TextLayout layout, double textRotation, double containerRotation)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return layout.Upright ? textRotation : textRotation + containerRotation + layout.FlowDegrees;
    }

    public Matrix Turn(Rect bounds, double degrees)
    {
        if (Math.Abs(degrees % 360) < 0.001) return Matrix.Identity;
        var centre = bounds.Center;
        return Matrix.CreateTranslation(-centre.X, -centre.Y)
            * Matrix.CreateRotation(degrees * Math.PI / 180)
            * Matrix.CreateTranslation(centre.X, centre.Y);
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
