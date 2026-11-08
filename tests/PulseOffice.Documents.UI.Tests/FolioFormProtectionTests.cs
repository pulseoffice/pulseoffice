using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.UI.Controls.Objects;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioFormProtectionTests
{
    private static FolioContentControls Controls(FolioScene scene, Dictionary<string, object> answer) => new(
        new AnsweringForms(answer),
        scene.Services.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
        null!,
        scene.Services.GetRequiredService<IFolioImages>(),
        scene.Services.GetRequiredService<FolioControlServices>(),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<FolioContentControls>.Instance);

    private static IReadOnlyList<ContentControlSpan> Spans(FolioScene scene) => scene.Services.GetRequiredService<IContentControlCatalog>().All(scene.Document);

    private static ContentControlSpan Span(FolioScene scene, ContentControlKind kind) => Spans(scene).Single(span => span.Control.Kind == kind);

    private static FolioScene Form()
    {
        var scene = new FolioScene();
        scene.Type("Nome:  accetto ");
        var id = scene.Editor.Caret.ParagraphId;
        var controls = Controls(scene, new() { ["placeholder"] = "Scrivi qui" });
        scene.Editor.PlaceCaret(new DocumentPosition(id, 6), false);
        Assert.True(controls.InsertAsync(scene.Editor, ContentControlKind.PlainText, CancellationToken.None).GetAwaiter().GetResult());
        scene.Editor.PlaceCaret(new DocumentPosition(id, scene.Paragraphs[0].Length), false);
        Assert.True(controls.InsertAsync(scene.Editor, ContentControlKind.CheckBox, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal("Nome: Scrivi qui accetto ☐", scene.Paragraphs[0]);
        scene.Document.Protection = new DocumentProtection(DocumentRestriction.Forms, null);
        return scene;
    }

    [Fact]
    public Task ConLaProtezioneModuliSiCompilaIlControlloDiTesto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Form();
        var text = Span(scene, ContentControlKind.PlainText);
        scene.Editor.PlaceCaret(new DocumentPosition(text.Start.ParagraphId, text.Start.Offset + 2), false);

        scene.Type("Ada");

        Assert.Equal("Nome: Ada accetto ☐", scene.Paragraphs[0]);
        var filled = Span(scene, ContentControlKind.PlainText);
        Assert.Equal("Ada", scene.Paragraphs[0][filled.Start.Offset..filled.End.Offset]);

        scene.Editor.PlaceCaret(filled.End, false);
        scene.Press(Avalonia.Input.Key.Back);
        Assert.Equal("Nome: Ad accetto ☐", scene.Paragraphs[0]);
    }, CancellationToken.None);

    [Fact]
    public Task ConLaProtezioneModuliIlTestoFuoriDaiControlliRestaBloccato() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Form();
        var id = scene.Editor.Caret.ParagraphId;
        var before = scene.Paragraphs[0];

        scene.Editor.PlaceCaret(new DocumentPosition(id, 2), false);
        scene.Type("X");
        Assert.Equal(before, scene.Paragraphs[0]);

        var text = Span(scene, ContentControlKind.PlainText);
        scene.Editor.PlaceCaret(text.Start, false);
        scene.Press(Avalonia.Input.Key.Back);
        Assert.Equal(before, scene.Paragraphs[0]);

        scene.Editor.PlaceCaret(text.End, false);
        scene.Press(Avalonia.Input.Key.Delete);
        Assert.Equal(before, scene.Paragraphs[0]);

        scene.Editor.Select(new DocumentPosition(id, 0), new DocumentPosition(id, text.Start.Offset + 3));
        scene.Type("Y");
        Assert.Equal(before, scene.Paragraphs[0]);

        scene.Editor.PlaceCaret(new DocumentPosition(id, text.Start.Offset + 2), false);
        Assert.False(scene.Editor.SplitParagraph());
        Assert.Single(scene.Paragraphs);

        scene.Editor.Select(new DocumentPosition(id, text.Start.Offset + 1), new DocumentPosition(id, text.Start.Offset + 4));
        Assert.False(scene.Editor.Toggle(CharacterToggle.Bold));
        Assert.Equal(before, scene.Paragraphs[0]);
    }, CancellationToken.None);

    [Fact]
    public Task ConLaProtezioneModuliLaCasellaSiSpuntaMaIlControlloNonSiTocca() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Form();
        var controls = Controls(scene, new() { ["title"] = "Cambiato", ["contentLocked"] = false, ["controlLocked"] = false });
        var box = Span(scene, ContentControlKind.CheckBox);

        Assert.True(controls.Activate(scene.Editor, box.Start));
        Assert.True(Span(scene, ContentControlKind.CheckBox).Control.Checked);
        Assert.EndsWith("☒", scene.Paragraphs[0], StringComparison.Ordinal);

        scene.Editor.PlaceCaret(box.Start, false);
        Assert.False(controls.Remove(scene.Editor));
        Assert.False(controls.PropertiesAsync(scene.Editor, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal(2, Spans(scene).Count);
        Assert.Null(Span(scene, ContentControlKind.CheckBox).Control.Title);
        Assert.False(controls.InsertAsync(scene.Editor, ContentControlKind.PlainText, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal(2, Spans(scene).Count);
    }, CancellationToken.None);

    [Fact]
    public Task LaSagomaDelModuloCambiaSoloFuoriDaiControlli() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Form();
        var filling = scene.Services.GetRequiredService<IFormFilling>();
        var outline = filling.Outline(scene.Document);
        var text = Span(scene, ContentControlKind.PlainText);

        scene.Editor.PlaceCaret(new DocumentPosition(text.Start.ParagraphId, text.Start.Offset + 2), false);
        scene.Type("Ada");
        Assert.True(Controls(scene, []).Activate(scene.Editor, Span(scene, ContentControlKind.CheckBox).Start));
        Assert.True(outline.Matches(filling.Outline(scene.Document)));

        scene.Document.Protection = DocumentProtection.None;
        scene.Editor.PlaceCaret(new DocumentPosition(text.Start.ParagraphId, 1), false);
        scene.Type("Z");
        Assert.False(outline.Matches(filling.Outline(scene.Document)));
    }, CancellationToken.None);

    [Fact]
    public Task LaProtezioneModuliAttraversaOdtEDocxConIControlli() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Form();
        var composer = new DocxComposer(new PulseOffice.Documents.Styles.DirectFormatting(), new PulseOffice.Documents.Styles.DefaultStyleSheetFactory());
        foreach (var (writer, reader) in new (Formats.ITextDocumentWriter, Formats.ITextDocumentReader)[]
                 {
                     (new OdtWriter(), new OdtReader()),
                     (composer.Writer(DocxFlavour.Document), composer.Reader(DocxFlavour.Document)),
                 })
        {
            using var stream = new MemoryStream();
            Assert.True(writer.Write(scene.Document, stream).Succeeded);
            stream.Position = 0;
            var read = reader.Read(stream);
            Assert.True(read.Succeeded);
            var document = read.Value!;
            Assert.Equal(DocumentRestriction.Forms, document.Protection.Restriction);
            var kinds = scene.Services.GetRequiredService<IContentControlCatalog>().All(document).Select(span => span.Control.Kind).ToList();
            Assert.Contains(ContentControlKind.PlainText, kinds);
            Assert.Contains(ContentControlKind.CheckBox, kinds);
        }
    }, CancellationToken.None);
}
