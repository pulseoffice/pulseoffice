using PulseOffice.UI.Controls.StylePanels;
using PulseOffice.UI.Controls.Notifications;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Presentations.UI;

public sealed class DeckStyleCommands(
    IActiveDeckContext context,
    ISidePanelHost panel,
    IShapeGeometry geometry,
    IDialogService dialogs,
    IStatusBarService status,
    PulseOffice.UI.Controls.Shapes.IShapeStyleEditors editors,
    IStyleRenamePrompt renames,
    ITextCatalog texts) : DeckCommandSet(context)
{
    private const string PanelKey = "styles";

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("deck.style.panel", _ => true, _ =>
        {
            panel.Show(PanelKey);
            return Task.CompletedTask;
        }, edits: false),
        Command("deck.style.new", t => Styled(t) is not null, NewAsync),
        Command("deck.style.update", t => Styled(t) is { StyleName: not null }, _ => Update()),
        Command("deck.style.edit", t => t.Deck.GraphicStyles.All.Count > 0, EditAsync),
        Command("deck.style.manager", _ => true, ManagerAsync),
    ];

    private CommandDefinition Command(string id, Func<DeckTarget, bool> enabled, Func<CancellationToken, Task> run, bool edits = true) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = edits ? DocumentState.ReadOnly : DocumentState.None,
            DefaultGesture = id == "deck.style.manager" ? "F11" : null,
            EnabledWhen = _ => Context.Target is { } t && enabled(t),
            Execute = (_, ct) => run(ct),
        };

    private EmbeddedStyled? Styled(DeckTarget target) =>
        target.Selected() is ObjectElement element ? EmbeddedStyled.Of(element.Content) : null;

    private async Task NewAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || Styled(t) is not { } selected) return;
        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["deck.style.new.explanation"],
                [new TextField("name", texts["deck.style.name"], selected.StyleName ?? string.Empty)], texts),
            new DialogOptions { TitleKey = "command.deck.style.new.title", PreferredWidth = 420 }, ct).ConfigureAwait(true);
        if ((values?["name"] as string)?.Trim() is not { Length: > 0 } name) return;
        if (t.Deck.GraphicStyles.Find(name) is not null && name != selected.StyleName)
        {
            status.Report(texts.Format("styles.rename.taken", name), NotificationSeverity.Warning);
            return;
        }

        t.Actions.ChangeGraphicStyles(catalog => catalog.Set(new GraphicStyle(name, selected.Style)),
            styled => ReferenceEquals(styled.Content, selected.Content) ? styled.With(selected.Style, name) : null,
            "command.deck.style.new.title");
        status.Report(texts.Format("deck.style.created", name));
    }

    private Task Update()
    {
        if (Context.Target is not { } t || Styled(t) is not { StyleName: { } name } selected) return Task.CompletedTask;
        Redefine(t, name, selected.Style, "command.deck.style.update.title");
        return Task.CompletedTask;
    }

    private void Redefine(DeckTarget target, string name, ShapeStyle style, string labelKey) =>
        target.Actions.ChangeGraphicStyles(catalog => catalog.Set(new GraphicStyle(name, style)),
            styled => styled.StyleName == name ? styled.With(style, name) : null, labelKey);

    private async Task EditAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || await ChooseAsync(t, "command.deck.style.edit.title", Styled(t)?.StyleName, ct).ConfigureAwait(true) is not { } chosen) return;
        var edited = await dialogs.ShowAsync<StyledOutline>(
            editors.Create(new StyledOutline(geometry.Outline(ShapeKind.RoundedRectangle), chosen.Style)),
            new DialogOptions { TitleKey = "command.deck.style.edit.title", PreferredWidth = 720 }, ct).ConfigureAwait(true);
        if (edited is null) return;
        Redefine(t, chosen.Name, edited.Style, "command.deck.style.edit.title");
    }

    private async Task ManagerAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t) return;
        var styles = t.Deck.GraphicStyles.All;
        if (styles.Count == 0)
        {
            status.Report(texts["deck.style.none"], NotificationSeverity.Warning);
            return;
        }

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["deck.style.manager.explanation"],
            [
                new ChoiceField("style", texts["deck.style.style"], [.. styles.Select(style => style.Name)]),
                new ChoiceField("action", texts["deck.style.action"], [texts["deck.style.apply"], texts["deck.style.delete"]]),
            ], texts),
            new DialogOptions { TitleKey = "command.deck.style.manager.title", PreferredWidth = 440 }, ct).ConfigureAwait(true);
        if (values is null) return;
        var target = t;

        var chosen = styles[values["style"] is int i && i >= 0 && i < styles.Count ? i : 0];
        if (values["action"] is int action && action == 1)
            target.Actions.ChangeGraphicStyles(catalog => catalog.Remove(chosen.Name),
                styled => styled.StyleName == chosen.Name ? styled.With(styled.Style, null) : null, "deck.style.deleted");
        else Apply(target, chosen);
    }

    private void Apply(DeckTarget target, GraphicStyle style) =>
        target.Actions.ApplyGraphicStyle(target.SelectedMany().OfType<ObjectElement>().Select(element => element.Content), style);

    private async Task<GraphicStyle?> ChooseAsync(DeckTarget target, string titleKey, string? preferred, CancellationToken ct)
    {
        var styles = target.Deck.GraphicStyles.All;
        if (preferred is not null && target.Deck.GraphicStyles.Find(preferred) is { } direct) return direct;
        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["deck.style.choose"], [new ChoiceField("style", texts["deck.style.style"], [.. styles.Select(style => style.Name)])], texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 420 }, ct).ConfigureAwait(true);
        return values is null || styles.Count == 0 ? null : styles[values["style"] is int i && i >= 0 && i < styles.Count ? i : 0];
    }

    public bool CanUseStyles => Context.Target is not null;

    public string? CurrentStyle() => Context.Target is { } t ? Styled(t)?.StyleName : null;

    public bool HasSelection => Context.Target is { } t && Styled(t) is not null;

    public void ApplyNamed(string name)
    {
        if (Context.Target is { } target && target.Deck.GraphicStyles.Find(name) is { } style) Apply(target, style);
    }

    public Task CreateAsync(CancellationToken ct) => NewAsync(ct);

    public void UpdateSelected() => Update();

    public async Task EditNamedAsync(string name, CancellationToken ct)
    {
        if (Context.Target is not { } t || t.Deck.GraphicStyles.Find(name) is not { } chosen) return;
        var edited = await dialogs.ShowAsync<StyledOutline>(
            editors.Create(new StyledOutline(geometry.Outline(ShapeKind.RoundedRectangle), chosen.Style)),
            new DialogOptions { TitleKey = "command.deck.style.edit.title", PreferredWidth = 720 }, ct).ConfigureAwait(true);
        if (edited is null) return;
        Redefine(t, chosen.Name, edited.Style, "command.deck.style.edit.title");
    }

    public bool CanRename(string name) => Context.Target is { } target && target.Deck.GraphicStyles.Find(name) is not null;

    public async Task RenameAsync(string name, CancellationToken ct)
    {
        if (!CanRename(name) || await renames.AskAsync(name, "deck.styles.rename.explanation", ct).ConfigureAwait(true) is not { } chosen) return;
        if (Context.Target is not { } target || target.Deck.GraphicStyles.Find(name) is null) return;
        var newName = chosen.Trim();
        if (newName == name) return;
        if (newName.Any(char.IsControl))
        {
            status.Report(texts.Format("styles.rename.invalid", 255), NotificationSeverity.Warning);
            return;
        }
        if (target.Deck.GraphicStyles.Find(newName) is not null)
        {
            status.Report(texts.Format("styles.rename.taken", newName), NotificationSeverity.Warning);
            return;
        }
        target.Actions.ChangeGraphicStyles(catalog => catalog.Rename(name, newName),
            styled => styled.StyleName == name ? styled.With(styled.Style, newName) : null, "styles.rename.title");
        status.Report(texts.Format("styles.renamed", newName));
    }

    public void Delete(string name)
    {
        if (Context.Target is not { } target) return;
        target.Actions.ChangeGraphicStyles(catalog => catalog.Remove(name),
            styled => styled.StyleName == name ? styled.With(styled.Style, null) : null, "deck.style.deleted");
    }
}

public sealed class DeckStylesSource : IStylesPanelSource
{
    private const string Family = "graphic";

    private readonly IActiveDeckContext _context;
    private readonly DeckStyleCommands _commands;
    private readonly ITextCatalog _texts;

    public DeckStylesSource(IActiveDeckContext context, DeckStyleCommands commands, ITextCatalog texts)
    {
        _context = context;
        _commands = commands;
        _texts = texts;
        context.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<StyleFamilyChoice> Families => [new(Family, _texts["deck.style.family"]) { Icon = "panel.family.graphic" }];

    public bool CanApply(string family) => _commands.HasSelection;

    public IReadOnlyList<StyleListEntry> Styles(string family) =>
        _context.Target is { } target
            ? [.. target.Deck.GraphicStyles.All.Select(style => new StyleListEntry(style.Name, style.Name, 0)
            {
                Custom = true,
                Preview = new StylePreview
                {
                    Swatch = style.Style.Fill.IsVisible ? style.Style.Fill.ColorHex : "#00FFFFFF",
                    SwatchBorder = style.Style.Line.Dash == PulseOffice.Drawing.Shapes.ShapeStrokeStyle.None ? null : style.Style.Line.ColorHex,
                },
            })]
            : [];

    public string? Current(string family) => _commands.CurrentStyle();

    public bool Can(StylePanelAction action, string family, string? style) => _context.Target is not null && action switch
    {
        StylePanelAction.New => _commands.HasSelection,
        StylePanelAction.Update => _commands.CurrentStyle() is not null,
        StylePanelAction.Derive => false,
        StylePanelAction.Rename => style is not null && _commands.CanRename(style),
        _ => style is not null,
    };

    public void Apply(string family, string style) => _commands.ApplyNamed(style);

    public async Task RunAsync(StylePanelAction action, string family, string? style, CancellationToken ct)
    {
        switch (action)
        {
            case StylePanelAction.New:
                await _commands.CreateAsync(ct).ConfigureAwait(true);
                break;
            case StylePanelAction.Update:
                _commands.UpdateSelected();
                break;
            case StylePanelAction.Edit when style is not null:
                await _commands.EditNamedAsync(style, ct).ConfigureAwait(true);
                break;
            case StylePanelAction.Delete when style is not null:
                _commands.Delete(style);
                break;
            case StylePanelAction.Rename when style is not null:
                await _commands.RenameAsync(style, ct).ConfigureAwait(true);
                break;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
