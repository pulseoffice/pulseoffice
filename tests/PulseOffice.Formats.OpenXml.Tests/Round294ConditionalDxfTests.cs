using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using ModelValue = PulseOffice.Sheets.Values.CellValue;
using Xunit;
using Model = PulseOffice.Sheets;
using ModelStyle = PulseOffice.Sheets.Styling.CellStyle;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round294ConditionalDxfTests
{
    [Fact]
    public void The_number_format_of_a_conditional_format_survives_xlsx()
    {
        var workbook = new Model.Workbook();
        var sheet = workbook.AddSheet("Sheet1");
        sheet.SetValue(new CellAddress(0, 0), ModelValue.Number(5));
        var custom = workbook.NumberFormats.Register("0.000\" kg\"");
        sheet.ConditionalRules.Add(Rule(ModelStyle.Default with { NumberFormatId = custom }, 0));
        sheet.ConditionalRules.Add(Rule(ModelStyle.Default with { NumberFormatId = 10 }, 1));

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!;
        var rules = read.Sheets[0].ConditionalRules.OrderBy(rule => rule.Priority).ToList();

        Assert.Equal("0.000\" kg\"", read.NumberFormats.Get(rules[0].Style.NumberFormatId).Code);
        Assert.Equal(10, rules[1].Style.NumberFormatId);
    }

    [Fact]
    public void Theme_and_indexed_colours_of_a_differential_format_are_resolved_on_read()
    {
        var stylesheet = new Stylesheet(new DifferentialFormats(
            new DifferentialFormat(
                new Font(new Color { Theme = 4U }),
                new Fill(new PatternFill { PatternType = PatternValues.Solid, BackgroundColor = new BackgroundColor { Indexed = 10U } })),
            new DifferentialFormat(new Font(new Color { Theme = 1U, Tint = 0.5 }))));
        var worksheet = new Worksheet(new SheetData(), new ConditionalFormatting(
            new ConditionalFormattingRule(new Formula("1")) { Type = ConditionalFormatValues.Expression, FormatId = 0U, Priority = 1 },
            new ConditionalFormattingRule(new Formula("1")) { Type = ConditionalFormatValues.Expression, FormatId = 1U, Priority = 2 })
        { SequenceOfReferences = new ListValue<StringValue> { InnerText = "A1" } });

        var rules = new XlsxConditionalMapper().ReadRules(worksheet, stylesheet).OrderBy(rule => rule.Priority).ToList();

        Assert.Equal("#4472C4", rules[0].Style.Font.ColorHex);
        Assert.Equal("#FF0000", rules[0].Style.BackgroundHex);
        Assert.Equal("#808080", rules[1].Style.Font.ColorHex);
    }

    private static ConditionalRule Rule(ModelStyle style, int priority) => new()
    {
        Range = new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)),
        Operator = ConditionOperator.GreaterThan,
        Operands = ["1"],
        Priority = priority,
        Style = style,
    };
}
