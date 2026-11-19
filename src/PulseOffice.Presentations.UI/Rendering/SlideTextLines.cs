using Avalonia;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.Presentations.UI.Rendering;

public interface ISlideTextLines
{
    IReadOnlyList<RichTextLineBox> For(Presentation deck, Slide slide, SlideElement element, Rect bounds, double scale);
}

public sealed class SlideTextLines(ITextFrameComposer composer, IRichTextLineBoxes boxes) : ISlideTextLines
{
    public SlideTextLines() : this(new TextFrameComposer(new ListLabels()), new RichTextLineBoxes())
    {
    }

    public IReadOnlyList<RichTextLineBox> For(Presentation deck, Slide slide, SlideElement element, Rect bounds, double scale)
    {
        ArgumentNullException.ThrowIfNull(deck);
        if (element is not TextElement { Text: var text } || text.IsEmpty || bounds.Width <= 0) return [];
        return boxes.Of(text, composer.Compose(text, bounds, deck.ThemeOf(slide), new SlideFieldsFactory().For(deck, slide), scale));
    }
}
