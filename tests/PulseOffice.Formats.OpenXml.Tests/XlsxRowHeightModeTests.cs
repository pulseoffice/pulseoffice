using PulseOffice.Foundation.Units;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxRowHeightModeTests
{
    [Fact]
    public void LeMisureManualiEAutomaticheRestanoDistinte()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("a"));
        sheet.Layout.SetRowHeight(1, Twip.FromPoints(40));
        sheet.Layout.SetAutomaticRowHeight(3, Twip.FromPoints(30));

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var layout = new XlsxReader().Read(stream).Value!.Sheets[0].Layout;

        Assert.True(layout.IsRowHeightManual(1));
        Assert.Equal(40, layout.RowHeight(1).ToPoints(), 1);
        Assert.False(layout.IsRowHeightManual(3));
        Assert.Equal(30, layout.RowHeight(3).ToPoints(), 1);
        Assert.False(layout.IsRowHeightManual(0));
    }

    [Fact]
    public void UnaRigaNascostaConservaLaSuaMisuraManuale()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("a"));
        sheet.Layout.SetRowHeight(2, Twip.FromPoints(40));
        sheet.Layout.HideRow(2);
        sheet.Layout.SetRowHeight(4, sheet.Layout.DefaultRowHeight);

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var layout = new XlsxReader().Read(stream).Value!.Sheets[0].Layout;

        Assert.True(layout.IsRowHidden(2));
        layout.ShowRow(2);
        Assert.True(layout.IsRowHeightManual(2));
        Assert.Equal(40, layout.RowHeight(2).ToPoints(), 1);
        Assert.True(layout.IsRowHeightManual(4));
    }

    [Fact]
    public void UnaMisuraPredefinitaManualeSiConserva()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("a"));
        sheet.Layout.DefaultRowHeight = Twip.FromPoints(40);
        sheet.Layout.DefaultRowHeightManual = true;

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var layout = new XlsxReader().Read(stream).Value!.Sheets[0].Layout;

        Assert.True(layout.DefaultRowHeightManual);
        Assert.Equal(40, layout.DefaultRowHeight.ToPoints(), 1);
    }

    [Fact]
    public void ConPredefinitoManualeLeRigheOttimaliRestanoOttimali()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(4, 0), CellValue.Text("a"));
        sheet.Layout.DefaultRowHeight = Twip.FromPoints(30);
        sheet.Layout.DefaultRowHeightManual = true;
        sheet.Layout.MakeRowOptimal(2);
        sheet.Layout.SetAutomaticRowHeight(3, Twip.FromPoints(50));

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var layout = new XlsxReader().Read(stream).Value!.Sheets[0].Layout;

        Assert.True(layout.DefaultRowHeightManual);
        Assert.True(layout.IsRowAutomatic(2));
        Assert.True(layout.IsRowAutomatic(3));
        Assert.Equal(50, layout.RowHeight(3).ToPoints(), 1);
        Assert.False(layout.IsRowAutomatic(4));
        Assert.False(layout.IsRowAutomatic(1));
    }
}
