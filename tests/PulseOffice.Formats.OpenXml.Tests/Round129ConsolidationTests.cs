using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round129ConsolidationTests
{
    private static CellRange R(int top, int left, int bottom, int right) => new(new CellAddress(top, left), new CellAddress(bottom, right));

    private static Workbook Book(CellAddress target)
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("North side");
        workbook.AddSheet("Total");
        workbook.Consolidation = new ConsolidationSettings
        {
            Sources = [new ConsolidationArea(0, R(0, 0, 4, 2)), new ConsolidationArea(1, R(1, 1, 5, 3))],
            TargetSheet = 2,
            Target = target,
            Function = ConsolidationFunction.Average,
            TopRowLabels = true,
            LeftColumnLabels = true,
            LinkToSource = true,
        };
        return workbook;
    }

    [Fact]
    public void Consolidation_settings_cross_ods()
    {
        var source = Book(new CellAddress(3, 4));

        var read = ThroughOds(source).Consolidation;

        Assert.NotNull(read);
        Assert.Equal(source.Consolidation!.Sources, read.Sources);
        Assert.Equal(source.Consolidation with { Sources = read.Sources }, read);
    }

    [Fact]
    public void Consolidation_settings_cross_xlsx_on_the_destination_sheet()
    {
        var source = Book(CellAddress.First);

        var read = ThroughXlsx(source).Consolidation;

        Assert.NotNull(read);
        Assert.Equal(source.Consolidation!.Sources, read.Sources);
        Assert.Equal(source.Consolidation with { Sources = read.Sources }, read);
    }

    [Fact]
    public void The_ods_element_follows_the_odf_vocabulary()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(Book(new CellAddress(3, 4)), stream);
        stream.Position = 0;
        using var zip = new ZipArchive(stream);
        using var content = zip.GetEntry("content.xml")!.Open();
        XNamespace table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        var element = XDocument.Load(content).Descendants(table + "consolidation").Single();

        Assert.Equal("average", (string?)element.Attribute(table + "function"));
        Assert.Equal("both", (string?)element.Attribute(table + "use-labels"));
        Assert.Equal("Total.E4", ((string?)element.Attribute(table + "target-cell-address"))?.Replace("$", string.Empty, StringComparison.Ordinal));
        Assert.Contains("'North side'", (string?)element.Attribute(table + "source-cell-range-addresses"), StringComparison.Ordinal);
    }

    [Fact]
    public void Xlsx_declares_that_the_destination_cell_is_lost()
    {
        Assert.DoesNotContain("XLSX237", new XlsxSpreadsheetWriter().CheckFidelity(Book(CellAddress.First)).Losses.Select(loss => loss.Code));
        Assert.Contains("XLSX237", new XlsxSpreadsheetWriter().CheckFidelity(Book(new CellAddress(3, 4))).Losses.Select(loss => loss.Code));
    }

    private static Workbook ThroughXlsx(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        return new XlsxReader().Read(stream).Value!;
    }

    private static Workbook ThroughOds(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        return new OdsReader().Read(stream).Value!;
    }
}
