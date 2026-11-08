using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Documents.Editing;

namespace PulseOffice.Documents.UI.Commands;

public sealed class EnvelopePreview : Control
{
    private readonly IBrush _paper = new SolidColorBrush(Color.FromRgb(0xFB, 0xF8, 0xF1));
    private readonly IPen _edge = new Pen(new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)), 1);
    private readonly IPen _stamp = new Pen(new SolidColorBrush(Color.FromRgb(0xB9, 0xC2, 0xD0)), 1, new DashStyle([3, 2], 0));
    private readonly IBrush _ink = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));

    public EnvelopePreview()
    {
        Width = 260;
        Height = 180;
    }

    public EnvelopeRequest? Request
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    public Rect EnvelopeBounds
    {
        get
        {
            if (Request is not { } request || request.Format.WidthMillimetres <= 0 || request.Format.HeightMillimetres <= 0) return default;
            var area = new Rect(Bounds.Size).Deflate(8);
            var scale = Math.Min(area.Width / request.Format.WidthMillimetres, area.Height / request.Format.HeightMillimetres);
            var width = request.Format.WidthMillimetres * scale;
            var height = request.Format.HeightMillimetres * scale;
            return new Rect(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
        }
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Request is not { } request) return;
        var envelope = EnvelopeBounds;
        if (envelope.Width <= 0) return;
        context.DrawRectangle(_paper, _edge, envelope);
        var stamp = new Rect(envelope.Right - envelope.Width * 0.14 - 6, envelope.Y + 6, envelope.Width * 0.14, envelope.Height * 0.22);
        context.DrawRectangle(null, _stamp, stamp);
        Text(context, request.Sender, new Point(envelope.X + 8, envelope.Y + 6), envelope.Width * 0.45, 7);
        Text(context, request.Addressee, new Point(envelope.X + envelope.Width * 0.48, envelope.Y + envelope.Height * 0.5), envelope.Width * 0.48, 9);
    }

    private void Text(DrawingContext context, string text, Point origin, double width, double size)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var formatted = new FormattedText(text.ReplaceLineEndings("\n"), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, size, _ink)
        {
            MaxTextWidth = Math.Max(1, width),
            MaxLineCount = 5,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        context.DrawText(formatted, origin);
    }
}
