using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class FuzzTests
{
    private string Signature(DocumentLayout layout) => string.Join('|', layout.Pages.Select(page =>
        $"{page.Index}:{page.StyleName}:{page.Start}:" + string.Join(',', page.Lines.Select(line => $"{line.ParagraphId}/{line.Line.Start}-{line.Line.End}@{line.Top:F3}"))));

    private string Text(Random random, int words) =>
        string.Join(random.Next(5) == 0 ? "\t" : " ", Enumerable.Range(0, words).Select(_ => new string((char)('a' + random.Next(26)), random.Next(1, 14))));

    private TextParagraph Shape(Random random, TextParagraph paragraph) => paragraph with
    {
        KeepWithNext = random.Next(6) == 0,
        KeepTogether = random.Next(8) == 0,
        Widows = random.Next(4),
        Orphans = random.Next(4),
        PageBreakBefore = random.Next(25) == 0,
        SpaceBefore = random.Next(3) * 4,
        SpaceAfter = random.Next(3) * 5,
        LineSpacing = 1 + random.Next(3) * 0.25,
        Explicit = ParagraphOverrides.SpaceBefore | ParagraphOverrides.SpaceAfter | ParagraphOverrides.LineSpacing,
    };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void LeModificheCasualiReimpaginanoComeLaReimpaginazioneCompleta(int seed)
    {
        var random = new Random(seed);
        var f = new LayoutFixture(width: 300, height: 400);
        var count = random.Next(20, 120);
        for (var i = 0; i < count; i++) f.Add(Text(random, random.Next(0, 90)), p => Shape(random, p));
        var previous = f.Layout();
        for (var step = 0; step < 25; step++)
        {
            var body = f.Document.Body;
            switch (random.Next(4))
            {
                case 0 when body.Count > 0:
                    var block = (ParagraphBlock)body[random.Next(body.Count)];
                    block.Content = block.Content.WithRuns([new TextRun(Text(random, random.Next(0, 150)))]);
                    break;
                case 1:
                    body.Insert(random.Next(body.Count + 1), new ParagraphBlock($"n{seed}-{step}", Shape(random, new TextParagraph(Text(random, random.Next(0, 60))))));
                    break;
                case 2 when body.Count > 1:
                    body.RemoveAt(random.Next(body.Count));
                    break;
                default:
                    if (body.Count == 0) break;
                    var shaped = (ParagraphBlock)body[random.Next(body.Count)];
                    shaped.Content = Shape(random, shaped.Content);
                    break;
            }
            var incremental = f.Engine.Relayout(f.Document, previous);
            f.Engine.Invalidate();
            var full = f.Engine.Layout(f.Document);
            Assert.Equal(Signature(full), Signature(incremental));
            Assert.All(full.Pages.SelectMany(page => page.Lines), line => Assert.True(double.IsFinite(line.Top)));
            previous = incremental;
        }
    }

    [Fact]
    public void UnDocumentoVuotoHaUnaPagina()
    {
        var f = new LayoutFixture();
        Assert.NotEmpty(f.Layout().Pages);
    }

    [Fact]
    public void UnaParolaPiuLargaDellaPaginaNonBloccaLImpaginazione()
    {
        var f = new LayoutFixture(width: 20, height: 40);
        f.Add(new string('m', 5000));
        var layout = f.Layout();
        Assert.True(layout.Pages.Count > 0);
        Assert.Equal(5000, f.Lines(layout).Max(line => line.Line.End));
    }

    [Fact]
    public void UnaPaginaPiuBassaDiUnaRigaNonBloccaLImpaginazione()
    {
        var f = new LayoutFixture(width: 200, height: 1);
        f.Add(Text(new Random(3), 200), p => p with { Widows = 3, Orphans = 3, KeepTogether = true });
        f.Add(Text(new Random(4), 200), p => p with { KeepWithNext = true });
        var layout = f.Layout();
        Assert.True(layout.Pages.Count < 2000);
    }
}
