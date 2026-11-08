using PulseOffice.Drawing.Layout;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI;

public sealed class DeckObjectCommands(
    IActiveDeckContext context,
    IBoxDistribution distribution,
    IDialogService dialogs,
    PulseOffice.UI.Controls.Macros.IMacroEventsDialog macroEvents,
    DeckMacroEventsSource macroEventsSource,
    ITextCatalog texts,
    IDeckInteractionForm interactions) : DeckCommandSet(context)
{

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("deck.object.distribute", t => t.SelectedMany().Count >= 3, DistributeAsync),
        Command("deck.order.beforeObject", t => t.SelectedMany().Count == 2, _ => Relative(front: true)),
        Command("deck.order.behindObject", t => t.SelectedMany().Count == 2, _ => Relative(front: false)),
        Command("deck.object.name", t => t.Selected() is ObjectElement, NameAsync),
        Command("deck.object.interaction", t => t.Selected() is not null, InteractionAsync),
        Command("deck.object.hide", t => t.SelectedMany().Count > 0, _ => Hide()),
        Command("deck.object.showHidden", t => t.CurrentSlide()!.Elements.Any(element => element.Hidden), _ => ShowHidden()),
    ];

    private Task Hide()
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return Task.CompletedTask;
        t.Actions.ReplaceElements(slide, [.. t.SelectedMany().Select(element => element with { Hidden = true })], "deck.action.hide");
        return Task.CompletedTask;
    }

    private Task ShowHidden()
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return Task.CompletedTask;
        t.Actions.ReplaceElements(slide, [.. slide.Elements.Where(element => element.Hidden).Select(element => element with { Hidden = false })], "deck.action.showHidden");
        return Task.CompletedTask;
    }

    private CommandDefinition Command(string id, Func<DeckTarget, bool> enabled, Func<CancellationToken, Task> run) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null && enabled(t),
            Execute = (_, ct) => run(ct),
        };

    private async Task DistributeAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;
        var selected = t.SelectedMany();
        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["deck.distribute.explanation"],
                [new ChoiceField("axis", texts["deck.distribute.axis"], [texts["deck.distribute.horizontal"], texts["deck.distribute.vertical"]])],
                texts),
            new DialogOptions { TitleKey = "command.deck.object.distribute.title", PreferredWidth = 420 }, ct).ConfigureAwait(true);
        if (values is null || selected.Any(element => !Holds(t, slide, element))) return;

        var horizontal = values["axis"] is not int axis || axis == 0;
        var boxes = selected.Select(element => horizontal
            ? new DistributedBox(element.Id, element.Bounds.Left.Value, element.Bounds.Width.Value)
            : new DistributedBox(element.Id, element.Bounds.Top.Value, element.Bounds.Height.Value)).ToList();

        var placements = distribution.EqualGaps(boxes).Select(box =>
        {
            var bounds = slide.Find(box.Id)!.Bounds;
            var start = new Twip((int)Math.Round(box.Start));
            return (box.Id, horizontal
                ? new DocumentRect(start, bounds.Top, bounds.Width, bounds.Height)
                : new DocumentRect(bounds.Left, start, bounds.Width, bounds.Height));
        }).ToList();
        t.Actions.AlignElements(slide, placements);
    }

    private Task Relative(bool front)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide || t.Selected() is not { } moving) return Task.CompletedTask;
        if (t.SelectedMany().FirstOrDefault(element => element.Id != moving.Id) is not { } reference) return Task.CompletedTask;

        var ordered = slide.InZOrder().Where(element => element.Id != moving.Id).ToList();
        var index = ordered.FindIndex(element => element.Id == reference.Id);
        ordered.Insert(front ? index + 1 : index, moving);
        t.Actions.ReorderElements(slide, [.. ordered.Select((element, i) => (element.Id, i))]);
        return Task.CompletedTask;
    }

    private async Task NameAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide || t.Selected() is not ObjectElement element) return;
        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["deck.objectName.explanation"],
                [new TextField("name", texts["deck.objectName.name"], element.Content.Name ?? string.Empty)], texts),
            new DialogOptions { TitleKey = "command.deck.object.name.title", PreferredWidth = 420 }, ct).ConfigureAwait(true);
        if (values is null || !Holds(t, slide, element)) return;

        var name = (values["name"] as string)?.Trim();
        t.Actions.ReplaceElement(slide, element with { Content = element.Content with { Name = string.IsNullOrEmpty(name) ? null : name } },
            "command.deck.object.name.title");
    }

    private async Task InteractionAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.Selected() is not { } element || t.CurrentSlide() is not { } slide) return;
        var current = slide.Interaction(element.Id);
        var deckSlides = t.Deck.Slides.ToList();
        var choices = interactions.Open(current, deckSlides);
        var slides = deckSlides.Select((s, i) => SlideNames.PageName(s) is var n ? $"{i + 1}. {n}" : string.Empty).ToList();

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["deck.interactionDialog.explanation"],
            [
                new ChoiceField(DeckInteractionForm.KindKey, texts["deck.interactionDialog.kind"],
                    [.. choices.Kinds.Select(kind => texts[interactions.LabelKey(kind)])], choices.Kind),
                new ChoiceField(DeckInteractionForm.SlideKey, texts["deck.interactionDialog.slide"], slides, choices.Slide),
                new TextField(DeckInteractionForm.ReferenceKey, texts["deck.interactionDialog.reference"], choices.Reference),
            ], texts),
            new DialogOptions { TitleKey = "command.deck.object.interaction.title", PreferredWidth = 480 }, ct).ConfigureAwait(true);
        if (values is null || !Holds(t, slide, element)) return;

        var answer = interactions.Read(choices, values, deckSlides);
        var kind = answer.Kind;
        var reference = answer.Reference;

        if (kind == InteractionKind.RunMacro)
        {
            if (await MacroAsync(current is { Kind: InteractionKind.RunMacro } ? current.Reference : null, element, ct)
                .ConfigureAwait(true) is not { } chosen) return;
            reference = chosen.Value;
            if (reference is null) kind = InteractionKind.None;
            if (!Holds(t, slide, element)) return;
        }
        t.Actions.SetInteraction(slide, element.Id, interactions.Build(element.Id, new DeckInteractionAnswer(kind, reference), current));
    }

    private async Task<MacroChoice?> MacroAsync(string? current, SlideElement element, CancellationToken ct)
    {
        if (macroEventsSource.Create(readOnly: false) is not { } events) return null;

        var name = element is ObjectElement { Content.Name: { Length: > 0 } named } ? named : "Object";
        var stem = new string([.. name.Where(char.IsLetterOrDigit)]);
        var slot = new PulseOffice.Document.Forms.MacroEventSlot(
            "click", "deck.interactionDialog.onClick", "deck.interactionDialog.onClick.description",
            $"{(stem.Length == 0 ? "Object" : stem)}_Click");

        var result = await macroEvents.ShowAsync(
            new PulseOffice.UI.Controls.Macros.MacroEventsRequest(
                [slot], new Dictionary<string, string?> { ["click"] = current }, events,
                "command.deck.object.interaction.title"),
            ct).ConfigureAwait(true);

        return result is null ? null : new MacroChoice(result.GetValueOrDefault("click"));
    }

    private sealed record MacroChoice(string? Value);
}
