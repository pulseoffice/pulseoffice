using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round269TableBandAxesTests
{
    private readonly DocxTableStyleCodec _codec = new(new DocxValues());

    [Fact]
    public void A_style_shading_only_the_first_column_band_reads_as_columns()
    {
        var style = new W.Style(new W.TableStyleProperties(new W.TableStyleConditionalFormattingTableCellProperties(new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = "DDEEFF" }))
        {
            Type = W.TableStyleOverrideValues.Band1Vertical,
        });

        var read = _codec.Read(style);

        Assert.Equal(TableBandAxes.Columns, read.FirstBandAxes);
        Assert.Null(read.BandAxes);
    }

    [Fact]
    public void A_column_only_band_is_written_back_only_for_columns()
    {
        var element = new W.Style();
        _codec.Write(element, new TableProperties { FirstBandBackground = 0xFF445566, FirstBandAxes = TableBandAxes.Columns });

        var types = element.Elements<W.TableStyleProperties>().Select(item => item.Type!.Value).ToList();

        Assert.Contains(W.TableStyleOverrideValues.Band1Vertical, types);
        Assert.DoesNotContain(W.TableStyleOverrideValues.Band1Horizontal, types);
        Assert.Equal(TableBandAxes.Columns, _codec.Read(element).FirstBandAxes);
    }

    [Fact]
    public void A_column_only_first_band_does_not_shade_rows()
    {
        TableRow Row() => new([new TableCell(new BlockContainer([new ParagraphBlock("p", "")]))]);
        var table = new TableBlock("t", [Row(), Row()], [new Twip(1000)]);
        var appearance = new TableAppearance(new StyleResolver());
        var style = new TableProperties { FirstBandBackground = 1, FirstBandAxes = TableBandAxes.Columns };

        Assert.Null(appearance.StyleBackground(table, style, 0, 0));
    }
}
