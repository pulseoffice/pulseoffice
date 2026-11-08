namespace PulseOffice.Presentations;

public enum ThemeOrigin
{
    Document,
    Catalog,
}

public sealed record DocumentDesign(Theme Theme, SlideMaster Master);

public sealed record ThemeChoice(Theme Theme, ThemeOrigin Origin, SlideMaster? Master = null);

public interface IThemeChoices
{
    IReadOnlyList<ThemeChoice> For(Presentation deck);

    int IndexOfCurrent(Presentation deck, IReadOnlyList<ThemeChoice> choices);

    DocumentDesign Design(Presentation deck, ThemeChoice choice);

    void Remember(Presentation deck);
}

public sealed class ThemeChoices(IThemeCatalog catalog, IMasterFactory masters) : IThemeChoices
{
    public IReadOnlyList<ThemeChoice> For(Presentation deck)
    {
        var choices = new List<ThemeChoice>();
        if (Own(deck) is { } own) choices.Add(new ThemeChoice(own.Theme, ThemeOrigin.Document, own.Master));
        choices.AddRange(catalog.Themes.Select(theme => new ThemeChoice(theme, ThemeOrigin.Catalog)));
        return choices;
    }

    public int IndexOfCurrent(Presentation deck, IReadOnlyList<ThemeChoice> choices)
    {
        var exact = choices.ToList().FindIndex(choice => choice.Theme == deck.Theme);
        return exact >= 0
            ? exact
            : choices.ToList().FindIndex(choice => string.Equals(choice.Theme.Name, deck.Theme.Name, StringComparison.OrdinalIgnoreCase));
    }

    public DocumentDesign Design(Presentation deck, ThemeChoice choice) =>
        choice is { Origin: ThemeOrigin.Document, Master: { } master }
            ? new DocumentDesign(choice.Theme, master)
            : new DocumentDesign(choice.Theme, masters.CreateStandard(choice.Theme, deck.Size) with { Id = deck.Master.Id, Elements = deck.Master.Elements });

    public void Remember(Presentation deck)
    {
        if (deck.DocumentDesign is null && catalog.Find(deck.Theme.Name) is null)
            deck.DocumentDesign = new DocumentDesign(deck.Theme, deck.Master);
    }

    private DocumentDesign? Own(Presentation deck) =>
        deck.DocumentDesign ?? (catalog.Find(deck.Theme.Name) is null ? new DocumentDesign(deck.Theme, deck.Master) : null);
}
