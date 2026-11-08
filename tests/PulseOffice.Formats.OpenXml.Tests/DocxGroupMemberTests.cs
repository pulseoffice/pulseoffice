using PulseOffice.Charts;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxGroupMemberTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static TextDocument Grouped()
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
        return document;
    }

    [Fact]
    public void Hyperlinks_and_charts_of_group_members_cross_the_docx()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Grouped(), stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code is "DOCX176" or "DOCX180");
        var group = Assert.IsType<ShapeGroupObject>(Assert.IsType<EmbeddedContent>(Assert.Single(read.Value!.Objects.All).Content).Value);
        Assert.Equal(2, group.Children.Count);
        Assert.Equal("https://example.org/", group.Children.Single(child => child.Content is ShapeObject).Hyperlink);
        var chart = Assert.IsType<ChartDefinition>(group.Children.Single(child => child.Content is ChartDefinition).Content);
        Assert.Equal("Sales", chart.InlineData!.Series[0].Name);
    }
}
