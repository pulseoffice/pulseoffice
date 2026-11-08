using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Commenting;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.UI.Canvas;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Review;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioReviewTests
{
    private static FolioReviewSource Source(FolioScene scene)
    {
        var context = scene.Services.GetRequiredService<IActiveFolioContext>();
        context.Attach(new FolioTarget(scene.Editor, () => default!, _ => { }, () => 1, () => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask));
        return scene.Services.GetRequiredService<FolioReviewSource>();
    }

    [Fact]
    public Task ScrivereERegistrareSiVedeNelPannelloESiAnnullaInUnColpo() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Uno ");
        scene.Document.Changes.IsRecording = true;
        scene.Type("due");
        scene.Press(Key.Back);

        var source = Source(scene);
        var item = Assert.Single(source.Changes);
        Assert.Equal(ReviewItemKind.Insertion, item.Kind);
        Assert.Equal("du", item.Text);

        scene.Editor.Select(new DocumentPosition(scene.Editor.Caret.ParagraphId, 0), new DocumentPosition(scene.Editor.Caret.ParagraphId, 3));
        scene.Press(Key.Delete);
        Assert.Equal(" du", scene.Paragraphs[0]);
        Assert.Contains(source.Changes, change => change.Kind == ReviewItemKind.Deletion && change.Text == "Uno");

        source.Reject([.. source.Changes.Select(change => change.Id)]);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Uno ", scene.Paragraphs[0]);
        Assert.Empty(scene.Document.Changes.All);

        scene.History.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, scene.Document.Changes.All.Count);
    }, CancellationToken.None);

    [Fact]
    public Task ADeletionNestedInAnotherAuthorsInsertionSaysWhoseInsertionItIsIn() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Uno tre");
        var paragraph = (ParagraphBlock)scene.Document.Body[0];
        var when = new DateTimeOffset(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);
        scene.Document.Changes.Add(new Insertion("ct1", "Anna", when));
        scene.Document.Changes.Add(new Deletion("ct2", "Bruno", when, new BlockContainer([new ParagraphBlock("dp", "due ")])) { WithinInsertion = "ct1" });
        paragraph.State = paragraph.State with
        {
            Marks = [new Marks.ChangeMark(4, "ct1", Marks.MarkEdge.Start), new Marks.ChangeMark(4, "ct2", Marks.MarkEdge.Point), new Marks.ChangeMark(7, "ct1", Marks.MarkEdge.End)],
        };

        var source = Source(scene);
        var deletion = Assert.Single(source.Changes, change => change.Kind == ReviewItemKind.Deletion);
        Assert.Equal("due ", deletion.Text);
        Assert.Contains("Anna", deletion.Place);
        Assert.Null(Assert.Single(source.Changes, change => change.Kind == ReviewItemKind.Insertion).Place);
    }, CancellationToken.None);

    [Fact]
    public Task GoingToATrackedRowWithASingleParagraphSelectsIt() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        scene.Document.Changes.Add(new Insertion("row1", "Ada", DateTimeOffset.UnixEpoch));
        var cell = new ParagraphBlock("rp", "riga");
        scene.Document.Body.Add(new TableBlock("t1", [new TableRow([new TableCell(new BlockContainer([cell]))]) { ChangeId = "row1" }], [new Foundation.Units.Twip(1440)]));

        var source = Source(scene);
        source.Go("row1");

        Assert.Equal("rp", scene.Editor.Caret.ParagraphId);
    }, CancellationToken.None);

    [Fact]
    public Task IlFiloDeiCommentiSiRispondeERisolveDalPannello() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo commentato");
        scene.Editor.SelectAll();
        scene.Editor.Apply((documents, range) => documents.AddComment(scene.Document, scene.History, range, "Carla", DateTimeOffset.Now, "Chiaro?"), scene.Editor.Selection);
        var source = Source(scene);
        var root = Assert.Single(source.Comments);

        source.Reply(root.Id, "Sì");
        source.ToggleResolved(root.Id);
        var thread = source.Comments;
        Assert.Equal(2, thread.Count);
        Assert.Equal(1, thread[1].Depth);
        Assert.All(thread, item => Assert.True(item.Resolved));
        Assert.Contains("Testo commentato", thread[0].Note);

        var margin = scene.Services.GetRequiredService<IFolioCommentMargin>();
        Assert.True(margin.Shows(scene.Document));
        var page = scene.Editor.Layout.Pages[0];
        var balloon = Assert.Single(margin.Place(scene.Document, margin.Sheet(scene.Document), page));
        Assert.True(balloon.Bounds.X > page.Width);

        source.Delete([root.Id]);
        Assert.Empty(scene.Document.Comments.All);
        scene.History.Undo();
        Assert.Equal(2, scene.Document.Comments.All.Count);
    }, CancellationToken.None);

    [Fact]
    public Task SezioniProtetteEDocumentoInSolaLetturaNonSiScrivono() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace(
        [
            new ParagraphBlock("libero", "libero"),
            new SectionBlock("s1", "Chiusa", new BlockContainer([new ParagraphBlock("chiuso", "chiuso")])) { Protected = true },
        ]));
        scene.Editor.PlaceCaret(new DocumentPosition("chiuso", 0), false);
        scene.Type("x");
        Assert.Equal("chiuso", ((ParagraphBlock)((SectionBlock)scene.Document.Body[1]).Content[0]).Text);

        scene.Editor.PlaceCaret(new DocumentPosition("libero", 0), false);
        scene.Type("y");
        Assert.Equal("ylibero", scene.Paragraphs[0]);

        scene.Document.Protection = new DocumentProtection(DocumentRestriction.ReadOnly, null);
        scene.Type("z");
        Assert.Equal("ylibero", scene.Paragraphs[0]);

        scene.Document.Protection = new DocumentProtection(DocumentRestriction.Comments, null);
        scene.Type("w");
        Assert.Equal("ylibero", scene.Paragraphs[0]);
        Assert.True(scene.Services.GetRequiredService<IEditGuard>().CanComment(scene.Document));
    }, CancellationToken.None);

    [Fact]
    public Task NelDocumentoInSolaLetturaIlCommentoNonSiRisolve() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo commentato");
        scene.Editor.SelectAll();
        scene.Editor.Apply((documents, range) => documents.AddComment(scene.Document, scene.History, range, "Carla", DateTimeOffset.Now, "Nota"), scene.Editor.Selection);
        var comment = scene.Document.Comments.All.Single();
        scene.Document.Protection = new DocumentProtection(DocumentRestriction.ReadOnly, null);

        Source(scene).ToggleResolved(comment.Id);

        Assert.False(scene.Document.Comments.All.Single().Resolved);
    }, CancellationToken.None);

    [Fact]
    public Task LaLinguaDiUnaPorzioneDecideIlControllo() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document =>
        {
            document.Language = "it-IT";
            document.Body.Replace([new ParagraphBlock("p", new TextParagraph("ciao hello") { Runs = [new TextRun("ciao "), new TextRun("hello") { Language = "en-US" }] })]);
        });
        var segments = scene.Services.GetRequiredService<IFolioLanguages>().Segments(scene.Document, scene.Document.Body.Blocks.OfType<ParagraphBlock>().Single().Content);
        Assert.Equal([("it-IT", 0, 5), ("en-US", 5, 10)], segments.Select(segment => (segment.Language!, segment.Start, segment.End)));
    }, CancellationToken.None);

    [Fact]
    public Task ICommentiSiStampanoInFondoSoloSeRichiesto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo commentato");
        scene.Editor.SelectAll();
        scene.Editor.Apply((documents, range) => documents.AddComment(scene.Document, scene.History, range, "Carla", DateTimeOffset.Now, "Da stampare"), scene.Editor.Selection);
        var printables = scene.Services.GetRequiredService<PulseOffice.Documents.UI.Rendering.IFolioPrintables>();
        var marks = scene.Services.GetRequiredService<PulseOffice.Documents.UI.Rendering.IFolioViewMarks>();
        var pages = scene.Editor.Layout.Pages.Count;

        Assert.Equal(pages, printables.From(scene.Editor.Layout, scene.Document)!.PageCount);
        marks.PrintComments = true;
        Assert.Equal(pages + 1, printables.From(scene.Editor.Layout, scene.Document)!.PageCount);
        var sheet = scene.Services.GetRequiredService<PulseOffice.Documents.UI.Rendering.IFolioCommentSheet>().Build(scene.Document)!;
        Assert.Contains(sheet.Body.Blocks.OfType<ParagraphBlock>(), paragraph => paragraph.Text.Contains("Carla", StringComparison.Ordinal) && paragraph.Text.Contains("Da stampare", StringComparison.Ordinal));
        Assert.Contains(sheet.Body.Blocks.OfType<ParagraphBlock>(), paragraph => paragraph.Text.Contains("«Testo commentato»", StringComparison.Ordinal));
        marks.PrintComments = false;
    }, CancellationToken.None);

    [Fact]
    public Task MolteRevisioniEMoltiCommentiNonRallentanoLaScrittura() => FolioScene.Session.Dispatch(() =>
    {
        var when = new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
        var scene = new FolioScene(document =>
        {
            var blocks = new List<Block>();
            for (var i = 0; i < 300; i++)
            {
                var id = "p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                blocks.Add(new ParagraphBlock(id, new TextParagraph("Paragrafo con testo da rivedere numero " + i) { Runs = [new TextRun("Paragrafo con testo da rivedere numero " + i)] })
                {
                    Marks =
                    [
                        new PulseOffice.Documents.Marks.ChangeMark(0, "i" + id, PulseOffice.Documents.Marks.MarkEdge.Start),
                        new PulseOffice.Documents.Marks.ChangeMark(9, "i" + id, PulseOffice.Documents.Marks.MarkEdge.End),
                        new PulseOffice.Documents.Marks.ChangeMark(15, "d" + id, PulseOffice.Documents.Marks.MarkEdge.Point),
                        new PulseOffice.Documents.Marks.CommentMark(20, "c" + id, PulseOffice.Documents.Marks.MarkEdge.Start),
                        new PulseOffice.Documents.Marks.CommentMark(25, "c" + id, PulseOffice.Documents.Marks.MarkEdge.End),
                    ],
                });
                document.Changes.Add(new Insertion("i" + id, i % 2 == 0 ? "Anna" : "Bruno", when));
                document.Changes.Add(new Deletion("d" + id, "Carla", when, new BlockContainer([new ParagraphBlock("x" + id, "via ")])));
                document.Comments.Add(new Comment("c" + id, "Dario", when, new BlockContainer([new ParagraphBlock("k" + id, "Nota " + i)])));
            }
            document.Body.Replace(blocks);
            document.Changes.IsRecording = true;
        });
        var keys = new List<double>();
        var layouts = new List<double>();
        for (var i = 0; i < 20; i++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            scene.Editor.Type("x");
            var inner = System.Diagnostics.Stopwatch.StartNew();
            _ = scene.Editor.Layout;
            layouts.Add(inner.Elapsed.TotalMilliseconds);
            keys.Add(watch.Elapsed.TotalMilliseconds);
        }
        var renders = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            scene.View.InvalidateVisual();
            Dispatcher.UIThread.RunJobs();
            renders.Add(watch.Elapsed.TotalMilliseconds);
        }
        var typing = Median(keys);
        var render = Median(renders);
        var perKey = typing + render;
        Assert.True(typing < 40 && perKey < 250, $"{perKey:F1} ms per tasto: typing {typing:F1} layout {Median(layouts):F1} render {render:F1}");
        Assert.Equal(600, scene.Document.Changes.All.Count);
        var panel = Enumerable.Range(0, 3).Select(_ =>
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var source = Source(scene);
            Assert.Equal(600, source.Changes.Count);
            Assert.Equal(300, source.Comments.Count);
            return watch.Elapsed.TotalMilliseconds;
        }).Min();
        Assert.True(panel < 2000, $"pannello in {panel:F0} ms");
    }, CancellationToken.None);

    [Fact]
    public Task InSolaLetturaIlPannelloNonCambiaNulla() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Document.Changes.IsRecording = true;
        scene.Type("nuovo");
        scene.Editor.SelectAll();
        scene.Editor.Apply((documents, range) => documents.AddComment(scene.Document, scene.History, range, "Carla", DateTimeOffset.Now, "Nota"), scene.Editor.Selection);
        var source = Source(scene);
        scene.ReadOnly = true;

        Assert.False(source.Abilities.Resolve);
        Assert.False(source.Abilities.Comment);
        source.Accept([.. source.Changes.Select(change => change.Id)]);
        source.Delete([.. source.Comments.Select(comment => comment.Id)]);
        source.Reply(source.Comments[0].Id, "no");
        Assert.Single(scene.Document.Changes.All);
        Assert.Single(scene.Document.Comments.All);
        scene.ReadOnly = false;
    }, CancellationToken.None);

    private double Median(List<double> samples) => samples.Order().ElementAt(samples.Count / 2);

    [Fact]
    public Task RifiutareLaCancellazioneDiUnParagrafoRiportaIlCommentoEIlRiquadro() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno");
        scene.Press(Key.Enter);
        scene.Type("due");
        scene.Press(Key.Enter);
        scene.Type("tre");
        var middle = scene.Document.Body.Blocks.OfType<ParagraphBlock>().ElementAt(1);
        scene.Services.GetRequiredService<IDocumentEditor>().AddComment(scene.Document, scene.History, DocumentRange.In(middle.Id, 0, 2), "Bea", DateTimeOffset.UnixEpoch, "vedi");
        scene.Document.Objects.Add(new PulseOffice.Documents.Objects.AnchoredObject("frame1", new PulseOffice.Documents.Objects.ParagraphAnchor(middle.Id),
            new PulseOffice.Documents.Objects.TextBoxContent(new BlockContainer([new ParagraphBlock("frame1-p", "riquadro")])),
            new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(2))));
        scene.Document.Changes.IsRecording = true;
        var paragraphs = scene.Document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        scene.Editor.Select(new DocumentPosition(paragraphs[0].Id, 2), new DocumentPosition(paragraphs[2].Id, 1));
        scene.Press(Key.Delete);

        var source = Source(scene);
        Assert.Single(source.Comments);
        Assert.Single(scene.Document.Objects.All);
        source.Reject([.. source.Changes.Select(change => change.Id)]);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["uno", "due", "tre"], scene.Paragraphs);
        var restored = scene.Document.Body.Blocks.OfType<ParagraphBlock>().ElementAt(1);
        var comment = Assert.Single(scene.Document.Comments.All);
        Assert.Contains(restored.Marks.OfType<PulseOffice.Documents.Marks.CommentMark>(), mark => mark.CommentId == comment.Id);
        Assert.Equal(restored.Id, Assert.Single(scene.Document.Objects.All).Anchor.Accept(new PulseOffice.Documents.Objects.AnchorParagraph()));
    }, CancellationToken.None);
}
