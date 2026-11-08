using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public sealed class ShowSlideSetup
{
    private readonly HeaderFooterResolver _headerFooter = new();
    private readonly SlideFieldsFactory _fields = new();

    public void Show(SlideView canvas, Presentation deck, Slide? slide)
    {
        canvas.DeckTheme = deck.Theme;
        canvas.Master = deck.Master;
        canvas.Size = deck.Size;
        canvas.Chrome = slide is null
            ? default
            : _headerFooter.Resolve(deck.HeaderFooter, slide, deck.IndexOf(slide) + 1, DateTime.Now, deck.MasterOf(slide).Elements, deck.TextLanguage);
        canvas.Fields = _fields.For(deck, slide);
        canvas.Show(slide);
    }
}
