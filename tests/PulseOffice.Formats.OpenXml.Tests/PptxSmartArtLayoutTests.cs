using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxSmartArtLayoutTests
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sd/qa/unit/data/pptx/";

    private Presentation Read(string name)
    {
        using var stream = File.OpenRead(Corpus + name);
        return new PptxReader(new ThemeCatalog(), new MasterFactory()).Read(stream).Value!;
    }

    private IReadOnlyList<string> Texts(Slide slide) =>
        [.. slide.Elements.Select(element => element switch
        {
            TextElement text => string.Join("\n", text.Text.Paragraphs.Select(paragraph => paragraph.Text)),
            ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject { Text: { } text } } => text,
            _ => string.Empty,
        }).Where(text => text.Length > 0)];

    [Theory]
    [InlineData("smartart1.pptx", "a b c d e")]
    [InlineData("smartart-cycle.pptx", "a b c d e")]
    [InlineData("smartart-org-chart2.pptx", "A B1 B2")]
    [InlineData("smartart-chevron.pptx", "a b")]
    [InlineData("fill-color-list.pptx", "A B C")]
    public void SenzaDisegnoDiRipiegoIlDiagrammaSiImpaginaDalModelloDati(string file, string expected)
    {
        Assert.SkipWhen(!File.Exists(Corpus + file), "Reference corpus not available.");
        var slide = Read(file).Slides[0];

        Assert.NotEmpty(slide.Diagrams);
        var texts = Texts(slide);
        foreach (var text in expected.Split(' ')) Assert.Contains(texts, shown => shown.Split('\n').Contains(text));
        Assert.All(slide.Elements, element => Assert.True(element.Bounds.Width.Value > 0 && element.Bounds.Height.Value > 0));
    }

    [Fact]
    public void IlCicloDisponeINodiInCerchioConLeFrecceFraUnoELAltro()
    {
        Assert.SkipWhen(!File.Exists(Corpus + "smartart-cycle.pptx"), "Reference corpus not available.");
        var slide = Read("smartart-cycle.pptx").Slides[0];
        var nodes = slide.Elements.Where(element => Texts(slide).Count > 0 && element is TextElement or ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject { Text.Length: > 0 } }).ToList();
        var centres = nodes.Select(node => (X: node.Bounds.Left.Value + node.Bounds.Width.Value / 2.0, Y: node.Bounds.Top.Value + node.Bounds.Height.Value / 2.0)).ToList();
        var middle = (X: centres.Average(point => point.X), Y: centres.Average(point => point.Y));
        var distances = centres.Select(point => Math.Sqrt(Math.Pow(point.X - middle.X, 2) + Math.Pow(point.Y - middle.Y, 2))).ToList();

        Assert.Equal(5, nodes.Count);
        Assert.True(distances.Max() - distances.Min() < distances.Average() * 0.35);
        Assert.True(slide.Elements.Count > nodes.Count);
    }

    [Fact]
    public void ModificatoIlTestoIlDiagrammaSiRigeneraNelPptxEResta()
    {
        Assert.SkipWhen(!File.Exists(Corpus + "smartart1.pptx"), "Reference corpus not available.");
        var deck = Read("smartart1.pptx");
        var slide = deck.Slides[0];
        var member = slide.Elements.OfType<TextElement>().First(text => text.Text.Paragraphs.Any(paragraph => paragraph.Text == "c"));
        slide.Replace(member with { Text = member.Text with { Paragraphs = [new TextParagraph("Terzo passo")] } });

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        using (var document = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(buffer, false))
        {
            var part = document.PresentationPart!.SlideParts.First();
            using var data = new StreamReader(part.DiagramDataParts.First().GetStream());
            Assert.Contains("Terzo passo", data.ReadToEnd(), StringComparison.Ordinal);
            var drawing = part.Parts.Select(pair => pair.OpenXmlPart).Single(found => found.ContentType == "application/vnd.ms-office.drawingml.diagramDrawing+xml");
            using var shapes = new StreamReader(drawing.GetStream());
            Assert.Contains("Terzo passo", shapes.ReadToEnd(), StringComparison.Ordinal);
        }

        buffer.Position = 0;
        var back = new PptxReader(new ThemeCatalog(), new MasterFactory()).Read(buffer).Value!.Slides[0];
        Assert.NotEmpty(back.Diagrams);
        Assert.Contains("Terzo passo", Texts(back));
    }
}
