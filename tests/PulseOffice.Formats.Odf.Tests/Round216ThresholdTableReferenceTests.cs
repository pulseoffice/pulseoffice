using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round216ThresholdTableReferenceTests
{
    private static CellAddress A(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    [Fact]
    public void Colour_scale_and_data_bar_thresholds_keep_their_table_references()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), CellValue.Text("Amount"));
        sheet.SetValue(A("A2"), CellValue.Number(5));
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = new CellRange(A("A1"), A("A2")) });
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("C1"), A("C2")),
            Visual = ConditionalVisual.ColorScale,
            ScaleColors = ["#F8696B", "#63BE7B"],
            Thresholds =
            [
                new ConditionalThreshold(ConditionalThresholdKind.Formula, 0, "MIN(Sales[Amount])"),
                new ConditionalThreshold(ConditionalThresholdKind.Maximum),
            ],
        });
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("D1"), A("D2")),
            Priority = 1,
            Visual = ConditionalVisual.DataBar,
            Thresholds =
            [
                new ConditionalThreshold(ConditionalThresholdKind.Minimum),
                new ConditionalThreshold(ConditionalThresholdKind.Formula, 0, "MAX(Sales[Amount])"),
            ],
        });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        var rules = new OdsReader().Read(stream).Value!.Sheets[0].ConditionalRules;

        Assert.Equal("MIN(Sales[Amount])", rules.Single(rule => rule.Visual == ConditionalVisual.ColorScale).Thresholds[0].Formula);
        Assert.Equal("MAX(Sales[Amount])", rules.Single(rule => rule.Visual == ConditionalVisual.DataBar).Thresholds[1].Formula);
    }
}
