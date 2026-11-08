using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Localization;
using PulseOffice.Text.Redaction;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioAutoRedactionTests
{
    private const string Secret = "Mario Rossi";

    private static readonly CommandContext Context = new(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen);

    private static void AutoRedact(FolioScene scene)
    {
        var services = scene.Services;
        var commands = new FolioTextToolCommands(services.GetRequiredService<IActiveFolioContext>(), null!, new AgreeingRedaction(), new TextRedactor(),
            new SyllableHyphenation(), services.GetRequiredService<IFolioLanguages>(), new AnsweringForms(new Dictionary<string, object>()),
            services.GetRequiredService<IStatusBarService>(), services.GetRequiredService<ITextCatalog>(), services.GetRequiredService<IMetadataRedaction>(), services.GetRequiredService<IDirectDocumentEditor>());
        commands.GetCommands().Single(command => command.Id.Value == "folio.tools.autoRedact").Execute(Context, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static FolioScene Prepared()
    {
        var scene = new FolioScene();
        scene.Type("Contratto riservato");
        var id = scene.Editor.Caret.ParagraphId;
        scene.Editor.Apply((documents, range) => documents.AddBookmark(scene.Document, scene.History, range, "Clausola_Mario_Rossi"), DocumentRange.In(id, 0, 9));
        scene.Editor.Apply((documents, range) => documents.AddComment(scene.Document, scene.History, range, Secret, DateTimeOffset.Now, "Da rivedere"), DocumentRange.In(id, 10, 19));
        var paragraph = scene.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().First();
        List<TextRun> runs =
        [
            .. paragraph.Content.EffectiveRuns(),
            new TextRun(" vedi "),
            new TextRun("sopra") { Field = TextFieldKind.Reference, FieldArgument = "Clausola_Mario_Rossi" },
            new TextRun(" link") { Hyperlink = "#Clausola_Mario_Rossi" },
        ];
        paragraph.State = paragraph.State with { Content = paragraph.Content.WithRuns(runs) };
        scene.Editor.Select(new DocumentPosition(id, paragraph.Length), new DocumentPosition(id, paragraph.Length));
        scene.Document.Changes.IsRecording = true;
        scene.Type(" aggiunto");
        scene.Document.Changes.IsRecording = false;
        foreach (var change in scene.Document.Changes.All) change.Author = Secret;
        var properties = scene.Document.Properties;
        properties.Title = "Accordo " + Secret;
        properties.Subject = "Fornitura per " + Secret;
        properties.Description = "Commento di " + Secret;
        properties.Author = Secret;
        properties.LastModifiedBy = Secret;
        properties.Keywords.Add(Secret);
        properties.Custom["Cliente"] = Secret;
        properties.Custom["Pagine"] = "12";
        properties.CustomKinds["Pagine"] = PulseOffice.Document.Metadata.CustomPropertyKind.Number;
        return scene;
    }

    private static IEnumerable<string> Bookmarks(FolioScene scene) =>
        scene.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().SelectMany(paragraph => paragraph.Marks).OfType<BookmarkMark>().Select(mark => mark.Name).Distinct();

    [Fact]
    public Task LOscuramentoAutomaticoCopreProprietaSegnalibriEAutoriInUnPasso() => FolioScene.RunAsync(() =>
    {
        var scene = Prepared();
        var properties = scene.Document.Properties;

        AutoRedact(scene);

        Assert.DoesNotContain(Secret, properties.Title ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, properties.Subject ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, properties.Description ?? string.Empty, StringComparison.Ordinal);
        Assert.NotEqual(Secret, properties.Author);
        Assert.NotEqual(Secret, properties.LastModifiedBy);
        Assert.Empty(properties.Keywords);
        Assert.Equal(string.Empty, properties.Custom["Cliente"]);
        Assert.Equal("12", properties.Custom["Pagine"]);
        var bookmark = Assert.Single(Bookmarks(scene));
        Assert.DoesNotContain("Mario", bookmark, StringComparison.Ordinal);
        var runs = scene.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().First().Content.Runs;
        Assert.Contains(runs, run => run.Field == TextFieldKind.Reference && run.FieldArgument == bookmark);
        Assert.Contains(runs, run => run.Hyperlink == "#" + bookmark);
        Assert.All(scene.Document.Comments.All, comment => Assert.NotEqual(Secret, comment.Author));
        Assert.NotEmpty(scene.Document.Changes.All);
        Assert.All(scene.Document.Changes.All, change => Assert.NotEqual(Secret, change.Author));
        var texts = scene.Services.GetRequiredService<ITextCatalog>();
        var status = scene.Services.GetRequiredService<IStatusBarService>();
        Assert.Contains(status.Segments, segment => segment.Text == texts.Format("redact.folio.auto.done", 0, 7, 1, 1, 0, 0));

        scene.History.Undo();

        Assert.Equal("Accordo " + Secret, properties.Title);
        Assert.Equal(Secret, properties.Author);
        Assert.Equal(Secret, properties.LastModifiedBy);
        Assert.Equal([Secret], properties.Keywords);
        Assert.Equal(Secret, properties.Custom["Cliente"]);
        Assert.Equal(["Clausola_Mario_Rossi"], Bookmarks(scene));
        Assert.Contains(scene.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().First().Content.Runs, run => run.FieldArgument == "Clausola_Mario_Rossi");
        Assert.All(scene.Document.Comments.All, comment => Assert.Equal(Secret, comment.Author));
        Assert.All(scene.Document.Changes.All, change => Assert.Equal(Secret, change.Author));
        return Task.CompletedTask;
    });

    [Fact]
    public Task IDatiOscuratiNonSopravvivonoAlSalvataggioInOdtEDocx() => FolioScene.RunAsync(() =>
    {
        var scene = Prepared();

        AutoRedact(scene);

        var composer = new DocxComposer(new PulseOffice.Documents.Styles.DirectFormatting(), new PulseOffice.Documents.Styles.DefaultStyleSheetFactory());
        foreach (var (writer, reader) in new (Formats.ITextDocumentWriter, Formats.ITextDocumentReader)[]
                 {
                     (new OdtWriter(), new OdtReader()),
                     (composer.Writer(DocxFlavour.Document), composer.Reader(DocxFlavour.Document)),
                 })
        {
            using var stream = new MemoryStream();
            Assert.True(writer.Write(scene.Document, stream).Succeeded);
            var bytes = stream.ToArray();
            using (var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
                foreach (var entry in archive.Entries)
                {
                    using var content = new StreamReader(entry.Open(), Encoding.UTF8);
                    var text = content.ReadToEnd();
                    Assert.False(text.Contains("Mario", StringComparison.Ordinal), entry.FullName + " still holds the redacted name");
                    Assert.False(text.Contains("Rossi", StringComparison.Ordinal), entry.FullName + " still holds the redacted name");
                }
            var read = reader.Read(new MemoryStream(bytes));
            Assert.True(read.Succeeded);
            var document = read.Value!;
            Assert.DoesNotContain(Secret, document.Properties.Title ?? string.Empty, StringComparison.Ordinal);
            Assert.All(document.Comments.All, comment => Assert.NotEqual(Secret, comment.Author));
        }
        return Task.CompletedTask;
    });

    [Fact]
    public Task Auto_redaction_reaches_tracked_deletions_and_drops_saved_versions() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("Contratto");
        var document = scene.Document;
        var removed = new PulseOffice.Documents.Blocks.BlockContainer([new PulseOffice.Documents.Blocks.ParagraphBlock(document.NextId("p"), "Scrivere a mario.rossi@example.com")]);
        document.Changes.Add(new Deletion("d1", "Anna", DateTimeOffset.UnixEpoch, removed));
        document.Versions.Add([1, 2, 3], Secret, "Prima stesura");

        AutoRedact(scene);

        var deletion = Assert.IsType<Deletion>(Assert.Single(document.Changes.All));
        var text = string.Concat(deletion.Removed.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().Select(paragraph => paragraph.Text));
        Assert.DoesNotContain("@example.com", text, StringComparison.Ordinal);
        Assert.StartsWith("Scrivere a", text, StringComparison.Ordinal);
        Assert.Equal(0, document.Versions.Count);

        scene.History.Undo();

        var restored = Assert.IsType<Deletion>(Assert.Single(document.Changes.All));
        Assert.Contains("mario.rossi@example.com", string.Concat(restored.Removed.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().Select(paragraph => paragraph.Text)), StringComparison.Ordinal);
        Assert.Equal(Secret, Assert.Single(document.Versions.All).Author);
        return Task.CompletedTask;
    });
}
