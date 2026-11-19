using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.Presentations.UI.Rendering;

public interface ITextFrameFitting
{
    SlideElement Fit(Presentation deck, Slide slide, SlideElement element);
}

public sealed class TextFrameFitting(ITextFrameMeasure measure, ITextFrameResize resize, PulseOffice.UI.Controls.Shapes.IShapeListText shapes) : ITextFrameFitting
{
    public TextFrameFitting(ITextFrameMeasure measure, ITextFrameResize resize) : this(measure, resize, new PulseOffice.UI.Controls.Shapes.ShapeListText())
    {
    }

    private readonly SlideFieldsFactory _fields = new();

    public SlideElement Fit(Presentation deck, Slide slide, SlideElement element)
    {
        ArgumentNullException.ThrowIfNull(deck);
        return element switch
        {
            TextElement { Text.Frame.Autofit: TextAutofit.ResizeShape } text => FitText(deck, slide, text),
            ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject { Style.Text.Autofit: TextAutofit.ResizeShape } shape } placed
                when !string.IsNullOrEmpty(shape.Text) || shape.Runs is { Count: > 0 } => FitShape(deck, slide, placed, shape),
            _ => element,
        };
    }

    private SlideElement FitText(Presentation deck, Slide slide, TextElement text)
    {
        var height = measure.Height(text.Text, text.Bounds.Width.Value, deck.ThemeOf(slide), _fields.For(deck, slide));
        var bounds = resize.Fit(text.Bounds, new Twip((int)Math.Ceiling(height)), text.Text.Frame.Anchor);
        return bounds == text.Bounds ? text : text with { Bounds = bounds };
    }

    private SlideElement FitShape(Presentation deck, Slide slide, ObjectElement placed, PulseOffice.Drawing.Shapes.ShapeObject shape)
    {
        var framed = shapes.Framed(shape, 0);
        var height = measure.Height(framed, placed.Bounds.Width.Value, shapes.Theme(shape, default), _fields.For(deck, slide));
        var bounds = resize.Fit(placed.Bounds, new Twip((int)Math.Ceiling(height)), framed.Frame.Anchor);
        return bounds == placed.Bounds ? placed : placed with { Bounds = bounds };
    }
}

public sealed class NoTextFrameFitting : ITextFrameFitting
{
    public SlideElement Fit(Presentation deck, Slide slide, SlideElement element) => element;
}
