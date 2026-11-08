using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioLinkCommandTests
{
    private sealed class ScriptedLinks(params ExternalLinkRequest[] requests) : IImageLinks
    {
        private readonly Queue<ExternalLinkRequest> _requests = new(requests);

        public List<IReadOnlyList<ExternalLink>> Shown { get; } = [];

        public IReadOnlyList<ExternalLink> Describe(IEnumerable<(string Id, string Name, RasterImageObject Image)> images) =>
            [.. images.Select(entry => new ExternalLink(entry.Id, entry.Name, entry.Image.LinkPath!, true))];

        public RasterImageObject? Update(RasterImageObject image) => image with { Data = new byte[] { 9, 9, 9 } };

        public Task<RasterImageObject?> ChangeSourceAsync(RasterImageObject image, CancellationToken ct) => Task.FromResult<RasterImageObject?>(null);

        public RasterImageObject Break(RasterImageObject image) => image with { LinkPath = null };

        public Task<ExternalLinkRequest?> ChooseAsync(IReadOnlyList<ExternalLink> links, string titleKey, CancellationToken ct)
        {
            Shown.Add(links);
            return Task.FromResult(_requests.Count > 0 ? _requests.Dequeue() : null);
        }
    }

    private static readonly CommandContext Context = new(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen);

    private static void Insert(FolioScene scene)
    {
        var size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(1), PulseOffice.Foundation.Units.Twip.FromCentimeters(1));
        var image = new RasterImageObject { Id = "img", Data = new byte[] { 1 }, MediaType = "image/png", NaturalSize = size, LinkPath = "/pictures/logo.png" };
        var item = new PulseOffice.Documents.Objects.AnchoredObject("o1", new PulseOffice.Documents.Objects.AsCharacterAnchor(scene.Editor.Caret.ParagraphId),
            new PulseOffice.Documents.Objects.EmbeddedContent(image), size);
        scene.Editor.Insert((documents, at) => documents.InsertObject(scene.Document, scene.History, item, at), "test");
    }

    private static RasterImageObject Image(FolioScene scene) =>
        (RasterImageObject)((PulseOffice.Documents.Objects.EmbeddedContent)scene.Document.Objects.All.Single().Content).Value;

    private static Task Run(FolioScene scene, IImageLinks links)
    {
        var time = TimeProvider.System;
        var status = new StatusBarService(time, new NotificationCenter(time), new NotificationHistory(time));
        var commands = new FolioMasterCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), scene.Services.GetRequiredService<ISubdocuments>(),
            null!, null!, null!, null!, null!, null!, status, links, scene.Services.GetRequiredService<IFolioObjectEditing>(), new TextCatalog(UiLanguage.English));
        return commands.GetCommands().Single(command => command.Id.Value == "folio.edit.links").Execute(Context, CancellationToken.None);
    }

    [Fact]
    public Task Updating_a_linked_image_reloads_that_image() => FolioScene.Session.Dispatch(async () =>
    {
        var scene = new FolioScene();
        scene.Type("Logo");
        Insert(scene);
        var links = new ScriptedLinks(new ExternalLinkRequest("i:o1", ExternalLinkAction.Update));

        await Run(scene, links);

        Assert.Equal(new byte[] { 9, 9, 9 }, Image(scene).Data.ToArray());
        Assert.Equal("/pictures/logo.png", Image(scene).LinkPath);
        Assert.Equal(2, links.Shown.Count);
    }, CancellationToken.None);

    [Fact]
    public Task Breaking_a_linked_image_keeps_the_picture_without_the_link() => FolioScene.Session.Dispatch(async () =>
    {
        var scene = new FolioScene();
        scene.Type("Logo");
        Insert(scene);

        await Run(scene, new ScriptedLinks(new ExternalLinkRequest("i:o1", ExternalLinkAction.Break)));

        Assert.Null(Image(scene).LinkPath);
        Assert.Equal(new byte[] { 1 }, Image(scene).Data.ToArray());
    }, CancellationToken.None);
}
