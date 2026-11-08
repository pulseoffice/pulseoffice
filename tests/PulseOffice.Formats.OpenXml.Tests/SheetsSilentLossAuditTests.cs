using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class SheetsSilentLossAuditTests
{
    private Workbook ThroughXlsx(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        return new XlsxReader().Read(stream).Value!;
    }

    private Workbook ThroughOds(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        return new OdsReader().Read(stream).Value!;
    }

    private IEnumerable<string> XlsxLosses(Workbook workbook) =>
        new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses.Select(loss => loss.Code);

    private Workbook Protected()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.Protection = new SheetProtection { IsEnabled = true, AllowSelectLockedCells = false, AllowSelectUnlockedCells = false };
        sheet.Scenarios.Add(new Scenario
        {
            Name = "Base",
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)),
            Values = new Dictionary<CellAddress, CellValue> { [new CellAddress(0, 0)] = CellValue.Number(9) },
            PreventChanges = true,
        });
        return workbook;
    }

    [Fact]
    public void LaSelezioneProtettaELoScenarioBloccatoAttraversanoXlsxComeOds()
    {
        foreach (var back in new[] { ThroughXlsx(Protected()), ThroughOds(Protected()) })
        {
            var sheet = back.Sheets[0];
            Assert.False(sheet.Protection.AllowSelectLockedCells);
            Assert.False(sheet.Protection.AllowSelectUnlockedCells);
            Assert.True(sheet.Scenarios.Single().PreventChanges);
        }
    }

    [Fact]
    public void LaVisibilitaDelleModificheELaLinguaPredefinitaAttraversanoOds()
    {
        var workbook = Protected();
        workbook.Changes.ShowChanges = false;
        workbook.DefaultLanguage = "de-DE";

        var back = ThroughOds(workbook);

        Assert.False(back.Changes.ShowChanges);
        Assert.Equal("de-DE", back.DefaultLanguage);
    }

    [Fact]
    public void XlsxDichiaraLaRegistrazioneEIlBordoDegliScenari()
    {
        var workbook = Protected();
        Assert.DoesNotContain("XLSX052", XlsxLosses(workbook));
        Assert.DoesNotContain("XLSX053", XlsxLosses(workbook));

        workbook.Changes.IsRecording = true;
        workbook.Sheets[0].Scenarios[0] = workbook.Sheets[0].Scenarios[0] with { BorderColorHex = "#FF0000" };

        Assert.Contains("XLSX052", XlsxLosses(workbook));
        Assert.Contains("XLSX053", XlsxLosses(workbook));
    }
}
