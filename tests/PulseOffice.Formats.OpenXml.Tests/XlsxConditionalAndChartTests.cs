using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxConditionalAndChartTests
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

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static Workbook WithData()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        sheet[A("A1")] = new Cell { Value = CellValue.Text("Mese") };
        sheet[A("B1")] = new Cell { Value = CellValue.Text("Ricavi") };

        for (var i = 0; i < 4; i++)
        {
            sheet[A($"A{i + 2}")] = new Cell { Value = CellValue.Text($"M{i + 1}") };
            sheet[A($"B{i + 2}")] = new Cell { Value = CellValue.Number(10 * (i + 1)) };
        }

        return workbook;
    }

    [Fact]
    public void UnaRegolaSuValoreConservaConfrontoOperandoEAspetto()
    {
        var workbook = WithData();
        workbook.Sheets[0].ConditionalRules.Add(new ConditionalRule
        {
            Range = R("B2:B5"),
            Operator = ConditionOperator.GreaterThan,
            Operands = ["25"],
            Style = CellStyle.Default with
            {
                BackgroundHex = "#FFC7CE",
                Font = CellFont.Default with { Bold = true, ColorHex = "#9C0006" },
            },
        });

        var rule = Assert.Single(RoundTrip(workbook).Sheets[0].ConditionalRules);

        Assert.Equal(R("B2:B5"), rule.Range);
        Assert.Equal(ConditionOperator.GreaterThan, rule.Operator);
        Assert.Equal("25", Assert.Single(rule.Operands));
        Assert.Equal("#FFC7CE", rule.Style.BackgroundHex);
        Assert.True(rule.Style.Font.Bold);
        Assert.Equal("#9C0006", rule.Style.Font.ColorHex);
    }

    [Fact]
    public void RegoleSulloStessoIntervalloRestanoNellOrdineDiPriorita()
    {
        var workbook = WithData();
        var sheet = workbook.Sheets[0];

        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = R("B2:B5"),
            Priority = 0,
            Operator = ConditionOperator.LessThan,
            Operands = ["15"],
            StopIfTrue = true,
        });

        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = R("B2:B5"),
            Priority = 1,
            Operator = ConditionOperator.Between,
            Operands = ["15", "35"],
        });

        var rules = RoundTrip(workbook).Sheets[0].ConditionalRules
            .OrderBy(r => r.Priority).ToList();

        Assert.Equal(2, rules.Count);
        Assert.Equal(ConditionOperator.LessThan, rules[0].Operator);
        Assert.True(rules[0].StopIfTrue);
        Assert.Equal(ConditionOperator.Between, rules[1].Operator);
        Assert.Equal(["15", "35"], rules[1].Operands);
    }

    [Fact]
    public void BarreDeiDatiEScalaDiColoreConservanoIColori()
    {
        var workbook = WithData();
        var sheet = workbook.Sheets[0];

        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = R("B2:B5"),
            Visual = ConditionalVisual.DataBar,
            BarColorHex = "#638EC6",
        });

        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = R("A2:A5"),
            Visual = ConditionalVisual.ColorScale,
            ScaleColors = ["#F8696B", "#FFEB84", "#63BE7B"],
        });

        var rules = RoundTrip(workbook).Sheets[0].ConditionalRules;

        var bar = Assert.Single(rules, r => r.Visual == ConditionalVisual.DataBar);
        Assert.Equal("#638EC6", bar.BarColorHex);

        var scale = Assert.Single(rules, r => r.Visual == ConditionalVisual.ColorScale);
        Assert.Equal(["#F8696B", "#FFEB84", "#63BE7B"], scale.ScaleColors);
    }

    [Fact]
    public void RegolaAFormulaConservaLaFormulaNellaNostraSintassi()
    {
        var workbook = WithData();
        workbook.Sheets[0].ConditionalRules.Add(new ConditionalRule
        {
            Range = R("A2:A5"),
            Formula = "=B2>20",
            Style = CellStyle.Default with { BackgroundHex = "#C6EFCE" },
        });

        var rule = Assert.Single(RoundTrip(workbook).Sheets[0].ConditionalRules);

        Assert.Equal("=B2>20", rule.Formula);
        Assert.Equal("#C6EFCE", rule.Style.BackgroundHex);
    }

    [Fact]
    public void UnGraficoConservaTipoOrigineAncoraggioETitolo()
    {
        var workbook = WithData();
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "grafico1",
            Anchor = A("D3"),
            Content = new ChartDefinition
            {
                Id = "grafico1",
                Type = ChartType.Column,
                SourceSheet = workbook.Sheets[0].Name,
                SourceRange = "A1:B5",
                ChartTitle = "Ricavi per mese",
                Legend = LegendPosition.Bottom,
            },
        });

        var placed = Assert.Single(RoundTrip(workbook).Sheets[0].Objects);
        var chart = Assert.IsType<ChartDefinition>(placed.Content);

        Assert.Equal(A("D3"), placed.Anchor);
        Assert.Equal(ChartType.Column, chart.Type);
        Assert.Equal("A1:B5", chart.SourceRange);
        Assert.Equal(workbook.Sheets[0].Name, chart.SourceSheet);
        Assert.Equal("Ricavi per mese", chart.ChartTitle);
        Assert.Equal(LegendPosition.Bottom, chart.Legend);
    }

    [Theory]
    [InlineData(ChartType.Column)]
    [InlineData(ChartType.Bar)]
    [InlineData(ChartType.StackedColumn)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area)]
    [InlineData(ChartType.Pie)]
    [InlineData(ChartType.Doughnut)]
    public void OgniTipoDiGraficoSiRileggeComeSeStesso(ChartType type)
    {
        var workbook = WithData();
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = A("D1"),
            Content = new ChartDefinition
            {
                Id = "g",
                Type = type,
                SourceSheet = workbook.Sheets[0].Name,
                SourceRange = "A1:B5",
            },
        });

        var placed = Assert.Single(RoundTrip(workbook).Sheets[0].Objects);
        Assert.Equal(type, Assert.IsType<ChartDefinition>(placed.Content).Type);
    }

    [Fact]
    public void ColonneELineaSiRileggeConLeSerieALinea()
    {
        var workbook = WithData();
        var sheet = workbook.Sheets[0];
        sheet[A("C1")] = new Cell { Value = CellValue.Text("Costi") };
        sheet[A("D1")] = new Cell { Value = CellValue.Text("Saldo") };
        for (var i = 0; i < 4; i++)
        {
            sheet[A($"C{i + 2}")] = new Cell { Value = CellValue.Number(5 * (i + 1)) };
            sheet[A($"D{i + 2}")] = new Cell { Value = CellValue.Number(5 * (i + 1)) };
        }
        sheet.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = A("F1"),
            Content = new ChartDefinition
            {
                Id = "g",
                Type = ChartType.ColumnLine,
                LineSeriesCount = 2,
                SourceSheet = sheet.Name,
                SourceRange = "A1:D5",
            },
        });

        var chart = Assert.IsType<ChartDefinition>(Assert.Single(RoundTrip(workbook).Sheets[0].Objects).Content);
        Assert.Equal(ChartType.ColumnLine, chart.Type);
        Assert.Equal(2, chart.LineSeriesCount);
        Assert.Equal("A1:D5", chart.SourceRange);
    }
}
