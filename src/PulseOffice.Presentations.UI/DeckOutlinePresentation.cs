using PulseOffice.Shell;

namespace PulseOffice.Presentations.UI;

public sealed class DeckOutlinePresentation(
    IThemeCatalog themes,
    IMasterFactory masters,
    IOutlineDeckBuilder builder,
    IDeckDocumentFactory documents,
    IDocumentWorkspace workspace) : IOutlinePresentation
{
    public bool Open(string title, IReadOnlyList<OutlineItem> items)
    {
        if (items.Count == 0) return false;
        var theme = themes.Default;
        var deck = new Presentation { Theme = theme, Master = masters.CreateStandard(theme) };
        builder.Fill(deck, [.. items.Select(item => new OutlineLine(item.Level, item.Text))]);
        if (deck.Slides.Count == 0) return false;
        workspace.Open(documents.CreateFrom(deck, title));
        return true;
    }
}
