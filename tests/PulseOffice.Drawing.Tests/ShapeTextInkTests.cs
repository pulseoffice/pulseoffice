using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class ShapeTextInkTests
{
    private readonly ShapeTextInk _ink = new();

    private ShapeStyle Automatic(ShapeFill fill) => ShapeStyle.Default with
    {
        Fill = fill,
        Text = ShapeTextStyle.Default with { AutomaticColor = true, ColorHex = "#123456" },
    };

    [Fact]
    public void Il_colore_esplicito_non_cambia_con_lo_sfondo()
    {
        var style = ShapeStyle.Default with { Fill = ShapeFill.None, Text = ShapeTextStyle.Default with { ColorHex = "#000000" } };

        Assert.Equal("#000000", _ink.Resolve(style, "#1E1E1E"));
    }

    [Fact]
    public void Su_un_riempimento_chiaro_il_testo_automatico_e_scuro()
    {
        Assert.Equal("#000000", _ink.Resolve(Automatic(ShapeFill.Default with { ColorHex = "#FFF3B0" }), "#1E1E1E"));
    }

    [Fact]
    public void Su_un_riempimento_scuro_il_testo_automatico_e_chiaro()
    {
        Assert.Equal("#FFFFFF", _ink.Resolve(Automatic(ShapeFill.Default with { ColorHex = "#1E3A8A" }), "#FFFFFF"));
    }

    [Fact]
    public void Senza_riempimento_il_testo_automatico_segue_lo_sfondo_del_documento()
    {
        Assert.Equal("#000000", _ink.Resolve(Automatic(ShapeFill.None), ShapeTextInk.PaperHex));
        Assert.Equal("#FFFFFF", _ink.Resolve(Automatic(ShapeFill.None), "#1F2125"));
    }

    [Fact]
    public void Un_riempimento_trasparente_lascia_vedere_lo_sfondo()
    {
        Assert.Equal("#FFFFFF", _ink.Resolve(Automatic(ShapeFill.Default with { ColorHex = "#FFFFFF", Opacity = 0.1 }), "#101010"));
    }
}
