using PulseOffice.UI.Embedding;
using Avalonia;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.Presentations.UI.Rendering;

public sealed record SlideScene(
    DrawingContext Context, Rect Bounds, double Scale, Theme Theme, SlidePalette Palette, TextFields Fields)
{
    public PulseOffice.UI.Controls.Shapes.ShapeSurroundings Backdrop { get; init; } = new();
}

public sealed class MediaElementPainter
{
    private readonly IBrush _screen = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromArgb(255, 24, 24, 28));
    private readonly IBrush _glyph = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromArgb(235, 245, 245, 250));

    public void Paint(SlideScene scene, MediaElement media)
    {
        var (context, bounds) = (scene.Context, scene.Bounds);
        context.FillRectangle(_screen, bounds);
        context.DrawRectangle(null, new Avalonia.Media.Immutable.ImmutablePen(scene.Palette.Border.ToUInt32(), 1), bounds);

        var side = Math.Min(bounds.Width, bounds.Height) * 0.32;
        var centre = bounds.Center;
        var glyph = _glyph;

        if (media.Kind == MediaKind.Video)
        {
            var geometry = new StreamGeometry();
            using (var figure = geometry.Open())
            {
                figure.BeginFigure(new Point(centre.X - side * 0.5, centre.Y - side * 0.6), true);
                figure.LineTo(new Point(centre.X - side * 0.5, centre.Y + side * 0.6));
                figure.LineTo(new Point(centre.X + side * 0.7, centre.Y));
                figure.EndFigure(true);
            }
            context.DrawGeometry(glyph, null, geometry);
            return;
        }

        context.FillRectangle(glyph, new Rect(centre.X - side * 0.15, centre.Y - side * 0.7, side * 0.3, side * 1.1));
        context.DrawEllipse(glyph, null, new Point(centre.X - side * 0.3, centre.Y + side * 0.4), side * 0.35, side * 0.28);
    }
}

public sealed class TextElementPainter(ITextCatalog texts, IRichTextFramePainter frames)
{
    private readonly TextFrameGeometry _geometry = new();

    public TextElementPainter(ITextCatalog texts, IListLabels labels)
        : this(texts, new RichTextFramePainter(new TextFrameComposer(labels)))
    {
    }

    public void Paint(SlideScene scene, TextElement element)
    {
        var frame = element.Text.Frame;
        if (element.Text.IsEmpty)
        {
            if (scene.Palette.Live?.Id != element.Id) PaintPrompt(scene, element, _geometry.Area(scene.Bounds, frame, scene.Scale), frame);
            return;
        }

        frames.Paint(new RichTextScene(scene.Context, scene.Bounds, scene.Scale, scene.Theme, scene.Fields, scene.Palette.Proofing), element.Text);
    }

    private void PaintPrompt(SlideScene scene, TextElement element, TextFrameArea area, TextFrame frame)
    {
        var paragraph = element.Text.Paragraphs.Count > 0 ? element.Text.Paragraphs[0] : new TextParagraph(string.Empty);
        var typesetter = new ParagraphTypesetter(scene.Theme, scene.Scale);
        var formatted = typesetter.Typeset(PromptFor(element.Text.Role), paragraph, scene.Palette.Placeholder, area.Inner.Width, System.Globalization.CultureInfo.CurrentUICulture);
        var free = area.Inner.Height - formatted.Height;
        var y = area.Inner.Y + frame.Anchor switch
        {
            TextAnchor.Middle or TextAnchor.Justified => free / 2,
            TextAnchor.Bottom => free,
            _ => 0,
        };
        scene.Context.DrawText(formatted, new Point(area.Inner.X, y));
    }

    private string PromptFor(PlaceholderRole role) => role switch
    {
        PlaceholderRole.Title => texts["deck.placeholder.title"],
        PlaceholderRole.Subtitle => texts["deck.placeholder.subtitle"],
        PlaceholderRole.Body => texts["deck.placeholder.body"],
        _ => string.Empty,
    };
}

public sealed class ElementPaintVisitor(
    SlideScene scene, TextElementPainter text, MediaElementPainter media, IContentPainter content)
    : ISlideElementVisitor<bool>
{
    public bool VisitText(TextElement element)
    {
        if (element.Backdrop is { } backdrop && backdrop.AsDrawing(element.Id) is var shape && content.CanPaint(shape))
            content.Paint(scene.Context, shape, scene.Bounds, scene.Scale * PulseOffice.Foundation.Units.Twip.PerPoint, scene.Backdrop);
        text.Paint(scene, element);
        return true;
    }

    public bool VisitObject(ObjectElement element)
    {
        if (!content.CanPaint(element.Content)) return false;
        content.Paint(scene.Context, element.Content, scene.Bounds, scene.Scale * PulseOffice.Foundation.Units.Twip.PerPoint, scene.Backdrop);
        return true;
    }

    public bool VisitMedia(MediaElement element)
    {
        media.Paint(scene, element);
        return true;
    }
}
