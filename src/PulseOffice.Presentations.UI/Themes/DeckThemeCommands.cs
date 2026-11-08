using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI.Themes;

public sealed class DeckThemeCommands(
    IActiveDeckContext context,
    IThemeChoices choices,
    IThemeNames names,
    IThemePreviews previews,
    IMasterFactory masters,
    IDialogService dialogs,
    PulseOffice.UI.Controls.Fonts.IFontPickerFactory fonts,
    ITextCatalog texts,
    PulseOffice.UI.Controls.Galleries.IGalleryNavigator navigator,
    ThemeMasterLookup lookup) : DeckCommandSet(context)
{
    private readonly Dictionary<(Theme, SlideSize), SlideMaster> _masters = [];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("deck.design.themeDialog") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => DialogAsync(ct),
        },

        Prototype("deck.design.themes") with
        {
            Surface = CommandSurface.Ribbon | CommandSurface.Palette,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            RibbonControl = new ChoiceSpec(Gallery, Current)
            {
                Look = RibbonChoiceLook.Band,
                Band = new RibbonBandSpec(1, 6, 16.0 / 9.0) { PopoverTileWidth = 176, PopoverColumns = 4, Proportion = Proportion },
                MoreCommandId = "deck.design.themeDialog",
            },
            Execute = (ctx, ct) => ctx.Argument is string key ? Apply(key) : DialogAsync(ct),
        },
    ];

    private IReadOnlyList<RibbonChoice> Gallery()
    {
        if (Context.Target is not { } t) return [];
        return [.. choices.For(t.Deck).Select((choice, index) => new RibbonChoice(Key(choice, index), names.Display(choice))
        {
            Thumbnail = new ThemeThumbnail(choice.Theme, MasterOf(t.Deck, choice), t.Deck.Size),
            Section = texts[choice.Origin == ThemeOrigin.Document ? "deck.theme.inDocument" : "deck.theme.catalog"],
        })];
    }

    private SlideMaster MasterOf(Presentation deck, ThemeChoice choice)
    {
        if (choice.Origin == ThemeOrigin.Document) return choices.Design(deck, choice).Master;
        if (!_masters.TryGetValue((choice.Theme, deck.Size), out var master))
            _masters[(choice.Theme, deck.Size)] = master = choices.Design(deck, choice).Master;
        return master;
    }

    private double Proportion() =>
        Context.Target is { Deck.Size: var size } && size.Height.Value > 0 ? (double)size.Width.Value / size.Height.Value : 16.0 / 9.0;

    private string? Current()
    {
        if (Context.Target is not { } t) return null;
        var options = choices.For(t.Deck);
        var index = choices.IndexOfCurrent(t.Deck, options);
        return index >= 0 ? Key(options[index], index) : null;
    }

    private string Key(ThemeChoice choice, int index) =>
        choice.Origin == ThemeOrigin.Document ? $"document:{index}" : choice.Theme.Name;

    private Task Apply(string key)
    {
        if (Context.Target is not { } t) return Task.CompletedTask;
        var options = choices.For(t.Deck);
        var chosen = options.Select((choice, index) => (choice, index)).FirstOrDefault(entry => Key(entry.choice, entry.index) == key).choice;
        if (chosen is null || chosen.Theme == t.Deck.Theme) return Task.CompletedTask;

        choices.Remember(t.Deck);
        var design = choices.Design(t.Deck, chosen);
        t.Actions.ApplyTheme(design.Theme, design.Master);
        return Task.CompletedTask;
    }

    private async Task DialogAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t) return;
        var current = t.Deck.Theme;
        var opened = t.CurrentSlide();
        var result = await dialogs.ShowAsync<ThemeDialogChoice>(
            new ThemeDialogView(t.Deck, new ThemeDialogServices(choices, names, previews, fonts, texts, navigator)),
            new DialogOptions { TitleKey = "command.deck.design.themeDialog.title", PreferredWidth = 960, PreferredHeight = 640 }, ct).ConfigureAwait(true);
        if (result is null) return;

        var design = choices.Design(t.Deck, result.Choice);
        var theme = design.Theme with { Fonts = result.Fonts };

        if (result.Scope == ThemeScope.CurrentSlide)
        {
            if (opened is null || !Holds(t, opened)) return;
            ApplyToSlide(t, opened, theme);
            return;
        }

        choices.Remember(t.Deck);
        if (design.Theme == current && result.Fonts == current.Fonts) return;
        t.Actions.ApplyTheme(theme, design.Master with { Theme = theme });
    }

    private void ApplyToSlide(DeckTarget t, Slide slide, Theme theme)
    {
        var existing = lookup.Find(t.Deck, theme);
        using var transaction = t.Actions.History.BeginTransaction(texts["deck.action.theme"]);
        var master = existing ?? masters.CreateStandard(theme, t.Deck.Size) with
        {
            Id = t.Deck.NextMasterId(),
            Name = names.Display(theme),
        };
        if (existing is null) t.Actions.AddMaster(master);
        t.Actions.AssignMaster([slide], master);
    }
}
