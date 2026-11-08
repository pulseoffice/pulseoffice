using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class VmlPathParserTests
{
    private readonly VmlPathParser _parser = new();

    private readonly VmlCoordinateSpace _space = new(0, 0, 100, 100);

    private IReadOnlyList<ShapePoint> Points(string path) =>
        [.. Assert.Single(_parser.Parse(path, _space, true)!.Figures).Nodes.Select(node => node.Point)];

    [Fact]
    public void I_valori_omessi_valgono_zero()
    {
        Assert.Equal([new ShapePoint(0, 0.5), new ShapePoint(1, 0), new ShapePoint(0, 0)], Points("m,50l100,,,e"));
    }

    [Fact]
    public void I_comandi_relativi_partono_dal_punto_corrente()
    {
        Assert.Equal([new ShapePoint(0.1, 0.1), new ShapePoint(0.3, 0.1), new ShapePoint(0.3, 0.5)], Points("t10,10r20,0,0,40e"));
    }

    [Fact]
    public void Le_curve_cubiche_portano_le_maniglie()
    {
        var nodes = Assert.Single(_parser.Parse("m0,0c10,0,100,90,100,100e", _space, false)!.Figures).Nodes;

        Assert.Equal(new ShapePoint(0.1, 0), nodes[0].After);
        Assert.Equal(new ShapePoint(1, 0.9), nodes[1].Before);
    }

    [Fact]
    public void L_arco_in_senso_orario_finisce_sul_raggio_indicato()
    {
        var nodes = Assert.Single(_parser.Parse("wr0,0,100,100,100,50,50,100e", _space, false)!.Figures).Nodes;

        Assert.Equal(1, nodes[0].Point.X, 6);
        Assert.Equal(0.5, nodes[0].Point.Y, 6);
        Assert.Equal(0.5, nodes[^1].Point.X, 6);
        Assert.Equal(1, nodes[^1].Point.Y, 6);
        Assert.Equal(2, nodes.Count);
    }

    [Fact]
    public void Il_quarto_di_ellisse_alterna_gli_assi()
    {
        var nodes = Assert.Single(_parser.Parse("m0,50qy50,0,100,50e", _space, false)!.Figures).Nodes;

        Assert.Equal(3, nodes.Count);
        Assert.Equal(0, nodes[0].After!.Value.X, 6);
        Assert.Equal(1, nodes[2].Before!.Value.X, 6);
    }

    [Fact]
    public void Le_formule_e_i_comandi_ignoti_non_si_leggono()
    {
        Assert.Null(_parser.Parse("m@0,0l10,10e", _space, true));
        Assert.Null(_parser.Parse("m0,0ae10,10,5,5,0,90e", _space, true));
        Assert.Null(_parser.Parse(string.Empty, _space, true));
    }

    [Fact]
    public void La_chiusura_separa_le_figure()
    {
        var path = _parser.Parse("m0,0l100,0,100,100xm0,0l0,100e", _space, true)!;

        Assert.Equal(2, path.Figures.Count);
        Assert.True(path.Figures[0].Closed);
        Assert.False(path.Figures[1].Closed);
        Assert.True(path.Filled);
    }

    [Fact]
    public void A_path_whose_coordinates_overflow_is_rejected()
    {
        Assert.Null(_parser.Parse("m-1e308,0l1e308,100e", new VmlCoordinateSpace(1e308, 0, 100, 100), false));
    }
}
