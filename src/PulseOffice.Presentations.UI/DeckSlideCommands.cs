using PulseOffice.Foundation.Modules;
using PulseOffice.Formats;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Commands;

namespace PulseOffice.Presentations.UI;

public sealed class DeckSlideCommands(
    IActiveDeckContext context,
    IFileDialogService files,
    IPresentationFormatRegistry formats,
    ISlideOutlining outlining,
    ITextCatalog texts,
    PulseOffice.UI.Controls.Services.IDialogService dialogs,
    Design.SlideBackgroundServices backgrounds,
    PulseOffice.UI.Controls.Objects.IRasterImageFiles images,
    PulseOffice.UI.Controls.Objects.IChosenFileReader reader,
    PulseOffice.UI.Controls.Services.IStatusBarService status) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Move("deck.slide.moveToStart", toStart: true),
        Move("deck.slide.moveToEnd", toStart: false),
        Prototype("deck.slide.insertFromFile") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, ct) => InsertFromFileAsync(ct),
        },
        Prototype("deck.slide.masterBackground") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            ContextPaths = ["slide"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.CurrentSlide() is not null,
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide)
                    t.Actions.SetMasterDisplay(slide, !slide.ShowMasterBackground, slide.ShowMasterObjects);
                return Task.CompletedTask;
            },
        },
        Prototype("deck.slide.masterObjects") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            ContextPaths = ["slide"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.CurrentSlide() is not null,
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide)
                    t.Actions.SetMasterDisplay(slide, slide.ShowMasterBackground, !slide.ShowMasterObjects);
                return Task.CompletedTask;
            },
        },
        Prototype("deck.slide.summary") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            ContextPaths = ["slide"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && outlining.Summary(t.Deck.Slides) is not null,
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && outlining.Summary(t.Deck.Slides) is { } summary)
                    t.Actions.InsertOutlineSlides(t.CurrentIndex(),
                        [summary with { Title = texts["deck.slide.summary.title"] }], "command.deck.slide.summary.title");
                return Task.CompletedTask;
            },
        },
        Prototype("deck.slide.expand") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            ContextPaths = ["slide"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is { } slide && outlining.Expand(slide).Count > 0,
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide)
                    t.Actions.InsertOutlineSlides(t.CurrentIndex(), outlining.Expand(slide), "command.deck.slide.expand.title");
                return Task.CompletedTask;
            },
        },
        Prototype("deck.slide.backgroundImage") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            ContextPaths = ["slide"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => BackgroundImageAsync(ct),
        },
        Prototype("deck.slide.backgroundFill") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            ContextPaths = ["slide"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => BackgroundFillAsync(ct),
        },
        Prototype("deck.slide.clearBackground") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            ContextPaths = ["slide"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is { } slide && (slide.BackgroundImage is not null || slide.BackgroundFill is not null),
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide)
                {
                    if (slide.BackgroundImage is not null) t.Actions.SetBackgroundImage(slide, null);
                    if (slide.BackgroundFill is not null) t.Actions.SetBackgroundFill(slide, null);
                }
                return Task.CompletedTask;
            },
        },
    ];

    private async Task BackgroundFillAsync(CancellationToken ct)
    {
        if (Context.Target is not { } target || target.CurrentSlide() is not { } slide) return;
        var result = await dialogs.ShowAsync<Design.SlideBackgroundResult>(
            new Design.SlideBackgroundView(target.Deck, slide, backgrounds),
            new PulseOffice.UI.Controls.Services.DialogOptions { TitleKey = "command.deck.slide.backgroundFill.title", PreferredWidth = 860 }, ct).ConfigureAwait(true);
        if (result is null || (!result.AllSlides && !Holds(target, slide))) return;
        target.Actions.SetBackgrounds(result.AllSlides ? [.. target.Deck.Slides] : [slide], result.Choice);
    }

    private async Task BackgroundImageAsync(CancellationToken ct)
    {
        if (Context.Target is not { } target || target.CurrentSlide() is not { } slide) return;

        if (await images.PickAsync(ct).ConfigureAwait(true) is not { } source || !Holds(target, slide)) return;

        target.Actions.SetBackgroundImage(slide, new PulseOffice.Drawing.Assets.RasterImageObject
        {
            Id = Guid.NewGuid().ToString("N"),
            Data = source.Data,
            MediaType = source.MediaType,
            PixelWidth = source.PixelWidth,
            PixelHeight = source.PixelHeight,
        });
    }

    private CommandDefinition Move(string id, bool toStart) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null
                && (toStart ? t.CurrentIndex() > 0 : t.CurrentIndex() < t.Deck.Slides.Count - 1),
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide)
                    t.Actions.MoveSlide(slide, toStart ? 0 : t.Deck.Slides.Count - 1);
                return Task.CompletedTask;
            },
        };

    private async Task InsertFromFileAsync(CancellationToken ct)
    {
        if (Context.Target is not { } target) return;

        var chosen = await files.OpenFileAsync(
            texts["deck.slide.insertFromFile.title"],
            [new FileFilter(texts["file.presentation"], [".odp", ".pptx"])],
            ct).ConfigureAwait(true);
        if (chosen is null) return;

        var content = await reader.ReadAsync(chosen, reader.DocumentLimit, ct).ConfigureAwait(true);
        if (!content.Succeeded)
        {
            status.Report(content.Status == PulseOffice.UI.Controls.Objects.ChosenFileReadStatus.TooLarge
                ? texts.Format("deck.slide.insertFromFile.tooLarge", chosen.Name, reader.DocumentLimit / (1024 * 1024))
                : texts.Format("deck.slide.insertFromFile.unreadable", chosen.Name), PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error);
            return;
        }

        using var buffer = new MemoryStream(content.Bytes, writable: false);
        var result = formats.Read(chosen.Path, buffer);
        if (!result.Succeeded || result.Value is not { } imported || imported.Slides.Count == 0)
        {
            status.Report(texts.Format("deck.slide.insertFromFile.unreadable", chosen.Name), PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error);
            return;
        }

        target.Actions.ImportSlides(target.CurrentIndex(), imported.Slides.ToList());
    }
}
