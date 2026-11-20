using System.IO.Compression;
using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round327OdfChartLegendTests
{
    [Theory]
    [InlineData("top-start", LegendPosition.Top)]
    [InlineData("top-end", LegendPosition.Top)]
    [InlineData("bottom-start", LegendPosition.Bottom)]
    [InlineData("bottom-end", LegendPosition.Bottom)]
    public void Corner_legend_positions_read_back_on_their_edge(string position, LegendPosition expected)
    {
        var chart = Read(Written(), "chart:legend-position=\"bottom\"", $"chart:legend-position=\"{position}\"");

        Assert.Equal(expected, chart.Legend);
    }

    private static MemoryStream Written()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("A"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Number(1));
        sheet.Objects.Add(new PlacedObject
        {
            Id = "c1",
            Anchor = new CellAddress(2, 2),
            Content = new ChartDefinition
            {
                Id = "c1",
                Type = ChartType.Column,
                SourceSheet = "Data",
                SourceRange = "A1:B1",
                Legend = LegendPosition.Bottom,
            },
        });
        var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        return stream;
    }

    private static ChartDefinition Read(MemoryStream stream, string find, string replace)
    {
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            foreach (var entry in archive.Entries.Where(entry => entry.FullName.EndsWith("content.xml", StringComparison.Ordinal)).ToList())
            {
                string text;
                using (var reader = new StreamReader(entry.Open())) text = reader.ReadToEnd();
                if (!text.Contains(find, StringComparison.Ordinal)) continue;
                var name = entry.FullName;
                entry.Delete();
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(text.Replace(find, replace, StringComparison.Ordinal));
            }
        }
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return Assert.IsType<ChartDefinition>(Assert.Single(result.Value!.Sheets[0].Objects).Content);
    }
}
