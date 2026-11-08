using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsSparklineRoundTripTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    [Fact]
    public void UnGruppoDiSparklineTornaIndietro()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        foreach (var (cell, value) in new[] { ("A1", 3d), ("B1", 5d), ("C1", 2d) })
            sheet.SetValue(A(cell), CellValue.Number(value));

        sheet.Sparklines.Add(new SparklineGroup
        {
            Id = "gruppo1",
            Sparklines = [new Sparkline(A("D1"), R("A1:C1"))],
            Kind = SparklineKind.Column,
            SameScale = true,
        });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));

        var letto = result.Value!.Sheets[0];
        var group = Assert.Single(letto.Sparklines.Groups);

        Assert.Equal(SparklineKind.Column, group.Kind);
        Assert.True(group.SameScale);

        var sparkline = Assert.Single(group.Sparklines);

        Assert.Equal(A("D1"), sparkline.Cell);
        Assert.Equal(R("A1:C1"), sparkline.Source);
    }

    [Fact]
    public void GruppoConStileEFoglioConSpaziTornaInteroNellaFormaEsterno()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati 2024");
        sheet.Sparklines.Add(new SparklineGroup
        {
            Id = "gruppo",
            Sparklines = [new Sparkline(A("D1"), R("A1:C1")), new Sparkline(A("D2"), R("A2:C2"))],
            Style = new SparklineStyle
            {
                ColorHex = "#112233", NegativeHex = "#445566", MarkerHex = "#778899",
                ShowHighPoint = true, ShowLastPoint = true, ShowNegative = false, LineWidth = 2.5,
            },
        });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true))
        using (var reader = new StreamReader(archive.GetEntry("content.xml")!.Open()))
        {
            var content = reader.ReadToEnd();
            Assert.Contains("'Dati 2024'.A1:'Dati 2024'.C1", content);
            Assert.Contains("calcext:color-series=\"#112233\"", content);
        }

        stream.Position = 0;
        var group = Assert.Single(new OdsReader().Read(stream).Value!.Sheets[0].Sparklines.Groups);

        Assert.Equal(2, group.Sparklines.Count);
        Assert.Equal(R("A2:C2"), group.Sparklines[1].Source);
        Assert.Equal(group.Style, new SparklineStyle
        {
            ColorHex = "#112233", NegativeHex = "#445566", MarkerHex = "#778899",
            ShowHighPoint = true, ShowLastPoint = true, ShowNegative = false, LineWidth = 2.5,
        });
    }
}
