using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class Round165OdtFormatChangeTests
{
    [Fact]
    public void The_previous_formatting_of_a_tracked_format_change_survives_the_round_trip()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("ab").WithRuns([new TextRun("ab") { Bold = true }]))
        {
            Marks = [new ChangeMark(0, "f1", MarkEdge.Start), new ChangeMark(2, "f1", MarkEdge.End), new ChangeMark(0, "f2", MarkEdge.Point)],
        }]);
        var date = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        document.Changes.Add(new FormatChange("f1", "Ada", date, [new TextRun("a") { Italic = true }, new TextRun("b")]));
        document.Changes.Add(new ParagraphFormatChange("f2", "Ada", date, new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.Header }));

        Assert.DoesNotContain("ODT002", new OdtWriter().CheckFidelity(document).Losses.Select(loss => loss.Code));
        var again = OdtRoundTripTests.RoundTrip(document);

        var format = Assert.IsType<FormatChange>(again.Changes.Find("f1"));
        Assert.Equal(["a", "b"], format.PreviousRuns.Select(run => run.Text));
        Assert.True(format.PreviousRuns[0].Italic);
        Assert.False(format.PreviousRuns[1].Italic);
        var layout = Assert.IsType<ParagraphFormatChange>(again.Changes.Find("f2"));
        Assert.True(layout.PreviousKnown);
        Assert.Equal(DefaultStyleNames.Header, layout.Previous.StyleName);
        Assert.Equal(["p1"], again.Body.Blocks.Select(block => block.Id));
    }
}
