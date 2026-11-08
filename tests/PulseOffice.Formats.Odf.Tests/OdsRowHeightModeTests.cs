using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsRowHeightModeTests
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
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var layout = new OdsReader().Read(stream).Value!.Sheets[0].Layout;

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
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var layout = new OdsReader().Read(stream).Value!.Sheets[0].Layout;

        Assert.True(layout.IsRowHidden(2));
        layout.ShowRow(2);
        Assert.True(layout.IsRowHeightManual(2));
        Assert.Equal(40, layout.RowHeight(2).ToPoints(), 1);
        Assert.True(layout.IsRowHeightManual(4));
    }
}
