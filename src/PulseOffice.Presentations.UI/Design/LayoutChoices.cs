using Avalonia.Controls;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Ribbon;

namespace PulseOffice.Presentations.UI.Design;

public sealed record LayoutThumbnail(Theme Theme, SlideMaster Master, SlideLayoutTemplate Layout, SlideSize Size) : RibbonThumbnail;

public sealed record LayoutChoice(string Key, SlideMaster Master, SlideLayoutTemplate Layout, string Title, string? Section);

public interface ILayoutChoices
{
    IReadOnlyList<LayoutChoice> For(Presentation deck, Slide? current);

    LayoutChoice? Resolve(Presentation deck, Slide? current, string key);

    string? KeyOf(Presentation deck, Slide slide);

    IReadOnlyList<RibbonChoice> Ribbon(Presentation deck, Slide? current);

    string Title(SlideMaster master, SlideLayoutTemplate layout);
}

public sealed class LayoutChoices(IDeckChoiceNames names, ISlideLayoutCatalog catalog) : ILayoutChoices
{
    private const char Separator = '\u001F';

    public IReadOnlyList<LayoutChoice> For(Presentation deck, Slide? current)
    {
        ArgumentNullException.ThrowIfNull(deck);
        var own = current is null ? deck.Master : deck.MasterOf(current);
        var masters = new[] { own }.Concat(deck.Masters.Where(master => !ReferenceEquals(master, own))).ToList();
        var grouped = masters.Count > 1;
        return [.. masters.SelectMany(master => master.Layouts.Select(layout =>
            new LayoutChoice(Key(master, layout), master, layout, Title(master, layout), grouped ? master.Name : null)))];
    }

    public LayoutChoice? Resolve(Presentation deck, Slide? current, string key) =>
        For(deck, current).FirstOrDefault(choice => string.Equals(choice.Key, key, StringComparison.Ordinal));

    public string? KeyOf(Presentation deck, Slide slide)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(slide);
        var master = deck.MasterOf(slide);
        var layout = master.Template(slide);
        return master.Layouts.Contains(layout) ? Key(master, layout) : null;
    }

    public IReadOnlyList<RibbonChoice> Ribbon(Presentation deck, Slide? current) =>
        [.. For(deck, current).Select(choice => new RibbonChoice(choice.Key, choice.Title)
        {
            Thumbnail = new LayoutThumbnail(ReferenceEquals(choice.Master, deck.Master) ? deck.Theme : choice.Master.Theme, choice.Master, choice.Layout, deck.Size),
            Section = choice.Section,
        })];

    public string Title(SlideMaster master, SlideLayoutTemplate layout) =>
        catalog.IsStandard(master, layout) || layout.Name.Trim().Length == 0 ? names.Layout(layout.Kind) : layout.Name;

    private string Key(SlideMaster master, SlideLayoutTemplate layout) => $"{master.Id}{Separator}{layout.Id}";
}

public interface ILayoutPreviews
{
    Control Create(Theme theme, SlideMaster master, SlideLayoutTemplate layout, SlideSize size, double width, double height);
}

public sealed class LayoutPreviews(ISlideRenderer renderer, ISlideFactory slides) : ILayoutPreviews
{
    public Control Create(Theme theme, SlideMaster master, SlideLayoutTemplate layout, SlideSize size, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var slide = slides.Create("layout-preview", layout);
        return new Themes.ThemePreview(renderer, slide, size, theme, master) { Width = width, Height = height };
    }
}

public sealed class LayoutThumbnailPainter(ILayoutPreviews previews) : IRibbonThumbnailPainter
{
    public bool Accepts(RibbonThumbnail thumbnail) => thumbnail is LayoutThumbnail;

    public Control Paint(RibbonThumbnail thumbnail, double width, double height)
    {
        var layout = (LayoutThumbnail)thumbnail;
        return previews.Create(layout.Theme, layout.Master, layout.Layout, layout.Size, width, height);
    }
}
