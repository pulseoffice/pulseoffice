using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI;

public sealed class DeckLinkCommands(
    IActiveDeckContext context,
    IImageLinks links,
    IRasterImageFiles files,
    IStatusBarService status,
    ITextCatalog texts) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("deck.insert.imageLink") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.CurrentSlide() is not null,
            Execute = (_, ct) => InsertAsync(ct),
        },
        Prototype("deck.edit.links") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Linked().Any(),
            Execute = (_, ct) => ManageAsync(ct),
        },
    ];

    private IEnumerable<(Slide Slide, ObjectElement Element, RasterImageObject Image)> Linked() =>
        Context.Target?.Deck.Slides.SelectMany(slide => slide.Elements.OfType<ObjectElement>()
            .Where(element => element.Content is RasterImageObject { LinkPath.Length: > 0 })
            .Select(element => (slide, element, (RasterImageObject)element.Content))) ?? [];

    private async Task InsertAsync(CancellationToken ct)
    {
        if (await files.PickAsync(ct).ConfigureAwait(true) is not { FilePath: { Length: > 0 } path } source) return;
        if (Context.Target?.InsertLinkedImage(source.Data, source.FileName, path) == true)
            status.Report(texts.Format("links.inserted", Path.GetFileName(path)));
    }

    private async Task ManageAsync(CancellationToken ct)
    {
        while (Context.Target is { } t)
        {
            var linked = Linked().ToList();
            var described = links.Describe(linked.Select(entry =>
                (entry.Element.Id, $"{texts["links.slide"]} {t.Deck.Slides.ToList().IndexOf(entry.Slide) + 1} · {Path.GetFileName(entry.Image.LinkPath)}", entry.Image)));
            if (await links.ChooseAsync(described, "command.deck.edit.links.title", ct).ConfigureAwait(true) is not { } request) return;
            if (linked.FirstOrDefault(entry => entry.Element.Id == request.Id) is not { Image: not null } target) return;

            RasterImageObject? updated = request.Action switch
            {
                ExternalLinkAction.Update => links.Update(target.Image),
                ExternalLinkAction.ChangeSource => await links.ChangeSourceAsync(target.Image, ct).ConfigureAwait(true),
                _ => links.Break(target.Image),
            };

            if (updated is null)
            {
                status.Report(texts["links.unavailable"], NotificationSeverity.Error);
                continue;
            }

            t.Actions.ReplaceElement(target.Slide, target.Element with { Content = updated }, "deck.action.links");
            if (!Linked().Any()) return;
        }
    }
}
