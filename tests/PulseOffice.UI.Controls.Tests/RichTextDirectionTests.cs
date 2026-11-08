using Avalonia;
using Avalonia.Media;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Rich.Bidi;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class RichTextDirectionTests
{
    private sealed class PlainTheme : ITextTheme
    {
        public uint Resolve(ColorRole role) => 0xFF000000;

        public string MajorFont => "Inter";

        public string MinorFont => "Inter";
    }

    private TypesetSegment Segment(TextParagraph paragraph) =>
        new TextFrameComposer(new ListLabels()).Compose(
            new RichText(PlaceholderRole.Body, [paragraph]), new Rect(0, 0, 20000, 8000), new PlainTheme(), new TextFields(), 1.0).Lines[0].Segments[0];

    [Fact]
    public Task A_right_to_left_paragraph_flows_right_to_left_and_starts_on_the_right() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var segment = Segment(new TextParagraph("שלום") { Direction = TextDirection.RightToLeft });

        Assert.Equal(FlowDirection.RightToLeft, segment.Text.FlowDirection);
        Assert.Equal(TextAlignment.Start, segment.Text.TextAlignment);
    }, CancellationToken.None);

    [Fact]
    public Task The_start_margin_of_a_right_to_left_paragraph_is_on_the_right() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var segment = Segment(new TextParagraph("שלום") { Direction = TextDirection.RightToLeft, MarginLeft = 50, MarginRight = 10 });

        Assert.Equal(10 * ParagraphTypesetter.PointToTwip, segment.X, 3);
    }, CancellationToken.None);

    [Fact]
    public Task Left_to_right_paragraphs_keep_their_flow() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var segment = Segment(new TextParagraph("abc") { Align = TextAlign.Right });

        Assert.Equal(FlowDirection.LeftToRight, segment.Text.FlowDirection);
        Assert.Equal(TextAlignment.End, segment.Text.TextAlignment);
    }, CancellationToken.None);

    [Fact]
    public void The_formatter_sets_and_reads_the_paragraph_direction()
    {
        var formatter = new RichTextFormatter();
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph("a"), new TextParagraph("b")]);

        var turned = formatter.SetDirection(text, null, TextDirection.RightToLeft);

        Assert.All(turned.Paragraphs, paragraph => Assert.Equal(TextDirection.RightToLeft, paragraph.Direction));
        Assert.Equal(TextDirection.RightToLeft, formatter.ReadParagraph(turned, null).Direction);
        Assert.Equal(TextDirection.LeftToRight, formatter.ReadParagraph(text, null).Direction);
    }
}
