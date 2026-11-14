using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI.Design;

public sealed class DeckLayoutCommands(
    IActiveDeckContext context,
    ILayoutChoices layouts,
    IDialogService dialogs,
    ITextCatalog texts) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("deck.slide.add") with
        {
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            ContextPaths = ["slide/manage"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is not null,
            RibbonControl = new SplitGallerySpec(Choices, Current) { Band = Band() },
            Execute = (ctx, _) => AddSlide(ctx.Argument as string),
        },

        Prototype("deck.layouts.gallery") with
        {
            Surface = CommandSurface.Ribbon | CommandSurface.Palette,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            RibbonControl = new ChoiceSpec(Choices, Current) { Look = RibbonChoiceLook.Band, Band = Band(), MoreCommandId = "deck.view.master" },
            Execute = (ctx, _) => ctx.Argument is string key ? Apply(key) : ShowLayouts(),
        },

        Manage("deck.layouts.rename", custom: true, RenameAsync),
        Manage("deck.layouts.duplicate", custom: false, DuplicateAsync),
        Manage("deck.layouts.delete", custom: true, DeleteAsync),
    ];

    private RibbonBandSpec Band() =>
        new(1, 4, 16.0 / 9.0) { PopoverTileWidth = 150, PopoverColumns = 4, Proportion = Proportion };

    private double Proportion() =>
        Context.Target is { Deck.Size: var size } && size.Height.Value > 0 ? (double)size.Width.Value / size.Height.Value : 16.0 / 9.0;

    private IReadOnlyList<RibbonChoice> Choices() =>
        Context.Target is { } t ? layouts.Ribbon(t.Deck, t.CurrentSlide()) : [];

    private string? Current() =>
        Context.Target is { } t && t.CurrentSlide() is { } slide ? layouts.KeyOf(t.Deck, slide) : null;

    private Task AddSlide(string? key)
    {
        if (Context.Target is not { } t) return Task.CompletedTask;
        if (key is not null && layouts.Resolve(t.Deck, t.CurrentSlide(), key) is { } choice)
            t.Actions.Layouts.AddSlide(choice.Master, choice.Layout, t.CurrentIndex());
        else if (key is null)
            t.Actions.AddSlide(SlideLayout.TitleAndContent, t.CurrentIndex());
        return Task.CompletedTask;
    }

    private Task ShowLayouts()
    {
        Context.Target?.SetViewMode(DeckViewMode.Master);
        return Task.CompletedTask;
    }

    private Task Apply(string? key)
    {
        if (key is null || Context.Target is not { } t || t.CurrentSlide() is not { } slide) return Task.CompletedTask;
        if (layouts.Resolve(t.Deck, slide, key) is { } choice) t.Actions.Layouts.ApplyLayout(slide, choice.Master, choice.Layout);
        return Task.CompletedTask;
    }

    private CommandDefinition Manage(string id, bool custom, Func<DeckTarget, SlideLayoutTemplate, CancellationToken, Task> run) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && Selected(t) is { } layout
                && (!custom || !t.Actions.Layouts.Catalog.IsStandard(t.Deck.Master, layout)),
            Execute = (_, ct) => Context.Target is { } t && Selected(t) is { } layout ? run(t, layout, ct) : Task.CompletedTask,
        };

    private SlideLayoutTemplate? Selected(DeckTarget target) =>
        target.ViewMode() == DeckViewMode.Master && target.MasterLayoutId() is { } id
            ? target.Actions.Layouts.Catalog.Find(target.Deck.Master, id)
            : null;

    private async Task RenameAsync(DeckTarget target, SlideLayoutTemplate layout, CancellationToken ct)
    {
        var master = target.Deck.Master;
        var name = await dialogs.ShowAsync<string>(
            new PulseOffice.UI.Controls.TextPromptView(texts["deck.layouts.renamePrompt"], layouts.Title(master, layout), texts),
            new DialogOptions { TitleKey = "command.deck.layouts.rename.title" }, ct).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(name) || !ReferenceEquals(target.Deck.Master, master)) return;
        target.Actions.Layouts.RenameLayout(master, layout.Id, name);
        target.SelectMasterLayout(layout.Id);
    }

    private Task DuplicateAsync(DeckTarget target, SlideLayoutTemplate layout, CancellationToken ct)
    {
        var master = target.Deck.Master;
        if (target.Actions.Layouts.DuplicateLayout(master, layout.Id, texts.Format("deck.layouts.copyName", layouts.Title(master, layout))) is { } copy)
            target.SelectMasterLayout(copy.Id);
        return Task.CompletedTask;
    }

    private Task DeleteAsync(DeckTarget target, SlideLayoutTemplate layout, CancellationToken ct)
    {
        target.Actions.Layouts.DeleteLayout(target.Deck.Master, layout.Id);
        target.SelectMasterLayout(null);
        return Task.CompletedTask;
    }
}
