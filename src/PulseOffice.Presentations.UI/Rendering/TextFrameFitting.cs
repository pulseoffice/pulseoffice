using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.Presentations.UI.Rendering;

public interface ITextFrameFitting
{
    SlideElement Fit(Presentation deck, Slide slide, SlideElement element);
}

public sealed class TextFrameFitting(ITextFrameMeasure measure, ITextFrameResize resize) : ITextFrameFitting
{
    private readonly SlideFieldsFactory _fields = new();

    public SlideElement Fit(Presentation deck, Slide slide, SlideElement element)
    {
        ArgumentNullException.ThrowIfNull(deck);
        if (element is not TextElement { Text.Frame.Autofit: TextAutofit.ResizeShape } text) return element;

        var height = measure.Height(text.Text, text.Bounds.Width.Value, deck.ThemeOf(slide), _fields.For(deck, slide));
        var bounds = resize.Fit(text.Bounds, new Twip((int)Math.Ceiling(height)), text.Text.Frame.Anchor);
        return bounds == text.Bounds ? element : text with { Bounds = bounds };
    }
}

public sealed class NoTextFrameFitting : ITextFrameFitting
{
    public SlideElement Fit(Presentation deck, Slide slide, SlideElement element) => element;
}
