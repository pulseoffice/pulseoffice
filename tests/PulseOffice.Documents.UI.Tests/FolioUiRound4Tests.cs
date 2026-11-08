using Avalonia;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class CountingControlCatalog(IStoryCatalog stories, PulseOffice.Documents.Blocks.IBlockTree tree) : IContentControlCatalog
{
    private readonly ContentControlCatalog _inner = new(stories, tree);

    public int Walks { get; private set; }

    public IReadOnlyList<ContentControlSpan> All(TextDocument document)
    {
        Walks++;
        return _inner.All(document);
    }

    public ContentControlSpan? Find(TextDocument document, string controlId) => All(document).FirstOrDefault(span => span.Id == controlId);

    public ContentControlSpan? At(TextDocument document, DocumentPosition position)
    {
        Walks++;
        return _inner.At(document, position);
    }

    public int Order(TextDocument document, string paragraphId) => _inner.Order(document, paragraphId);

    public void Join(TextDocument document) => _inner.Join(document);
}

public sealed class CountingLineNumbers : PulseOffice.Documents.Layout.Queries.ILineNumbers
{
    private readonly PulseOffice.Documents.Layout.Queries.LineNumbers _inner = new();

    public int Passes { get; private set; }

    public IReadOnlyDictionary<PulseOffice.Documents.Layout.Tree.LineBox, int> Number(PulseOffice.Documents.Layout.Tree.DocumentLayout layout, PulseOffice.Documents.Styles.LineNumbering numbering)
    {
        Passes++;
        return _inner.Number(layout, numbering);
    }
}

public sealed class BrokenClipboard : PulseOffice.Exchange.IExchangeClipboard
{
    public bool IsAvailable => true;

    public Task WriteAsync(IReadOnlyList<PulseOffice.Exchange.ExchangeRepresentation> representations, CancellationToken ct = default) =>
        throw new IOException("clipboard busy");

    public Task<IReadOnlyList<PulseOffice.Exchange.ExchangeRepresentation>> ReadAsync(CancellationToken ct = default) =>
        throw new IOException("clipboard busy");
}

public sealed class FolioUiRound4Tests
{
    private static FolioScene Sandwich() => new(document => document.Body.Replace(
    [
        new ParagraphBlock("primo", "primo"),
        new SectionBlock("s1", "Chiusa", new BlockContainer([new ParagraphBlock("chiuso", "chiuso")])) { Protected = true },
        new ParagraphBlock("ultimo", "ultimo"),
    ]));

    private static ParagraphBlock Closed(FolioScene scene) => (ParagraphBlock)((SectionBlock)scene.Document.Body[1]).Content[0];

    private static FormatSample BoldSample() => new(new CharacterProperties { Bold = true }, null, new ParagraphProperties(), null);

    [Fact]
    public Task IlGrassettoNonAttraversaLaSezioneProtetta() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Sandwich();
        var before = Closed(scene).Content;
        scene.Editor.Select(new DocumentPosition("primo", 0), new DocumentPosition("ultimo", 6));

        Assert.False(scene.Editor.Toggle(CharacterToggle.Bold));

        Assert.Equal(before, Closed(scene).Content);
        Assert.False(scene.History.CanUndo);
    }, CancellationToken.None);

    [Fact]
    public Task LoStileDalPannelloNonAttraversaLaSezioneProtetta() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Sandwich();
        var before = Closed(scene).Content;
        scene.Editor.Select(new DocumentPosition("primo", 0), new DocumentPosition("ultimo", 6));

        Assert.False(scene.Editor.Apply((documents, range) => documents.ApplyParagraphStyle(scene.Document, scene.History, range, DefaultStyleNames.HeadingLevel(1)), scene.Editor.ParagraphRange()));

        Assert.Equal(before, Closed(scene).Content);
    }, CancellationToken.None);

    [Fact]
    public Task IlPennelloNonDipingeNelDocumentoProtetto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo libero");
        var before = scene.Document.Body.Blocks.OfType<ParagraphBlock>().First().Content;
        scene.Editor.Arm(BoldSample());
        scene.Editor.SelectAll();
        scene.Document.Protection = new DocumentProtection(DocumentRestriction.ReadOnly, null);

        Assert.False(scene.Editor.PaintBrush());

        Assert.Equal(before, scene.Document.Body.Blocks.OfType<ParagraphBlock>().First().Content);
    }, CancellationToken.None);

    [Fact]
    public Task IlPennelloNonAttraversaLaSezioneProtetta() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Sandwich();
        var before = Closed(scene).Content;
        scene.Editor.Arm(BoldSample());
        scene.Editor.Select(new DocumentPosition("primo", 0), new DocumentPosition("ultimo", 6));

        Assert.False(scene.Editor.PaintBrush());

        Assert.Equal(before, Closed(scene).Content);
    }, CancellationToken.None);

    [Fact]
    public Task IlLampeggioDelCursoreNonRipercorreIlDocumento() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(configure: services => services.AddSingleton<CountingControlCatalog>()
            .AddSingleton<IContentControlCatalog>(provider => provider.GetRequiredService<CountingControlCatalog>()));
        scene.Type("Testo");
        var catalog = scene.Services.GetRequiredService<CountingControlCatalog>();
        var canvas = scene.View.Canvas;
        using var bitmap = new RenderTargetBitmap(new PixelSize(400, 300));
        bitmap.Render(canvas);
        var first = catalog.Walks;

        for (var blink = 0; blink < 5; blink++) bitmap.Render(canvas);

        Assert.Equal(first, catalog.Walks);
    }, CancellationToken.None);

    [Fact]
    public Task LaNumerazioneDelleRigheNonSiRicalcolaAOgniFotogramma() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        scene.Document.LineNumbering = new PulseOffice.Documents.Styles.LineNumbering();
        var numbers = new CountingLineNumbers();
        var marks = new PulseOffice.Documents.UI.Rendering.FolioLineNumberMarks(numbers);
        var layout = scene.Editor.Layout;

        for (var frame = 0; frame < 4; frame++) marks.Apply(PulseOffice.Documents.UI.Rendering.PageMarks.None, scene.Document, layout);

        Assert.Equal(1, numbers.Passes);
    }, CancellationToken.None);

    [Fact]
    public Task UnIncollaDaTastieraCheFallisceLoDiceInveceDiTacere() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(configure: services => services.AddSingleton<PulseOffice.Exchange.IExchangeClipboard, BrokenClipboard>());
        scene.Type("Testo");
        var history = scene.Services.GetRequiredService<PulseOffice.UI.Controls.Notifications.INotificationHistory>();

        scene.View.Perform(PulseOffice.UI.Controls.Text.TextEditingAction.Paste);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Contains(history.Entries, entry => entry.Notification.Severity == PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error);
    }, CancellationToken.None);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task TrascinareUnOggettoNonLoAncoraNellaSezioneProtetta(bool inline) => FolioScene.Session.Dispatch(() =>
    {
        var scene = Sandwich();
        var size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(1), PulseOffice.Foundation.Units.Twip.FromCentimeters(1));
        var image = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "img", Data = new byte[] { 1 }, MediaType = "image/png", NaturalSize = size };
        PulseOffice.Documents.Objects.ObjectAnchor anchor = inline ? new PulseOffice.Documents.Objects.AsCharacterAnchor("primo") : new PulseOffice.Documents.Objects.CharacterAnchor("primo");
        var item = new PulseOffice.Documents.Objects.AnchoredObject("o1", anchor, new PulseOffice.Documents.Objects.EmbeddedContent(image), size);
        scene.Editor.PlaceCaret(new DocumentPosition("primo", 2), false);
        scene.Editor.Insert((documents, at) => documents.InsertObject(scene.Document, scene.History, item, at), "test");
        var before = Closed(scene);
        var (content, marks) = (before.Content, before.Marks);
        var caret = scene.Editor.Services.Queries.Caret(scene.Editor.Layout, new DocumentPosition("chiuso", 3))!.Value;

        Assert.False(scene.Services.GetRequiredService<IFolioObjectMover>().Move(scene.Editor, "o1", new ObjectDrop(caret.Page, caret.X, caret.Y, caret.X, caret.Y + 1)));

        Assert.Equal(content, Closed(scene).Content);
        Assert.Equal(marks, Closed(scene).Marks);
    }, CancellationToken.None);

    [Fact]
    public Task RifiutareUnaModificaNelPannelloNonToccaLaSezioneProtetta() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace(
        [
            new ParagraphBlock("primo", "primo"),
            new SectionBlock("s1", "Chiusa", new BlockContainer([new ParagraphBlock("chiuso", "chiuso")])),
        ]));
        scene.Document.Changes.IsRecording = true;
        scene.Editor.PlaceCaret(new DocumentPosition("chiuso", 6), false);
        scene.Type(" aggiunto");
        ((SectionBlock)scene.Document.Body[1]).Protected = true;
        var context = scene.Services.GetRequiredService<IActiveFolioContext>();
        context.Attach(new FolioTarget(scene.Editor, () => default!, _ => { }, () => 1, () => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask));
        var source = scene.Services.GetRequiredService<FolioReviewSource>();
        var before = Closed(scene).Content;

        source.Reject([.. source.Changes.Select(change => change.Id)]);

        Assert.Equal(before, Closed(scene).Content);
    }, CancellationToken.None);
}
