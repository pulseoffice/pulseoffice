using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class OpenXmlConditionalAuditTests
{
    private static Workbook With(params ConditionalRule[] rules)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        foreach (var rule in rules) sheet.ConditionalRules.Add(rule);
        return workbook;
    }

    private static (string Xml, Workbook Read) RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        string xml;
        using (var document = SpreadsheetDocument.Open(stream, false))
        using (var reader = new StreamReader(document.WorkbookPart!.WorksheetParts.Single().GetStream()))
            xml = reader.ReadToEnd();
        stream.Position = 0;
        return (xml, new XlsxReader().Read(stream).Value!);
    }

    [Fact]
    public void An_extended_icon_set_is_written_with_a_one_based_priority_and_read_back_unchanged()
    {
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 0));
        var (xml, read) = RoundTrip(With(new ConditionalRule { Range = range, Priority = 0, Visual = ConditionalVisual.IconSet, IconSetName = "3Stars" }));

        Assert.Contains("type=\"iconSet\" priority=\"1\"", xml, StringComparison.Ordinal);
        Assert.Equal(0, Assert.Single(read.Sheets[0].ConditionalRules).Priority);
    }

    [Fact]
    public void A_threshold_that_is_not_a_finite_number_is_not_written_as_NaN()
    {
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 0));
        var (xml, _) = RoundTrip(With(
            new ConditionalRule
            {
                Range = range, Visual = ConditionalVisual.ColorScale, ScaleColors = ["#FF0000", "#00FF00"],
                Thresholds = [new ConditionalThreshold(ConditionalThresholdKind.Number, double.NaN), new ConditionalThreshold(ConditionalThresholdKind.Number, double.PositiveInfinity)],
            },
            new ConditionalRule
            {
                Range = range, Priority = 1, Visual = ConditionalVisual.DataBar,
                Thresholds = [new ConditionalThreshold(ConditionalThresholdKind.Number, double.NaN), new ConditionalThreshold(ConditionalThresholdKind.Number, 5)],
            }));

        Assert.DoesNotContain("NaN", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", xml, StringComparison.Ordinal);
    }
}
