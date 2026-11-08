namespace PulseOffice.Presentations.UI;

public sealed class ThemeMasterLookup
{
    public SlideMaster? Find(Presentation deck, Theme theme) =>
        theme == deck.Theme
            ? deck.Master
            : deck.Masters.Skip(1).FirstOrDefault(master => master.Theme == theme);
}
