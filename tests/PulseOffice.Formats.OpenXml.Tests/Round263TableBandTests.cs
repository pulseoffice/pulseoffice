using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round263TableBandTests
{
    private readonly DocxTableStyleCodec _codec = new(new DocxValues());

    [Fact]
    public void A_style_shading_only_the_first_band_keeps_its_banding()
    {
        var style = new W.Style(new W.TableStyleProperties(new W.TableStyleConditionalFormattingTableCellProperties(new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = "DDEEFF" }))
        {
            Type = W.TableStyleOverrideValues.Band1Horizontal,
        });

        var read = _codec.Read(style);

        Assert.Equal(0xFFDDEEFFu, read.FirstBandBackground);
        Assert.Null(read.BandBackground);
    }

    [Fact]
    public void Both_bands_cross_the_style_codec()
    {
        var element = new W.Style();
        _codec.Write(element, new TableProperties { BandBackground = 0xFF112233, FirstBandBackground = 0xFF445566 });

        var read = _codec.Read(element);

        Assert.Equal(0xFF112233u, read.BandBackground);
        Assert.Equal(0xFF445566u, read.FirstBandBackground);
    }

    [Fact]
    public void The_first_band_shades_even_body_rows_and_the_second_odd_ones()
    {
        TableRow Row() => new([new TableCell(new BlockContainer([new ParagraphBlock("p", "")]))]);
        var table = new TableBlock("t", [Row(), Row(), Row()], [new Twip(1000)]) { HeaderRows = 1 };
        var appearance = new TableAppearance(new StyleResolver());
        var style = new TableProperties { BandBackground = 2, FirstBandBackground = 1 };

        Assert.Equal(1u, appearance.StyleBackground(table, style, 1, 0));
        Assert.Equal(2u, appearance.StyleBackground(table, style, 2, 0));
    }
}
