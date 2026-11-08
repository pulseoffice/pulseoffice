using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using Xunit;
using LayoutRect = PulseOffice.Documents.Layout.Tree.LayoutRect;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioShapeStoryPaintTests
{
    [Fact]
    public Task LaFormaDellaGalleriaSiDisegnaColorataEColTestoAlCentro() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        var objects = scene.Services.GetRequiredService<IFolioObjectEditing>();
        Assert.True(objects.Insert(scene.Editor, new ObjectInsertion(new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Text = "Ciao" },
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)), ObjectAnchorKind.Paragraph, WrapMode.Parallel)));
        Dispatcher.UIThread.RunJobs();
        var item = scene.Document.Objects.All.Single();
        Assert.NotNull(item.Content.Story);
        Assert.Equal(PulseOffice.Text.Rich.TextAlign.Center, ((PulseOffice.Documents.Blocks.ParagraphBlock)item.Content.Story![0]).Content.Align);
        var recording = new RecordingSurface();

        scene.Services.GetRequiredService<IPageRenderer>().Paint(recording, scene.Editor.Layout.Pages[0],
            id => scene.Document.Objects.Find(id)?.Content is EmbeddedContent embedded ? embedded.Value : null);

        var (content, bounds) = Assert.Single(recording.Objects);
        var shape = Assert.IsType<ShapeObject>(content);
        Assert.True(shape.Style.Fill.IsVisible);
        Assert.Equal("#2563EB", shape.Style.Fill.ColorHex);
        Assert.Contains("Ciao", recording.Texts.Select(text => text.Text));
        var box = scene.Editor.Layout.Pages[0].Objects.Single();
        var line = Assert.Single(box.Lines);
        Assert.InRange(line.Top + line.Line.Height / 2 - (bounds.Y + bounds.Height / 2), -1, 1);
    }, CancellationToken.None);

    [Fact]
    public Task IlTestoDiUnaFormaRuotataRuotaConLaForma() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        var objects = scene.Services.GetRequiredService<IFolioObjectEditing>();
        Assert.True(objects.Insert(scene.Editor, new ObjectInsertion(new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Text = "Ciao" },
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)), ObjectAnchorKind.Paragraph, WrapMode.Parallel)));
        Dispatcher.UIThread.RunJobs();
        objects.Rotate(scene.Editor, 90, relative: false);
        Dispatcher.UIThread.RunJobs();
        var recording = new RecordingSurface();

        scene.Services.GetRequiredService<IPageRenderer>().Paint(recording, scene.Editor.Layout.Pages[0],
            id => scene.Document.Objects.Find(id)?.Content is EmbeddedContent embedded ? embedded.Value : null);

        Assert.Equal([90.0], recording.Turns);
        Assert.Equal(0, recording.Depth);
        Assert.False(recording.EmbeddedWhileTurned);
        Assert.Contains(recording.TurnedTexts, text => text == "Ciao");
        Assert.DoesNotContain(recording.TurnedTexts, text => text == "Testo");
    }, CancellationToken.None);

    [Fact]
    public void LaSvoltaDiUnOggettoRuotaIlPuntoAttornoAlCentro()
    {
        var (x, y) = new ObjectTurn(10, 10, 90).Apply(20, 10);

        Assert.Equal(10, x, 6);
        Assert.Equal(20, y, 6);
    }

    private sealed class RecordingSurface : IPageSurface
    {
        private readonly Stack<double> _open = new();

        public List<double> Turns { get; } = [];

        public List<string> TurnedTexts { get; } = [];

        public int Depth => _open.Count;

        public bool EmbeddedWhileTurned { get; private set; }

        public void BeginObject(LayoutRect bounds, double rotationDegrees)
        {
            _open.Push(rotationDegrees);
            Turns.Add(rotationDegrees);
        }

        public void EndObject() => _open.Pop();

        public List<(string Text, double Baseline)> Texts { get; } = [];

        public List<(EmbeddedObject Content, LayoutRect Bounds)> Objects { get; } = [];

        public void Fill(LayoutRect rect, uint argb)
        {
        }

        public void Line(double x1, double y1, double x2, double y2, uint argb, double thickness, PageStroke stroke = PageStroke.Solid)
        {
        }

        public void Frame(LayoutRect rect, uint argb, double thickness, PageStroke stroke = PageStroke.Solid)
        {
        }

        public void Glyphs(PulseOffice.Documents.Layout.Tree.GlyphRun run, double x, double baseline, uint argb)
        {
            Texts.Add((run.Text, baseline));
            if (_open.Any(degrees => degrees != 0)) TurnedTexts.Add(run.Text);
        }

        public void RotatedGlyphs(PulseOffice.Documents.Layout.Tree.GlyphRun run, double x, double baseline, uint argb, double degrees) => Texts.Add((run.Text, baseline));

        public void Embedded(EmbeddedObject content, LayoutRect bounds, ObjectTransform transform)
        {
            EmbeddedWhileTurned |= _open.Any(degrees => degrees != 0);
            Objects.Add((content, bounds));
        }
    }
}
