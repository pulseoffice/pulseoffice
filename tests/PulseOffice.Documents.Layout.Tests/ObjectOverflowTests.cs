using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class ObjectOverflowTests
{
    [Fact]
    public async Task UnaCorniceAffiancataPiuGrandeDellaPaginaNonBloccaLImpaginazione()
    {
        var f = new LayoutFixture(width: 200, height: 300);
        var block = f.Add("x xxxx");
        var content = new BlockContainer([new ParagraphBlock("fp", "dentro")]);
        f.Document.Objects.Add(new AnchoredObject("frame", new CharacterAnchor(block.Id), new TextBoxContent(content), new DocumentSize(Twip.FromPoints(220), Twip.FromPoints(350)))
        {
            Wrap = WrapMode.Parallel,
        });
        f.Add("dopo");

        var task = Task.Run(f.Layout, TestContext.Current.CancellationToken);
        var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Same(task, finished);
        Assert.Contains((await task).Pages.SelectMany(page => page.Lines), line => line.ParagraphId == block.Id);
    }

    [Fact]
    public void LaSpaziaturaVerticaleDelleCelleEDistintaDaQuellaLaterale()
    {
        var f = new LayoutFixture(width: 300, height: 400);
        var probe = f.Add("riga");
        var table = new TableBlock("t1", [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("c1", new PulseOffice.Text.Rich.TextParagraph("cella") { Runs = [new PulseOffice.Text.Rich.TextRun("cella")] })]))])], [Twip.FromPoints(200)])
        {
            Direct = new PulseOffice.Documents.Styles.TableProperties { CellPadding = 6, VerticalCellPadding = 0 },
        };
        f.Document.Body.Add(table);

        var layout = f.Layout();

        var line = layout.Pages[0].Lines.Single(item => item.ParagraphId == probe.Id);
        var box = layout.Pages[0].Tables.Single();
        Assert.Equal(line.Line.Height, box.Bounds.Height, 3);
        Assert.Equal(box.Bounds.X + 6, box.Lines.Single().Left, 3);
    }

    [Fact]
    public void UnaTabellaCheCambiaStileDiPaginaApreUnaPaginaNuova()
    {
        var f = new LayoutFixture(width: 300, height: 400);
        f.Document.Styles.Page.Add(f.PageOf("Larga", 400, 300));
        f.Add("prima");
        f.Document.Body.Add(new TableBlock("t1", [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("c1", new PulseOffice.Text.Rich.TextParagraph("cella") { Runs = [new PulseOffice.Text.Rich.TextRun("cella")] })]))])], [Twip.FromPoints(200)])
        {
            PageStyleName = "Larga",
        });

        var layout = f.Layout();

        Assert.Equal(2, layout.Pages.Count);
        Assert.Equal("Larga", layout.Pages[1].StyleName);
        Assert.Single(layout.Pages[1].Tables);
    }
}
