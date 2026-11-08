using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class ColumnBalancerRound17Tests
{
    private sealed class CountingComposition : IParagraphComposition
    {
        public int Calls { get; private set; }

        public IReadOnlyList<ComposedLine> Compose(ParagraphEntry entry, double columnWidth) => Compose(entry, columnWidth, 0, 0);

        public IReadOnlyList<ComposedLine> Compose(ParagraphEntry entry, double columnWidth, int start, int firstIndex)
        {
            Calls++;
            return [new ComposedLine(entry.Block.Id, firstIndex, start, start + 1, columnWidth, 12, 9, 3, [])];
        }

        public ComposedLine ComposeLine(ParagraphEntry entry, double columnWidth, double left, double right, int start, int index) =>
            new(entry.Block.Id, index, start, start + 1, right - left, 12, 9, 3, []);
    }

    [Fact]
    public void BalancingAPageDoesNotComposeTheWholeRestOfTheSection()
    {
        var composition = new CountingComposition();
        var flow = new ParagraphFlow(0, 0, false, false, 2, 2, false, false, null, null);
        var items = Enumerable.Range(0, 5000)
            .Select(index => (FlowItem)new ParagraphEntry(new ParagraphBlock("p" + index, "x"), new TextParagraph("x"), string.Empty, null!, null!, null!, flow, composition))
            .ToList();
        var balancer = new ColumnBalancer(null!, new PageFlowOptions());

        var height = balancer.Balance(new BalanceRequest(items, 0, 0, 0, 0, new ColumnLayout(2, 12, true, false), 200, 600));

        Assert.Equal(600, height);
        Assert.True(composition.Calls < 200, $"{composition.Calls} paragraphs composed for one page");
    }

    [Fact]
    public void AShortSectionIsStillBalancedEvenly()
    {
        var composition = new CountingComposition();
        var flow = new ParagraphFlow(0, 0, false, false, 2, 2, false, false, null, null);
        var items = Enumerable.Range(0, 10)
            .Select(index => (FlowItem)new ParagraphEntry(new ParagraphBlock("p" + index, "x"), new TextParagraph("x"), string.Empty, null!, null!, null!, flow, composition))
            .ToList();
        var balancer = new ColumnBalancer(null!, new PageFlowOptions());

        var height = balancer.Balance(new BalanceRequest(items, 0, 0, 0, 0, new ColumnLayout(2, 12, true, false), 200, 600));

        Assert.InRange(height, 60, 61);
    }

    private sealed class RecordingBalancer(ITableComposer tables) : IColumnBalancer
    {
        private readonly ColumnBalancer _inner = new(tables, new PageFlowOptions());

        public List<double> Limits { get; } = [];

        public double Balance(BalanceRequest request)
        {
            Limits.Add(request.TurnedLimit);
            return _inner.Balance(request);
        }
    }

    [Fact]
    public void TheBalancerEstimatesTablesWithThePageLimitOfVerticalCells()
    {
        RecordingBalancer? recording = null;
        var f = new LayoutFixture(width: 400, height: 300, replace: services => services.AddSingleton<IColumnBalancer>(provider => recording = new RecordingBalancer(provider.GetRequiredService<ITableComposer>())));
        var turned = new TableCell(new BlockContainer([new ParagraphBlock(f.Document.NextId("c"), new TextParagraph("vertical") { Runs = [new TextRun("vertical")] })])) { TextDirection = CellTextDirection.TopToBottom };
        var content = new BlockContainer([new TableBlock("t", [new TableRow([turned])], [Twip.FromPoints(60)]), new ParagraphBlock("s1", "after")]);
        f.Document.Body.Add(new SectionBlock("sec", "S", content) { Columns = new SectionColumns(2, Twip.FromPoints(10)) });
        f.Add("tail");

        f.Layout();

        Assert.NotNull(recording);
        Assert.NotEmpty(recording.Limits);
        Assert.All(recording.Limits, limit => Assert.Equal(300, limit, 3));
    }
}
