using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI;

public sealed class DeckMotionCommands(
    IActiveDeckContext context,
    Motion.IDeckMotionEditors editors,
    ITransitionCatalog transitions,
    IDialogService dialogs,
    ITextCatalog texts) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("deck.transition.dialog", _ => true, TransitionAsync),
        Command("deck.animation.dialog", t => t.CurrentSlide()?.Animations.Count > 0, AnimationAsync),
        Prototype("deck.transition.gallery") with
        {
            Surface = CommandSurface.Ribbon | CommandSurface.Palette,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            RibbonControl = new ChoiceSpec(TransitionChoices, CurrentTransition)
            {
                Look = RibbonChoiceLook.Band,
                Band = new RibbonBandSpec(1, 7, 16.0 / 9.0) { PopoverTileWidth = 112, PopoverColumns = 6 },
                MoreCommandId = "deck.transition.dialog",
            },
            Execute = (ctx, ct) => ctx.Argument is string key ? ApplyTransition(key) : TransitionAsync(ct),
        },
    ];

    private IReadOnlyList<RibbonChoice> TransitionChoices()
    {
        var template = Context.Target?.CurrentSlide()?.Transition ?? SlideTransition.None;
        return [.. transitions.Entries.Select(entry => new RibbonChoice(entry.Kind.ToString(), texts[$"deck.transition.{char.ToLowerInvariant(entry.Kind.ToString()[0])}{entry.Kind.ToString()[1..]}"])
        {
            Thumbnail = new Motion.TransitionRibbonThumbnail(transitions.Choose(template, entry.Kind)),
            Section = texts[$"deck.motion.group.{entry.Group switch { TransitionGroup.Exciting => "exciting", TransitionGroup.Dynamic => "dynamic", _ => "subtle" }}"],
        })];
    }

    private string? CurrentTransition() => Context.Target?.CurrentSlide()?.Transition.Kind.ToString();

    private Task ApplyTransition(string key)
    {
        if (Context.Target is { } t && t.CurrentSlide() is { } slide && Enum.TryParse<TransitionKind>(key, out var kind))
            t.Actions.SetTransition(slide, transitions.Choose(slide.Transition, kind));
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

    private async Task TransitionAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;
        var result = await dialogs.ShowAsync<Motion.TransitionDialogResult>(
            editors.TransitionDialog(t.Deck, slide),
            new DialogOptions { TitleKey = "command.deck.transition.dialog.title", PreferredWidth = 1100 }, ct).ConfigureAwait(true);
        if (result is null || (!result.AllSlides && !Holds(t, slide))) return;
        t.Actions.SetTransitions(result.AllSlides ? [.. t.Deck.Slides] : [slide], result.Transition);
    }

    private async Task AnimationAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;
        Motion.AnimationPane? pane = null;
        pane = editors.Animations(new Motion.AnimationPaneContext(
            slide,
            () => t.Selected(),
            id => editors.Names.Of(slide, id),
            animations =>
            {
                if (!Holds(t, slide)) return;
                t.Actions.ReplaceAnimations(slide, animations);
                pane?.Refresh();
            },
            () => t.Play()));
        await dialogs.ShowAsync<object>(
            new Motion.AnimationDialogView(pane, texts),
            new DialogOptions { TitleKey = "command.deck.animation.dialog.title", PreferredWidth = 420 }, ct).ConfigureAwait(true);
    }
}
