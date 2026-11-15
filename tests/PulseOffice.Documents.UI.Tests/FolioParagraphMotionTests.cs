using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.UI.Editing;
using TextParagraph = PulseOffice.Text.Rich.TextParagraph;
using TextRun = PulseOffice.Text.Rich.TextRun;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioParagraphMotionTests
{
    private ParagraphBlock Paragraph(FolioScene scene, string text) =>
        new(scene.Document.NextId("p"), new TextParagraph(text) { Runs = [new TextRun(text)] });

    [Fact]
    public Task ParagraphMotionsReachTheNeighbouringParagraphStarts() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var one = Paragraph(scene, "one");
        var two = Paragraph(scene, "two two");
        var three = Paragraph(scene, "three");
        scene.Document.Body.Replace([one, two, three]);
        var layout = scene.Services.GetRequiredService<IDocumentLayoutEngine>().Layout(scene.Document);
        var navigator = scene.Services.GetRequiredService<ICaretNavigator>();
        DocumentPosition Move(DocumentPosition from, CaretMotion motion) => navigator.Move(scene.Document, layout, from, motion, null, 10).Position;

        Assert.Equal(new DocumentPosition(three.Id, 0), Move(new DocumentPosition(two.Id, 3), CaretMotion.ParagraphDown));
        Assert.Equal(new DocumentPosition(two.Id, 0), Move(new DocumentPosition(two.Id, 3), CaretMotion.ParagraphUp));
        Assert.Equal(new DocumentPosition(one.Id, 0), Move(new DocumentPosition(two.Id, 0), CaretMotion.ParagraphUp));
        Assert.Equal(new DocumentPosition(three.Id, three.Length), Move(new DocumentPosition(three.Id, 1), CaretMotion.ParagraphDown));
        Assert.Equal(new DocumentPosition(one.Id, 0), Move(new DocumentPosition(one.Id, 0), CaretMotion.ParagraphUp));
    }, CancellationToken.None);

    [Fact]
    public Task ControlAndAltArrowsMoveByParagraph() => FolioScene.Session.Dispatch(() =>
    {
        var map = new FolioScene().Services.GetRequiredService<IFolioKeyMap>();

        foreach (var modifiers in new[] { KeyModifiers.Control, KeyModifiers.Alt })
        {
            Assert.Equal(CaretMotion.ParagraphUp, Assert.IsType<MotionKeyAction>(map.Resolve(Key.Up, modifiers)).Motion);
            Assert.Equal(CaretMotion.ParagraphDown, Assert.IsType<MotionKeyAction>(map.Resolve(Key.Down, modifiers | KeyModifiers.Shift)).Motion);
        }
    }, CancellationToken.None);
}
