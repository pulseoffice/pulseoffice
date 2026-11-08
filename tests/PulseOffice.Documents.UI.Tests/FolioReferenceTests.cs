using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Navigation;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioReferenceTests
{
    private static void Book(TextDocument document)
    {
        document.Body.Replace(
        [
            new ParagraphBlock("intro", string.Empty, DefaultStyleNames.TextBody),
            new ParagraphBlock("h1", "Primo capitolo", DefaultStyleNames.HeadingLevel(1)),
            new ParagraphBlock("t1", string.Join(' ', Enumerable.Repeat("testo", 2000)), DefaultStyleNames.TextBody),
            new ParagraphBlock("h2", "Secondo capitolo", DefaultStyleNames.HeadingLevel(1)),
            new ParagraphBlock("t2", "fine", DefaultStyleNames.TextBody),
        ]);
    }

    private static IReadOnlyList<string> Lines(FolioScene scene) =>
        [.. scene.Services.GetRequiredService<IIndexEditing>().Indexes(scene.Document).Single().Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text)];

    private static void InsertContents(FolioScene scene)
    {
        var refresh = scene.Services.GetRequiredService<IFolioFieldRefresh>();
        var indexes = scene.Services.GetRequiredService<IIndexEditing>();
        scene.Editor.PlaceCaret(new DocumentPosition("intro", 0), false);
        scene.Editor.Change(() => indexes.Insert(scene.Document, scene.History, scene.Editor.Caret, new IndexDefinition(IndexKind.Contents) { Title = "Indice" }, refresh.Facts(scene.Editor)));
        refresh.UpdateAll(scene.Editor);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task IlSommarioPrendeLePagineVereESiAggiornaInUnPasso() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(Book);
        InsertContents(scene);

        var lines = Lines(scene);
        Assert.Equal("Indice", lines[0]);
        Assert.Equal("Primo capitolo\t1", lines[1]);
        Assert.NotEqual("Secondo capitolo\t1", lines[2]);
        Assert.StartsWith("Secondo capitolo\t", lines[2], StringComparison.Ordinal);

        scene.Editor.Change(() => scene.Document.Body.Add(new ParagraphBlock("h3", "Terzo", DefaultStyleNames.HeadingLevel(2))));
        scene.Services.GetRequiredService<IFolioFieldRefresh>().UpdateAll(scene.Editor);
        Assert.Equal(4, Lines(scene).Count);
        scene.History.Undo();
        Assert.Equal(3, Lines(scene).Count);
    }, CancellationToken.None);

    [Fact]
    public Task NellIndiceNonSiScrive() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(Book);
        InsertContents(scene);
        var entry = scene.Services.GetRequiredService<IIndexEditing>().Indexes(scene.Document).Single().Content.Blocks.OfType<ParagraphBlock>().Last();
        scene.Editor.PlaceCaret(new DocumentPosition(entry.Id, 2), false);
        var before = entry.Text;

        Assert.False(scene.Editor.Type("x"));
        Assert.False(scene.Editor.Backspace());
        Assert.False(scene.Editor.SplitParagraph());
        Assert.Equal(before, entry.Text);
    }, CancellationToken.None);

    [Fact]
    public Task IlNavigatoreSaltaESpostaICapitoli() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(Book);
        var context = scene.Services.GetRequiredService<IActiveFolioContext>();
        Assert.NotNull(context.Target);
        var source = scene.Services.GetRequiredService<FolioNavigatorSource>();

        var headings = source.Items("Headings");
        Assert.Equal(["Primo capitolo", "Secondo capitolo"], headings.Select(item => item.Title));
        Assert.All(headings, item => Assert.True(item.Movable));
        source.Go("Headings", "h2");
        Assert.Equal("h2", scene.Editor.Caret.ParagraphId);

        Assert.True(source.CanDrop("Headings", "h2", "h1"));
        source.Drop("Headings", "h2", "h1");
        Assert.Equal(["intro", "h2", "t2", "h1", "t1"], scene.Document.Body.Blocks.Select(block => block.Id));
        source.Move("Headings", "h2", NavigatorMove.Demote);
        Assert.Equal(DefaultStyleNames.HeadingLevel(2), ((ParagraphBlock)scene.Document.Body.Blocks[1]).StyleName);
        scene.History.Undo();
        scene.History.Undo();
        Assert.Equal(["intro", "h1", "t1", "h2", "t2"], scene.Document.Body.Blocks.Select(block => block.Id));
    }, CancellationToken.None);

    [Fact]
    public Task IlCollegamentoInternoPortaAllIntestazione() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(Book);
        Assert.True(scene.Services.GetRequiredService<IFolioNavigation>().Follow(scene.Editor, "#Secondo capitolo|outline"));
        Assert.Equal("h2", scene.Editor.Caret.ParagraphId);
        Assert.False(scene.Services.GetRequiredService<IFolioNavigation>().Follow(scene.Editor, "#nessuno"));
    }, CancellationToken.None);

    [Fact]
    public Task IlNumeroDiPagineNelTestoSiAggiornaConLaPaginazioneVera() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document =>
        {
            Book(document);
            document.Body.Insert(0, new ParagraphBlock("count", new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.TextBody }.WithRuns(
                [new TextRun("Pagine: "), new TextRun("?") { Field = TextFieldKind.WordCount }])));
        });
        scene.Services.GetRequiredService<IFolioFieldRefresh>().UpdateFields(scene.Editor);
        Assert.Matches(@"^Pagine: \d+$", ((ParagraphBlock)scene.Document.Body.Blocks[0]).Text);
        Assert.True(scene.History.CanUndo);
    }, CancellationToken.None);

    private static TextDocument Long(IServiceProvider services, int chapters)
    {
        var document = services.GetRequiredService<ITextDocumentFactory>().Create();
        var blocks = new List<Block> { new ParagraphBlock("top", string.Empty, DefaultStyleNames.TextBody) };
        for (var chapter = 0; chapter < chapters; chapter++)
        {
            blocks.Add(new ParagraphBlock("h" + chapter, "Capitolo " + chapter, DefaultStyleNames.HeadingLevel(1 + chapter % 3)));
            var paragraph = new ParagraphBlock("p" + chapter, new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.TextBody }.WithRuns(
            [
                new TextRun(string.Join(' ', Enumerable.Repeat("parola", 120)) + " "),
                new TextRun("?") { Field = TextFieldKind.Title },
                new TextRun(" vedi "),
                new TextRun("?") { Field = TextFieldKind.Bookmark, FieldArgument = "b" + (chapter / 2), FieldDetail = new FieldDetail { Format = "page" } },
                new TextRun(" e "),
                new TextRun("?") { Field = TextFieldKind.WordCount },
            ]));
            paragraph.Marks =
            [
                new Documents.Marks.BookmarkMark(0, "b" + chapter, Documents.Marks.MarkEdge.Point),
                new Documents.Marks.NoteMark(10, "fn" + chapter),
                new Documents.Marks.IndexEntryMark(0, "voce" + chapter % 40, Documents.Marks.MarkEdge.Point),
            ];
            document.Notes.Add(new Documents.Annotations.Note("fn" + chapter, Documents.Annotations.NoteKind.Footnote,
                new BlockContainer([new ParagraphBlock("n" + chapter, "Nota " + chapter, DefaultStyleNames.Footnote)])));
            blocks.Add(paragraph);
        }
        document.Body.Replace(blocks);
        document.Properties.Title = "Lungo";
        return document;
    }

    [Fact]
    public Task UnDocumentoLungoConMoltiCampiNoteEIndiciSiAggiornaInTempi() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var document = Long(scene.Services, 150);
        var history = new PulseOffice.Document.Commands.UndoStack();
        using var editor = new FolioEditor(document, history, scene.Services.GetRequiredService<FolioEditingServices>(), () => false);
        var indexes = scene.Services.GetRequiredService<IIndexEditing>();
        var refresh = scene.Services.GetRequiredService<IFolioFieldRefresh>();
        editor.PlaceCaret(new DocumentPosition("top", 0), false);
        editor.Change(() => indexes.Insert(document, history, editor.Caret, new IndexDefinition(IndexKind.Contents), refresh.Facts(editor)));
        editor.Change(() => indexes.Insert(document, history, editor.Caret, new IndexDefinition(IndexKind.Alphabetical), refresh.Facts(editor)));
        var pages = editor.Layout.Pages.Count;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        refresh.UpdateAll(editor);
        var update = watch.Elapsed.TotalMilliseconds;
        Assert.True(pages > 40, $"pages: {pages}");
        Assert.True(update < 8000, $"update all took {update:F0} ms on {pages} pages");
        var body = (ParagraphBlock)document.Body.Blocks.First(block => block.Id == "p100");
        Assert.Contains("Lungo", body.Text, StringComparison.Ordinal);
        Assert.Matches(@"vedi \d+ e \d+$", body.Text);

        editor.PlaceCaret(new DocumentPosition("p140", 3), false);
        var keys = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            watch.Restart();
            editor.Type("x");
            _ = editor.Layout;
            keys.Add(watch.Elapsed.TotalMilliseconds);
        }
        var typing = keys.Order().ElementAt(keys.Count / 2);
        Assert.True(typing < 400, $"typing took {typing:F0} ms per key on {pages} pages after update {update:F0} ms");
    }, CancellationToken.None);

    [Fact]
    public Task InSolaLetturaNienteSiAggiornaNeSiSposta() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document =>
        {
            Book(document);
            document.Body.Insert(0, new ParagraphBlock("count", new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.TextBody }.WithRuns(
                [new TextRun("?") { Field = TextFieldKind.WordCount }])));
        });
        scene.ReadOnly = true;
        Assert.False(scene.Services.GetRequiredService<IFolioFieldRefresh>().UpdateAll(scene.Editor));
        Assert.Equal("?", ((ParagraphBlock)scene.Document.Body.Blocks[0]).Text);
        var source = scene.Services.GetRequiredService<FolioNavigatorSource>();
        Assert.False(source.CanMove("Headings", "h1", NavigatorMove.Down));
        Assert.False(source.CanDrop("Headings", "h2", "h1"));
        Assert.False(scene.History.CanUndo);
    }, CancellationToken.None);
}
