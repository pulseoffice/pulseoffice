using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round244FolioFormFieldMarkTests
{
    [Fact]
    public Task AFolioPlainTextControlIsMarkedAsATextFormFieldWithItsShownText() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Nome: Ada");
        var paragraph = scene.Editor.Caret.ParagraphId;
        scene.Services.GetRequiredService<IContentControlEditing>().Insert(scene.Document, scene.History,
            new DocumentRange(new DocumentPosition(paragraph, 6), new DocumentPosition(paragraph, 9)), new ContentControl { Kind = ContentControlKind.PlainText, Title = "Nome" });
        var page = scene.Services.GetRequiredService<IDocumentLayoutEngine>().Layout(scene.Document).Pages[0];
        var marks = scene.Services.GetRequiredService<IFolioPrintMarks>();
        var canvas = new FieldRecorder();

        marks.Mark(canvas, page, marks.Index(scene.Document));

        var (area, field) = Assert.Single(canvas.Fields);
        Assert.Equal(PrintFormFieldKind.Text, field.Kind);
        Assert.Equal("Nome", field.Name);
        Assert.Equal("Ada", field.Value);
        Assert.True(area.Width.ToPoints() > 0);
    }, CancellationToken.None);

    [Fact]
    public Task Round248AFolioRichTextControlOverTwoParagraphsIsOneMultiLineTextField() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Uno");
        var first = scene.Editor.Caret.ParagraphId;
        scene.Editor.SplitParagraph();
        scene.Type("Due");
        var second = scene.Editor.Caret.ParagraphId;
        scene.Services.GetRequiredService<IContentControlEditing>().Insert(scene.Document, scene.History,
            new DocumentRange(new DocumentPosition(first, 0), new DocumentPosition(second, 3)), new ContentControl { Kind = ContentControlKind.RichText, Title = "Note" });
        var page = scene.Services.GetRequiredService<IDocumentLayoutEngine>().Layout(scene.Document).Pages[0];
        var marks = scene.Services.GetRequiredService<IFolioPrintMarks>();
        var canvas = new FieldRecorder();

        marks.Mark(canvas, page, marks.Index(scene.Document));

        var (_, field) = Assert.Single(canvas.Fields);
        Assert.Equal(PrintFormFieldKind.Text, field.Kind);
        Assert.True(field.MultiLine);
        Assert.Equal("Uno\nDue", field.Value);
    }, CancellationToken.None);

    private sealed class FieldRecorder : IPageCanvas
    {
        public List<PrintMarkedField> Fields { get; } = [];

        public DocumentSize PageSize { get; } = new(Twip.FromPoints(600), Twip.FromPoints(800));

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints(text.Length * 6);

        public void FillRectangle(DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle)
        {
        }

        public void DrawPath(PrintPath path)
        {
        }

        public void DrawImage(PageImage image, DocumentRect bounds)
        {
        }

        public void MarkField(DocumentRect area, PrintFormField field) => Fields.Add(new PrintMarkedField(area, field));
    }
}
