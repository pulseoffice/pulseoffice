using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class BorderPresetsTests
{
    private readonly BorderPresets _presets = new();
    private readonly CellRange _range = new(new CellAddress(0, 0), new CellAddress(2, 2));

    private CellBorders At(BorderPreset preset, int row, int column, CellBorders? start = null) =>
        _presets.Apply(start ?? CellBorders.None, preset, _presets.Pen(BorderStyle.Thin, "#000000"), _range, new CellAddress(row, column));

    [Fact]
    public void IBordiEsterniToccanoSoloIlPerimetro()
    {
        var corner = At(BorderPreset.Outside, 0, 0);
        var centre = At(BorderPreset.Outside, 1, 1);

        Assert.True(corner.Top.IsVisible && corner.Left.IsVisible);
        Assert.False(corner.Right.IsVisible || corner.Bottom.IsVisible);
        Assert.True(centre.IsEmpty);
    }

    [Fact]
    public void IBordiInterniEvitanoIlPerimetro()
    {
        var corner = At(BorderPreset.Inside, 0, 0);
        var centre = At(BorderPreset.Inside, 1, 1);

        Assert.False(corner.Top.IsVisible || corner.Left.IsVisible);
        Assert.True(corner.Right.IsVisible && corner.Bottom.IsVisible);
        Assert.True(centre.Left.IsVisible && centre.Right.IsVisible && centre.Top.IsVisible && centre.Bottom.IsVisible);
    }

    [Fact]
    public void OrizzontaliEVerticaliInterniSonoSeparati()
    {
        var horizontal = At(BorderPreset.InsideHorizontal, 1, 1);
        var vertical = At(BorderPreset.InsideVertical, 1, 1);

        Assert.True(horizontal.Top.IsVisible && !horizontal.Left.IsVisible);
        Assert.True(vertical.Left.IsVisible && !vertical.Top.IsVisible);
    }

    [Fact]
    public void IlDoppioInferioreVaSoloSullUltimaRiga()
    {
        Assert.Equal(BorderStyle.Double, At(BorderPreset.DoubleBottom, 2, 1).Bottom.Style);
        Assert.False(At(BorderPreset.DoubleBottom, 1, 1).Bottom.IsVisible);
    }

    [Fact]
    public void NessunBordoCancellaAncheLeDiagonali()
    {
        var crossed = At(BorderPreset.DiagonalUp, 1, 1);
        Assert.True(crossed.HasDiagonal);
        Assert.True(At(BorderPreset.None, 1, 1, crossed).IsEmpty);
    }

    [Fact]
    public void TuttiIBordiConservanoLeDiagonali()
    {
        var crossed = At(BorderPreset.DiagonalDown, 1, 1);
        Assert.True(At(BorderPreset.All, 1, 1, crossed).DiagonalDown.IsVisible);
    }

    [Fact]
    public void IlTrattoSpessoEPiuLargoDelSottile()
    {
        Assert.True(_presets.Pen(BorderStyle.Thick, "#000").Width.Value > _presets.Pen(BorderStyle.Thin, "#000").Width.Value);
        Assert.False(_presets.Pen(BorderStyle.None, "#000").IsVisible);
    }
}
