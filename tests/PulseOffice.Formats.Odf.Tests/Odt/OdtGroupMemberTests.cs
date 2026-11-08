using PulseOffice.Charts;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtGroupMemberTests
{
    [Fact]
    public void Hyperlinks_and_charts_of_group_members_cross_the_odt()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", "Body") { Marks = [new ObjectMark(0, "g1")] }]);
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle };
        var chart = new ChartDefinition
        {
            Id = "c", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty,
            InlineData = new ChartData(["2024", "2025"], [new ChartSeries("Sales", [1, 2])]),
        };
        var group = new ShapeGroupObject
        {
            Id = "grp",
            Children = [new GroupChild(shape, 0, 0, 0.5, 1) { Hyperlink = "https://example.org/" }, new GroupChild(chart, 0.5, 0, 0.5, 1)],
        };
        document.Objects.Add(new AnchoredObject("g1", new CharacterAnchor("p1"), new EmbeddedContent(group),
            new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(3))));

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;

        var members = Assert.IsType<ShapeGroupObject>(Assert.IsType<EmbeddedContent>(Assert.Single(read.Objects.All).Content).Value);
        Assert.Equal("https://example.org/", members.Children.Single(child => child.Content is ShapeObject).Hyperlink);
        var readChart = Assert.IsType<ChartDefinition>(members.Children.Single(child => child.Content is ChartDefinition).Content);
        Assert.Equal("Sales", readChart.InlineData!.Series[0].Name);
    }
}
