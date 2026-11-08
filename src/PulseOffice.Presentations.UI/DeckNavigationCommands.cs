using PulseOffice.UI.Controls.Notifications;
using System.Globalization;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI;

public sealed class DeckNavigationCommands(
    IActiveDeckContext context,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    Design.SlidePickerServices pickers,
    Design.ISlideCaptions captions) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Navigate("deck.navigate.first", t => t.CurrentIndex() > 0, _ => 0),
        Navigate("deck.navigate.previous", t => t.CurrentIndex() > 0, t => t.CurrentIndex() - 1),
        Navigate("deck.navigate.next", t => t.CurrentIndex() < t.Deck.Slides.Count - 1, t => t.CurrentIndex() + 1),
        Navigate("deck.navigate.last", t => t.CurrentIndex() < t.Deck.Slides.Count - 1, t => t.Deck.Slides.Count - 1),
        Navigate("deck.navigate.lastEdited",
            t => t.LastEditedIndex() is { } edited && edited != t.CurrentIndex(),
            t => t.LastEditedIndex() ?? t.CurrentIndex()),

        Prototype("deck.navigate.goTo") with
        {
            Surface = CommandSurface.Common,
            DefaultGesture = "Ctrl+G",
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is { } t && t.Deck.Slides.Count > 1,
            Execute = (_, ct) => GoToAsync(ct),
        },

        Prototype("deck.slide.rename") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            ContextPaths = ["slide/manage"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => RenameAsync(ct),
        },
    ];

    private CommandDefinition Navigate(string id, Func<DeckTarget, bool> enabled, Func<DeckTarget, int> index) => Prototype(id) with
    {
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        EnabledWhen = _ => Context.Target is { } t && t.Deck.Slides.Count > 0 && enabled(t),
        Execute = (_, _) =>
        {
            if (Context.Target is { } t) t.GoToSlide(index(t));
            return Task.CompletedTask;
        },
    };

    private async Task GoToAsync(CancellationToken ct)
    {
        if (Context.Target is not { } target) return;
        var count = target.Deck.Slides.Count;
        var chosen = await dialogs.ShowAsync<object>(
            new Design.SlidePickerView(target.Deck, target.CurrentIndex(), texts.Format("deck.navigate.goTo.explanation", count), pickers, captions),
            new DialogOptions { TitleKey = "command.deck.navigate.goTo.title", PreferredWidth = 700 },
            ct).ConfigureAwait(true);
        if (chosen is int index && ReferenceEquals(Context.Target, target) && target.Deck.Slides.Count > 0)
            target.GoToSlide(Math.Clamp(index, 0, target.Deck.Slides.Count - 1));
    }

    private async Task RenameAsync(CancellationToken ct)
    {
        if (Context.Target is not { } target || target.CurrentSlide() is not { } slide) return;

        var answers = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts["deck.slide.rename.explanation"],
                [new TextField("name", texts["deck.slide.rename.name"], slide.Name ?? string.Empty)],
                texts),
            new DialogOptions { TitleKey = "command.deck.slide.rename.title", PreferredWidth = 440 },
            ct).ConfigureAwait(true);

        if (answers is null || !Holds(target, slide)) return;

        var name = ((string)answers["name"]).Trim();

        if (name.Length == 0)
        {
            target.Actions.RenameSlide(slide, null);
            return;
        }

        if (!SlideNames.IsAvailable(target.Deck, slide, name))
        {
            status.Report(texts.Format("deck.slide.rename.taken", name), NotificationSeverity.Error);
            return;
        }

        target.Actions.RenameSlide(slide, name);
    }
}
