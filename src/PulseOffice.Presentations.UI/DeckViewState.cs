using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI;

internal sealed class DeckViewState(Presentation deck, DeckActions actions, ITextCatalog texts, SlideView canvas, DeckViewOptions options)
{
    public Presentation Deck => deck;

    public DeckActions Actions => actions;

    public ITextCatalog Texts => texts;

    public SlideView Canvas => canvas;

    public DeckViewOptions Options => options;

    public DesignTokens.Metrics Metrics { get; } = DesignTokens.For(InterfaceDensity.Compact);

    public Avalonia.Controls.Canvas Overlay { get; } = new();

    public Slide? Current { get; set; }

    public SlideElement? Selected { get; set; }

    public TextElement? SelectedText { get; set; }

    public TextElement? Editing { get; set; }

    public DeckViewMode Mode { get; set; } = DeckViewMode.Normal;

    public string? MasterLayoutId { get; set; }

    public DeckTarget? Target { get; set; }

    public bool Syncing { get; set; }

    public bool EditorMenuOpen { get; set; }

    public SlideChrome ChromeFor(Slide? slide) =>
        slide is null ? default
        : new HeaderFooterResolver().Resolve(deck.HeaderFooter, slide, deck.IndexOf(slide) + 1, DateTime.Now, deck.MasterOf(slide).Elements, deck.TextLanguage);
}
