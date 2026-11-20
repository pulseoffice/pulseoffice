using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round259TableStyleCodecTests
{
    private readonly DocxTableStyleCodec _codec = CreateCodec();

    private static DocxTableStyleCodec CreateCodec()
    {
        var values = new DocxValues();
        return new DocxTableStyleCodec(values, new DocxBorders(values));
    }

    [Fact]
    public void Side_margins_alone_leave_the_vertical_padding_to_the_style()
    {
        var read = _codec.Padding(new W.TableCellMarginDefault(new W.TableCellLeftMargin { Width = 108, Type = W.TableWidthValues.Dxa }));

        Assert.Equal(5.4, read.CellPadding);
        Assert.Null(read.VerticalCellPadding);
    }

    [Fact]
    public void Borders_without_a_top_line_still_count()
    {
        var read = _codec.Borders(new W.TableBorders(new W.TopBorder { Val = W.BorderValues.None }, new W.BottomBorder { Val = W.BorderValues.Single, Size = 8, Color = "FF0000" }));

        Assert.Equal(1, read.BorderWidth);
    }
}
