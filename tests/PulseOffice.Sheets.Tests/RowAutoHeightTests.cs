using System.Globalization;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class RowAutoHeightTests
{
    private sealed class FixedMeasure : ICellTextMeasure
    {
        public TextExtent Measure(string text, CellFont font, double? wrapWidth, int rotationDegrees)
        {
            var width = text.Length * 6.0;
            if (rotationDegrees % 360 != 0) return new TextExtent(font.Size * 1.3, width);
            var lines = wrapWidth is { } limit && width > limit ? Math.Ceiling(width / limit) : 1;
            return new TextExtent(Math.Min(width, wrapWidth ?? width), lines * font.Size * 1.3);
        }
    }

    private static IRowAutoHeight Service() =>
        new RowAutoHeight(new FixedMeasure(), new CellPresenter(
            new NumberFormatter(CultureInfo.InvariantCulture,
                serial => new DateOnly(1899, 12, 30).ToDateTime(TimeOnly.MinValue).AddDays(serial))));

    private static (Workbook Workbook, Worksheet Sheet) WrappedText()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text(new string('x', 80)));
        sheet.SetStyle(CellAddress.First, workbook.Styles.Intern(CellStyle.Default with { WrapText = true }));
        return (workbook, sheet);
    }

    [Fact]
    public void IlTestoACapoAlzaLaRigaAutomatica()
    {
        var (workbook, sheet) = WrappedText();

        Assert.True(Service().Refresh(workbook, sheet));

        Assert.True(sheet.Layout.RowHeight(0).Value > sheet.Layout.DefaultRowHeight.Value);
        Assert.False(sheet.Layout.IsRowHeightManual(0));
        Assert.Empty(sheet.Layout.RowHeights);
    }

    [Fact]
    public void TogliendoIlTestoLaRigaTornaAllaMisuraPredefinita()
    {
        var (workbook, sheet) = WrappedText();
        var service = Service();
        service.Refresh(workbook, sheet);

        sheet.SetValue(CellAddress.First, CellValue.Text("breve"));
        service.Refresh(workbook, sheet);

        Assert.Equal(sheet.Layout.DefaultRowHeight, sheet.Layout.RowHeight(0));
        Assert.Empty(sheet.Layout.AutomaticRowHeights);
    }

    [Fact]
    public void UnaMisuraManualeNonSiTocca()
    {
        var (workbook, sheet) = WrappedText();
        sheet.Layout.SetRowHeight(0, Twip.FromPoints(12));

        Service().Refresh(workbook, sheet);

        Assert.Equal(Twip.FromPoints(12), sheet.Layout.RowHeight(0));
        Assert.Empty(sheet.Layout.AutomaticRowHeights);
    }

    [Fact]
    public void UnCorpoGrandeAlzaLaRiga()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("Titolo"));
        sheet.SetStyle(CellAddress.First, workbook.Styles.Intern(CellStyle.Default with { Font = CellFont.Default with { Size = 28 } }));

        Service().Refresh(workbook, sheet);

        Assert.True(sheet.Layout.RowHeight(0).Value > sheet.Layout.DefaultRowHeight.Value);
    }

    [Fact]
    public void LaRotazioneAlzaLaRiga()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("Intestazione lunga"));
        sheet.SetStyle(CellAddress.First, workbook.Styles.Intern(CellStyle.Default with { RotationDegrees = 90 }));

        Service().Refresh(workbook, sheet);

        Assert.True(sheet.Layout.RowHeight(0).ToPoints() > 60);
    }

    [Fact]
    public void UnaRigaNascostaRestaNascostaERiappareConLaMisuraAutomatica()
    {
        var (workbook, sheet) = WrappedText();
        var service = Service();
        service.Refresh(workbook, sheet);
        var automatic = sheet.Layout.RowHeight(0);

        sheet.Layout.HideRow(0);
        service.Refresh(workbook, sheet);
        Assert.True(sheet.Layout.IsRowHidden(0));

        sheet.Layout.ShowRow(0);
        Assert.Equal(automatic, sheet.Layout.RowHeight(0));
    }

    [Fact]
    public void LoScostamentoDelleRigheSegueLaMisuraAutomatica()
    {
        var layout = new SheetLayout();
        layout.SetAutomaticRowHeight(1, Twip.FromPoints(45));

        Assert.Equal(Twip.FromPoints(15 + 45).Value, layout.RowOffset(2).Value);

        layout.SetRowHeight(1, Twip.FromPoints(20));
        Assert.True(layout.IsRowHeightManual(1));
        layout.ResetRowHeight(1);
        Assert.Equal(Twip.FromPoints(45), layout.RowHeight(1));
    }

    [Fact]
    public void LaModificaDiUnaCellaRimisuraSoloLaSuaRiga()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var wrap = workbook.Styles.Intern(CellStyle.Default with { WrapText = true });
        for (var row = 0; row < 100_000; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text("riga " + row));
            sheet.SetStyle(new CellAddress(row, 0), wrap);
        }

        var tracker = new RowAutoHeightTracker(Service());
        tracker.Attach(workbook);

        sheet.SetValue(new CellAddress(500, 0), CellValue.Text(new string('x', 80)));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(tracker.Flush());
        watch.Stop();

        Assert.True(sheet.Layout.AutomaticRowHeights.ContainsKey(500));
        Assert.True(watch.ElapsedMilliseconds < 200, $"{watch.ElapsedMilliseconds} ms");
        Assert.False(tracker.Flush());
    }

    [Fact]
    public void CambiareLaLarghezzaDiUnaColonnaRimisuraIlFoglio()
    {
        var (workbook, sheet) = WrappedText();
        var tracker = new RowAutoHeightTracker(Service());
        tracker.Attach(workbook);
        var before = sheet.Layout.AutomaticRowHeights[0];

        sheet.Layout.SetColumnWidth(0, Twip.FromPoints(400));
        tracker.Flush();

        Assert.NotEqual(before, sheet.Layout.AutomaticRowHeights.GetValueOrDefault(0));
    }

    [Fact]
    public void ConMisuraPredefinitaManualeLaRigaNonCresceDaSola()
    {
        var (workbook, sheet) = WrappedText();
        sheet.Layout.DefaultRowHeightManual = true;

        Assert.False(Service().Refresh(workbook, sheet));
        Assert.Equal(sheet.Layout.DefaultRowHeight, sheet.Layout.RowHeight(0));
        Assert.False(sheet.Layout.IsRowAutomatic(0));
    }

    [Fact]
    public void LAltezzaOttimaleRendeAutomaticaLaRigaAncheConPredefinitoManuale()
    {
        var (workbook, sheet) = WrappedText();
        sheet.Layout.DefaultRowHeightManual = true;
        sheet.Layout.MakeRowOptimal(0);

        Assert.True(Service().Refresh(workbook, sheet));
        Assert.True(sheet.Layout.RowHeight(0).Value > sheet.Layout.DefaultRowHeight.Value);

        sheet.Layout.ClearRowOptimal(0);
        Assert.Equal(sheet.Layout.DefaultRowHeight, sheet.Layout.RowHeight(0));
        Assert.False(sheet.Layout.IsRowAutomatic(0));
    }

    [Fact]
    public void UnaRigaOttimaleNascostaRestaOttimaleQuandoRiappare()
    {
        var layout = new SheetLayout { DefaultRowHeightManual = true };
        layout.MakeRowOptimal(3);
        layout.HideRow(3);
        Assert.True(layout.IsRowAutomatic(3));
        layout.ShowRow(3);
        Assert.True(layout.IsRowAutomatic(3));
    }
}
