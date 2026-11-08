using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI;

public sealed class DeckMasterCommands(
    IActiveDeckContext context,
    IThemeCatalog themes,
    IMasterFactory masters,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    Themes.IThemeNames names,
    Design.MasterGalleryServices gallery,
    Design.MasterElementsServices elementViews) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("deck.master.new", _ => true, NewAsync),
        Command("deck.master.delete", t => t.Deck.Masters.Count > 1, DeleteAsync),
        Command("deck.master.change", _ => true, ChangeAsync),
        Command("deck.master.elements", _ => true, ElementsAsync),
    ];

    private CommandDefinition Command(string id, Func<DeckTarget, bool> enabled, Func<CancellationToken, Task> run) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && enabled(t),
            Execute = (_, ct) => run(ct),
        };

    private string Label(SlideMaster master) => $"{master.Name} ({names.Display(master.Theme)})";

    private async Task NewAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t) return;
        var catalog = themes.Themes;
        var items = catalog.Select(theme => new Design.MasterGalleryItem(masters.CreateStandard(theme, t.Deck.Size), names.Display(theme))).ToList();
        var result = await dialogs.ShowAsync<Design.MasterGalleryResult>(
            new Design.MasterGalleryView(new Design.MasterGalleryRequest(items, 0, texts["deck.master.new.explanation"],
                [new Design.MasterGalleryAction("create", texts["deck.master.create"], Primary: true)], t.Deck.Size)
            {
                NamePrompt = texts["deck.master.name"],
                SuggestedName = item => $"{item.Caption} {t.Deck.Masters.Count + 1}",
            }, gallery),
            new DialogOptions { TitleKey = "command.deck.master.new.title", PreferredWidth = 720 }, ct).ConfigureAwait(true);
        if (result is null || result.Index < 0 || result.Index >= catalog.Count) return;
        var target = t;

        var theme = catalog[result.Index];
        var name = result.Name.Length > 0 ? result.Name : $"{names.Display(theme)} {target.Deck.Masters.Count + 1}";
        target.Actions.AddMaster(masters.CreateStandard(theme, target.Deck.Size) with { Id = target.Deck.NextMasterId(), Name = name });
        status.Report(texts.Format("deck.master.added", name));
    }

    private async Task DeleteAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t) return;
        var candidates = t.Deck.Masters.Skip(1).ToList();
        var result = await dialogs.ShowAsync<Design.MasterGalleryResult>(
            new Design.MasterGalleryView(new Design.MasterGalleryRequest(
                [.. candidates.Select(master => new Design.MasterGalleryItem(master, Label(master)))], 0,
                texts["deck.master.delete.explanation"],
                [new Design.MasterGalleryAction("delete", texts["deck.master.deleteAction"], Primary: true)], t.Deck.Size), gallery),
            new DialogOptions { TitleKey = "command.deck.master.delete.title", PreferredWidth = 720 }, ct).ConfigureAwait(true);
        if (result is null || result.Index < 0 || result.Index >= candidates.Count || !t.Deck.Masters.Contains(candidates[result.Index])) return;
        t.Actions.DeleteMaster(candidates[result.Index]);
    }

    private async Task ChangeAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;
        var available = t.Deck.Masters.ToList();
        var result = await dialogs.ShowAsync<Design.MasterGalleryResult>(
            new Design.MasterGalleryView(new Design.MasterGalleryRequest(
                [.. available.Select(master => new Design.MasterGalleryItem(master, Label(master)))],
                Math.Max(0, available.IndexOf(t.Deck.MasterOf(slide))),
                texts["deck.master.change.explanation"],
                [
                    new Design.MasterGalleryAction("all", texts["deck.motion.applyAll"]),
                    new Design.MasterGalleryAction("slide", texts["deck.motion.apply"], Primary: true),
                ], t.Deck.Size), gallery),
            new DialogOptions { TitleKey = "command.deck.master.change.title", PreferredWidth = 720 }, ct).ConfigureAwait(true);
        if (result is null || result.Index < 0 || result.Index >= available.Count || !t.Deck.Masters.Contains(available[result.Index])) return;
        if (result.Action != "all" && !Holds(t, slide)) return;
        IReadOnlyList<Slide> targets = result.Action == "all" ? [.. t.Deck.Slides] : [slide];
        t.Actions.AssignMaster(targets, available[result.Index]);
    }

    private async Task ElementsAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;
        var master = t.Deck.MasterOf(slide);
        var elements = await dialogs.ShowAsync<MasterElements>(
            new Design.MasterElementsView(t.Deck, master, elementViews),
            new DialogOptions { TitleKey = "command.deck.master.elements.title", PreferredWidth = 660 }, ct).ConfigureAwait(true);
        if (elements is null || !t.Deck.Masters.Contains(master)) return;
        t.Actions.ReplaceMaster(master with { Elements = elements });
    }
}
