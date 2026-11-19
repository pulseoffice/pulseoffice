using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.Presentations.UI.Rendering;

public interface ITextFrameFitting
{
    SlideElement Fit(Presentation deck, Slide slide, SlideElement element);
}

public sealed class TextFrameFitting(ITextFrameMeasure measure, ITextFrameResize resize, PulseOffice.UI.Controls.Shapes.IShapeTextFit shapes, ITextFrameComposer composer) : ITextFrameFitting
{
    private const double ScaleTolerance = 0.005;

    public TextFrameFitting(ITextFrameMeasure measure, ITextFrameResize resize, PulseOffice.UI.Controls.Shapes.IShapeTextFit shapes)
        : this(measure, resize, shapes, new TextFrameComposer(new ListLabels()))
    {
    }

    public TextFrameFitting(ITextFrameMeasure measure, ITextFrameResize resize) : this(measure, resize, new PulseOffice.UI.Controls.Shapes.ShapeTextFit(measure, new PulseOffice.UI.Controls.Shapes.ShapeListText(), new PulseOffice.Drawing.Shapes.ShapeTextArea()), new TextFrameComposer(new ListLabels()))
    {
    }

    private readonly SlideFieldsFactory _fields = new();

    public SlideElement Fit(Presentation deck, Slide slide, SlideElement element)
    {
        ArgumentNullException.ThrowIfNull(deck);
        return element switch
        {
            TextElement { Text.Frame.Autofit: TextAutofit.ResizeShape } text => FitText(deck, slide, text),
            TextElement { Text.Frame.Autofit: TextAutofit.ShrinkText } text => ShrinkText(deck, slide, text),
            ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject shape } placed when shapes.Resizes(shape) => FitShape(deck, slide, placed, shape),
            _ => element,
        };
    }

    private SlideElement FitText(Presentation deck, Slide slide, TextElement text)
    {
        var height = measure.Height(text.Text, text.Bounds.Width.Value, deck.ThemeOf(slide), _fields.For(deck, slide));
        var bounds = resize.Fit(text.Bounds, new Twip((int)Math.Ceiling(height)), text.Text.Frame.Anchor);
        return bounds == text.Bounds ? text : text with { Bounds = bounds };
    }

    private SlideElement ShrinkText(Presentation deck, Slide slide, TextElement text)
    {
        var bounds = new Avalonia.Rect(0, 0, Math.Max(1, text.Bounds.Width.Value), Math.Max(1, text.Bounds.Height.Value));
        var factor = composer.Compose(text.Text, bounds, deck.ThemeOf(slide), _fields.For(deck, slide), 1.0).FontFactor;
        var frame = text.Text.Frame;
        if (Math.Abs(frame.FontScale - factor) < ScaleTolerance && frame.LineReduction == 0) return text;
        return text with { Text = text.Text with { Frame = frame with { FontScale = Math.Round(factor, 3), LineReduction = 0 } } };
    }

    private SlideElement FitShape(Presentation deck, Slide slide, ObjectElement placed, PulseOffice.Drawing.Shapes.ShapeObject shape)
    {
        if (shapes.Size(shape, new DocumentSize(placed.Bounds.Width, placed.Bounds.Height), _fields.For(deck, slide)) is not { } size) return placed;
        if (size.Width != placed.Bounds.Width) return placed with { Bounds = placed.Bounds with { Width = size.Width } };
        var height = size.Height;
        var anchor = shape.Style.Text.Vertical switch
        {
            PulseOffice.Drawing.Shapes.TextVerticalPlacement.Top => TextAnchor.Top,
            PulseOffice.Drawing.Shapes.TextVerticalPlacement.Bottom => TextAnchor.Bottom,
            _ => TextAnchor.Middle,
        };
        var bounds = resize.Fit(placed.Bounds, height, anchor);
        return bounds == placed.Bounds ? placed : placed with { Bounds = bounds };
    }
}

public sealed class NoTextFrameFitting : ITextFrameFitting
{
    public SlideElement Fit(Presentation deck, Slide slide, SlideElement element) => element;
}
