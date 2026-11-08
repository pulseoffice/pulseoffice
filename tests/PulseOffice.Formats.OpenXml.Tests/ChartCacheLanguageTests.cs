using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ChartCacheLanguageTests
{
    private static readonly XNamespace Chart = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook Book(string language)
    {
        var workbook = new Workbook { DefaultLanguage = language };
        var sheet = workbook.AddSheet("Data");
        sheet[A("A1")] = new Cell { Value = CellValue.Text("Step") };
        sheet[A("B1")] = new Cell { Value = CellValue.Text("Value") };
        sheet[A("A2")] = new Cell { Value = CellValue.Number(1.5) };
        sheet[A("B2")] = new Cell { Value = CellValue.Number(2.25) };
        sheet[A("A3")] = new Cell { Value = CellValue.Number(2.5) };
        sheet[A("B3")] = new Cell { Value = CellValue.Number(3.75) };
        sheet.Objects.Add(new PlacedObject
        {
            Id = "c1",
            Anchor = A("D2"),
            Content = new ChartDefinition { Id = "c1", Type = ChartType.Column, SourceSheet = "Data", SourceRange = "A1:B3" },
        });
        return workbook;
    }

    private static XDocument ChartPart(Workbook workbook, string system)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(system);
        try
        {
            using var stream = new MemoryStream();
            new XlsxWriter().Write(workbook, stream);
            stream.Position = 0;
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var entry = zip.Entries.Single(candidate => candidate.FullName.Contains("/charts/", StringComparison.Ordinal) && candidate.FullName.EndsWith(".xml", StringComparison.Ordinal));
            using var content = entry.Open();
            return XDocument.Load(content);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Theory]
    [InlineData("it-IT", "en-US", "1.5", "2.5")]
    [InlineData("en-US", "it-IT", "1,5", "2,5")]
    public void CachedCategoryLabelsFollowTheWorkbookLanguageAndValuesStayInvariant(string system, string language, string first, string second)
    {
        var chart = ChartPart(Book(language), system);

        var categories = chart.Descendants(Chart + "cat").Descendants(Chart + "v").Select(value => value.Value).ToList();
        var values = chart.Descendants(Chart + "val").Descendants(Chart + "v").Select(value => value.Value).ToList();

        Assert.Equal([first, second], categories);
        Assert.Equal(["2.25", "3.75"], values);
    }
}
