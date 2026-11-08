using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Navigation;

namespace PulseOffice.Presentations.UI;

public sealed class DeckNavigatorSource : INavigatorSource
{
    private const string Slides = "slides";
    private const string Objects = "objects";

    private readonly IActiveDeckContext _context;
    private readonly ITextCatalog _texts;
    public DeckNavigatorSource(IActiveDeckContext context, ITextCatalog texts)
    {
        _context = context;
        _texts = texts;
        context.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<NavigatorCategory> Categories =>
        [new(Slides, _texts["deck.navigator.slides"]), new(Objects, _texts["deck.navigator.objects"])];

    public IReadOnlyList<NavigatorItem> Items(string category)
    {
        if (_context.Target is not { } target) return [];
        var deck = target.Deck;
        if (category == Slides)
            return [.. deck.Slides.Select((slide, index) => new NavigatorItem(slide.Id, (index + 1).ToString(CultureInfo.CurrentCulture) + ". " + Title(slide), 0) { Movable = true })];
        return [.. deck.Slides.SelectMany((slide, index) => slide.InZOrder().OfType<ObjectElement>()
            .Where(element => element.Content.Name is { Length: > 0 })
            .Select(element => new NavigatorItem(slide.Id + ":" + element.Id, (index + 1).ToString(CultureInfo.CurrentCulture) + " · " + element.Content.Name, 0)))];
    }

    private string Title(Slide slide) =>
        slide.Placeholder(PlaceholderRole.Title)?.Text.PlainText.Trim() is { Length: > 0 } title ? title : SlideNames.PageName(slide);

    public string? Current(string category) =>
        category == Slides && _context.Target is { } target && target.CurrentSlide() is { } slide ? slide.Id : null;

    public void Go(string category, string id)
    {
        if (_context.Target is not { } target) return;
        var slideId = category == Slides ? id : id[..Math.Max(0, id.IndexOf(':', StringComparison.Ordinal))];
        var index = target.Deck.Slides.ToList().FindIndex(slide => slide.Id == slideId);
        if (index < 0) return;
        target.GoToSlide(index);
        if (category == Objects && target.Deck.Slides[index].InZOrder().FirstOrDefault(element => slideId + ":" + element.Id == id) is { } element)
            target.Select([element]);
    }

    public bool CanMove(string category, string id, NavigatorMove move)
    {
        if (category != Slides || _context.Target is not { } target) return false;
        var index = target.Deck.Slides.ToList().FindIndex(slide => slide.Id == id);
        return move switch
        {
            NavigatorMove.Up => index > 0,
            NavigatorMove.Down => index >= 0 && index < target.Deck.Slides.Count - 1,
            _ => false,
        };
    }

    public void Move(string category, string id, NavigatorMove move)
    {
        if (!CanMove(category, id, move) || _context.Target is not { } target) return;
        var slide = target.Deck.Slides.First(candidate => candidate.Id == id);
        var index = target.Deck.IndexOf(slide);
        target.Actions.MoveSlide(slide, move == NavigatorMove.Up ? index - 1 : index + 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool CanDrop(string category, string id, string? beforeId) =>
        category == Slides && _context.Target is { } target && target.Deck.Slides.Any(slide => slide.Id == id)
        && (beforeId is null || target.Deck.Slides.Any(slide => slide.Id == beforeId));

    public void Drop(string category, string id, string? beforeId)
    {
        if (!CanDrop(category, id, beforeId) || _context.Target is not { } target) return;
        var slides = target.Deck.Slides.ToList();
        var slide = slides.First(candidate => candidate.Id == id);
        var from = slides.IndexOf(slide);
        var before = beforeId is null ? slides.Count : slides.FindIndex(candidate => candidate.Id == beforeId);
        var to = before > from ? before - 1 : before;
        if (to == from) return;
        target.Actions.MoveSlide(slide, to);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
