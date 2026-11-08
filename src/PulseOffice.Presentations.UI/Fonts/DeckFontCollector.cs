using PulseOffice.Typography.Embedding;

namespace PulseOffice.Presentations.UI.Fonts;

public sealed class DeckFontCollector(IPresentationFontNames names) : IUsedFontCollector
{
    public bool Handles(object model) => model is Presentation;

    public IReadOnlyCollection<string> Families(object model) => model is Presentation deck ? names.Used(deck) : [];
}
