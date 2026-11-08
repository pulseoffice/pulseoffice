using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class ObjectFlowTests
{
    private string Words(int count) => string.Join(' ', Enumerable.Range(0, count).Select(_ => "aaaaaaaaa"));

    private AnchoredObject Obstacle(string id, string paragraphId, double x, double width, double height) =>
        new(id, new ParagraphAnchor(paragraphId), new TextBoxContent(new BlockContainer()), new DocumentSize(Twip.FromPoints(width), Twip.FromPoints(height)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(x), Twip.Zero),
            Wrap = WrapMode.Parallel,
        };

    [Fact]
    public void LaDistanzaDallOggettoAllontanaIlTesto()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var block = f.Add(Words(12));
        var item = Obstacle("img", block.Id, 80, 40, 30);
        item.WrapSpacing = new Margins(Twip.FromPoints(10), Twip.Zero, Twip.FromPoints(10), Twip.FromPoints(12));
        f.Document.Objects.Add(item);

        var layout = f.Layout();
        var lines = layout.Pages[0].Lines;

        Assert.Equal(new LayoutRect(80, 0, 40, 30), layout.Pages[0].Objects.Single().Bounds);
        Assert.Contains(lines, line => line.Top == 0 && line.Left == 130);
        Assert.All(lines.Where(line => line.Top < 42 && line.Left == 0), line => Assert.True(line.Line.Runs.Where(run => run.Kind == GlyphRunKind.Text).Sum(run => run.Width) <= 70 + 0.001));
        Assert.DoesNotContain(lines, line => line.Top >= 30 && line.Top < 42 && line.Left == 0 && line.Line.Width > 150);
    }

    [Fact]
    public void IlTestoDiUnaCorniceVieneImpaginato()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var block = f.Add("testo");
        var content = new BlockContainer([new ParagraphBlock("fp", new TextParagraph(Words(6)) { Runs = [new TextRun(Words(6))] })]);
        f.Document.Objects.Add(new AnchoredObject("frame", new ParagraphAnchor(block.Id), new TextBoxContent(content), new DocumentSize(Twip.FromPoints(60), Twip.FromPoints(12)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(100), Twip.Zero),
            Wrap = WrapMode.Through,
        });

        var layout = f.Layout();
        var box = layout.Pages[0].Objects.Single();

        Assert.Equal(6, box.Lines.Count());
        Assert.Equal(72, box.Bounds.Height, 3);
        Assert.All(box.Lines, line => Assert.True(line.Left >= 100 - 0.001 && line.Top >= -0.001));
        Assert.Contains(layout.Pages[0].AllLines, line => line.ParagraphId == "fp");
    }

    [Fact]
    public void LeOrfaneValgonoAncheAccantoAUnOggetto()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        var first = f.Add("riga");
        f.Document.Objects.Add(Obstacle("img", first.Id, 160, 40, 60));
        f.Add("riga");
        f.Add("riga");
        var block = f.Add(Words(30), p => p with { Orphans = 3, Widows = 1 });

        var layout = f.Layout();

        Assert.Equal(3, layout.Pages[0].Lines.Count);
        Assert.Equal(block.Id, layout.Pages[1].Lines[0].ParagraphId);
    }

    [Fact]
    public void LeVedoveValgonoAncheAccantoAUnOggetto()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        var first = f.Add("riga");
        f.Document.Objects.Add(Obstacle("img", first.Id, 160, 40, 60));
        var block = f.Add(Words(10), p => p with { Orphans = 1, Widows = 3 });

        var layout = f.Layout();
        var all = layout.Pages.SelectMany(page => page.Lines.Where(line => line.ParagraphId == block.Id).Select(_ => page.Index)).ToList();

        Assert.Equal(2, all.Count(index => index == 0));
        Assert.Equal(2, all.Count(index => index == 1));
    }

    [Fact]
    public void IlContornoAvvicinaIlTestoAllaFormaRotonda()
    {
        double Width(bool contour)
        {
            var f = new LayoutFixture(width: 200, height: 400);
            var block = f.Add(Words(40));
            var circle = new AnchoredObject("c", new ParagraphAnchor(block.Id), new EmbeddedContent(new PulseOffice.Drawing.Shapes.ShapeObject { Id = "s", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse }),
                new DocumentSize(Twip.FromPoints(100), Twip.FromPoints(100)))
            {
                Placement = new ObjectPlacement(Twip.Zero, Twip.Zero),
                Wrap = WrapMode.Right,
                WrapContour = contour,
            };
            f.Document.Objects.Add(circle);
            var lines = f.Layout().Pages[0].Lines;
            return lines.First(line => line.Top > 85 && line.Top < 100).Left;
        }

        Assert.Equal(100, Width(false), 3);
        Assert.True(Width(true) < 90);
    }

    [Fact]
    public void IlContornoDelModelloValeAncheRuotato()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var block = f.Add(Words(40));
        f.Document.Objects.Add(new AnchoredObject("t", new ParagraphAnchor(block.Id), new TextBoxContent(new BlockContainer()), new DocumentSize(Twip.FromPoints(100), Twip.FromPoints(100)))
        {
            Wrap = WrapMode.Right,
            WrapContour = true,
            Contour = new ObjectContour([new ContourPoint(0, 0), new ContourPoint(1, 0), new ContourPoint(0, 1)]),
        });

        var lines = f.Layout().Pages[0].Lines;

        Assert.True(lines.First(line => line.Top < 10).Left > 85);
        Assert.True(lines.First(line => line.Top > 80 && line.Top < 100).Left < 30);
    }

    [Fact]
    public void ConSoloIlPrimoParagrafoGliAltriVannoSottoLOggetto()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var first = f.Add("uno");
        var second = f.Add(Words(4));
        var item = Obstacle("img", first.Id, 0, 60, 60);
        item.WrapFirstParagraph = true;
        f.Document.Objects.Add(item);

        var lines = f.Layout().Pages[0].Lines;

        Assert.Equal(60, lines.Single(line => line.ParagraphId == first.Id).Left, 3);
        Assert.True(lines.First(line => line.ParagraphId == second.Id).Top >= 60 - 0.001);
        Assert.Equal(0, lines.First(line => line.ParagraphId == second.Id).Left, 3);
    }

    [Fact]
    public void IlTestoGiraIntornoAllOggettoAncheNelleProssimePagine()
    {
        var f = new LayoutFixture(width: 200, height: 100);
        for (var i = 0; i < 12; i++) f.Add("riga");
        var anchor = f.Add(Words(30));
        f.Document.Objects.Add(Obstacle("img", anchor.Id, 0, 80, 60));

        var layout = f.Layout();
        var page = layout.Pages.First(candidate => candidate.Objects.Any(box => box.ObjectId == "img"));

        Assert.True(page.Index > 0);
        Assert.Contains(page.Lines, line => line.ParagraphId == anchor.Id && Math.Abs(line.Left - 80) < 0.001);
    }

    [Fact]
    public void IlTestoScorreDaUnaCorniceAllaCollegata()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("testo");
        var content = new BlockContainer([new ParagraphBlock("fp", new TextParagraph(Words(12)) { Runs = [new TextRun(Words(12))] })]);
        f.Document.Objects.Add(new AnchoredObject("a", new ParagraphAnchor(block.Id), new TextBoxContent(content) { AutoGrowHeight = false, NextFrameName = "b" }, new DocumentSize(Twip.FromPoints(60), Twip.FromPoints(36)))
        {
            Wrap = WrapMode.Through,
        });
        f.Document.Objects.Add(new AnchoredObject("b", new ParagraphAnchor(block.Id), new TextBoxContent(new BlockContainer([new ParagraphBlock("empty", string.Empty)])), new DocumentSize(Twip.FromPoints(120), Twip.FromPoints(12)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(200), Twip.FromPoints(100)),
            Wrap = WrapMode.Through,
        });

        var page = f.Layout().Pages[0];
        var first = page.Objects.Single(box => box.ObjectId == "a");
        var second = page.Objects.Single(box => box.ObjectId == "b");

        Assert.True(first.Lines.Count() == 3, string.Join(";", first.Lines.Select(l => $"{l.Top}/{l.Line.Height}")) + " " + string.Join(";", second.Lines.Select(l => $"{l.Top}/{l.Line.Height}")));
        Assert.Equal(36, first.Bounds.Height, 3);
        Assert.NotEmpty(second.Lines);
        Assert.All(second.Lines, line => Assert.Equal("fp", line.ParagraphId));
        Assert.Equal(first.Lines.Last().Line.Next, second.Lines.First().Line.Start);
        Assert.All(second.Lines, line => Assert.True(line.Left >= 200 - 0.001));
    }

    [Fact]
    public void UnImmagineComeCarattereDentroUnaCorniceSiDisegna()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("testo");
        var inner = new ParagraphBlock("ip", new TextParagraph("x") { Runs = [new TextRun("x")] });
        f.Document.Objects.Add(new AnchoredObject("frame", new ParagraphAnchor(block.Id), new TextBoxContent(new BlockContainer([inner])), new DocumentSize(Twip.FromPoints(100), Twip.FromPoints(40))));
        f.Document.Objects.Add(new AnchoredObject("pic", new AsCharacterAnchor("ip"), new TextBoxContent(new BlockContainer()), new DocumentSize(Twip.FromPoints(30), Twip.FromPoints(20))));
        inner.State = inner.State with { Marks = [new PulseOffice.Documents.Marks.ObjectMark(0, "pic")] };

        var page = f.Layout().Pages[0];

        Assert.Contains(page.Objects, box => box.ObjectId == "pic" && box.Layer == ObjectLayer.Inline);
    }

    [Fact]
    public void LeCorniciCollegateRestanoDopoUnaModificaDelParagrafoDiAncoraggio()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("testo");
        var content = new BlockContainer([new ParagraphBlock("fp", new TextParagraph(Words(12)) { Runs = [new TextRun(Words(12))] })]);
        f.Document.Objects.Add(new AnchoredObject("a", new ParagraphAnchor(block.Id), new TextBoxContent(content) { AutoGrowHeight = false, NextFrameName = "b" }, new DocumentSize(Twip.FromPoints(60), Twip.FromPoints(36))));
        f.Document.Objects.Add(new AnchoredObject("b", new ParagraphAnchor(block.Id), new TextBoxContent(new BlockContainer([new ParagraphBlock("empty", string.Empty)])), new DocumentSize(Twip.FromPoints(120), Twip.FromPoints(12)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(200), Twip.FromPoints(100)),
        });
        var layout = f.Layout();
        Assert.Equal(2, layout.Pages[0].Objects.Count);

        block.Content = block.Content.WithRuns([new TextRun("testo cambiato")]);
        layout = f.Engine.Relayout(f.Document, layout);

        Assert.Equal(2, layout.Pages[0].Objects.Count);
        Assert.NotEmpty(layout.Pages[0].Objects.Single(box => box.ObjectId == "b").Lines);
    }
}
