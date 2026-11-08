using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SheetOutlineTests
{
    private static readonly ISheetOutline Outline = new SheetOutline();

    [Fact]
    public void RaggruppareRegistraLIntervallo()
    {
        var sheet = new Worksheet("Foglio1");

        var group = Outline.Group(sheet, StructureAxis.Rows, 2, 5);

        Assert.Equal(1, group.Level);
        Assert.Equal(4, group.Count);
        Assert.Single(sheet.RowGroups);
    }

    [Fact]
    public void UnGruppoDentroUnAltroSaleDiLivello()
    {
        var sheet = new Worksheet("Foglio1");

        Outline.Group(sheet, StructureAxis.Rows, 1, 10);
        var inner = Outline.Group(sheet, StructureAxis.Rows, 3, 5);

        Assert.Equal(2, inner.Level);
    }

    [Fact]
    public void ChiudereNascondeLeRigheEAprireLeRimette()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.Layout.SetRowHeight(3, Twip.FromPoints(40));

        Outline.Group(sheet, StructureAxis.Rows, 2, 4);
        Outline.Collapse(sheet, StructureAxis.Rows, 0);

        Assert.True(sheet.Layout.IsRowHidden(2));
        Assert.True(sheet.Layout.IsRowHidden(3));

        Outline.Expand(sheet, StructureAxis.Rows, 0);

        Assert.False(sheet.Layout.IsRowHidden(2));
        Assert.Equal(40, sheet.Layout.RowHeight(3).ToPoints(), 3);
    }

    [Fact]
    public void ChiudereDueVolteNonPerdeLeMisure()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.Layout.SetRowHeight(2, Twip.FromPoints(30));

        Outline.Group(sheet, StructureAxis.Rows, 2, 3);
        Outline.Collapse(sheet, StructureAxis.Rows, 0);
        Outline.Collapse(sheet, StructureAxis.Rows, 0);
        Outline.Expand(sheet, StructureAxis.Rows, 0);

        Assert.Equal(30, sheet.Layout.RowHeight(2).ToPoints(), 3);
    }

    [Fact]
    public void SciogliereUnGruppoChiusoRiapreLeRighe()
    {
        var sheet = new Worksheet("Foglio1");

        Outline.Group(sheet, StructureAxis.Rows, 1, 3);
        Outline.Collapse(sheet, StructureAxis.Rows, 0);
        Outline.Ungroup(sheet, StructureAxis.Rows, 1, 3);

        Assert.Empty(sheet.RowGroups);
        Assert.False(sheet.Layout.IsRowHidden(2));
    }

    [Fact]
    public void MostrareUnLivelloChiudeQuelliPiuProfondi()
    {
        var sheet = new Worksheet("Foglio1");

        Outline.Group(sheet, StructureAxis.Rows, 1, 10);
        Outline.Group(sheet, StructureAxis.Rows, 3, 5);

        Outline.ShowLevel(sheet, StructureAxis.Rows, 1);

        Assert.False(sheet.RowGroups[0].IsCollapsed);
        Assert.True(sheet.RowGroups[1].IsCollapsed);
        Assert.True(sheet.Layout.IsRowHidden(4));
    }

    [Fact]
    public void LeColonneSiRaggruppanoAllaStessaManiera()
    {
        var sheet = new Worksheet("Foglio1");

        Outline.Group(sheet, StructureAxis.Columns, 1, 2);
        Outline.Collapse(sheet, StructureAxis.Columns, 0);

        Assert.True(sheet.Layout.IsColumnHidden(1));
        Assert.Empty(sheet.RowGroups);
    }

    [Fact]
    public void ChiudereTuttiILivelliERiaprirliRendeLeRigheDelGruppoInterno()
    {
        var sheet = new PulseOffice.Sheets.Worksheet("Dati");
        var outline = new SheetOutline();
        var normal = sheet.Layout.RowHeight(3);
        outline.Group(sheet, StructureAxis.Rows, 1, 8);
        outline.Group(sheet, StructureAxis.Rows, 2, 4);

        outline.ShowLevel(sheet, StructureAxis.Rows, 0);
        outline.ShowLevel(sheet, StructureAxis.Rows, 2);

        Assert.Equal(normal, sheet.Layout.RowHeight(3));
        Assert.Equal(normal, sheet.Layout.RowHeight(6));
    }
}
