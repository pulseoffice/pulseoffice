using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using Xunit;
using Workbook = PulseOffice.Sheets.Workbook;
using CellStyle = PulseOffice.Sheets.Styling.CellStyle;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class RangeConditionRoundTripTests
{
    public static TheoryData<string, ConditionOperator, string[], DatePeriod> Rules() => new()
    {
        { "ods", ConditionOperator.Duplicate, [], DatePeriod.Today },
        { "xlsx", ConditionOperator.Duplicate, [], DatePeriod.Today },
        { "ods", ConditionOperator.Unique, [], DatePeriod.Today },
        { "xlsx", ConditionOperator.Unique, [], DatePeriod.Today },
        { "ods", ConditionOperator.TopItems, ["3"], DatePeriod.Today },
        { "xlsx", ConditionOperator.TopItems, ["3"], DatePeriod.Today },
        { "ods", ConditionOperator.BottomPercent, ["20"], DatePeriod.Today },
        { "xlsx", ConditionOperator.BottomPercent, ["20"], DatePeriod.Today },
        { "ods", ConditionOperator.BelowOrEqualAverage, [], DatePeriod.Today },
        { "xlsx", ConditionOperator.BelowOrEqualAverage, [], DatePeriod.Today },
        { "ods", ConditionOperator.AboveAverage, [], DatePeriod.Today },
        { "xlsx", ConditionOperator.AboveAverage, [], DatePeriod.Today },
        { "ods", ConditionOperator.DateIs, [], DatePeriod.LastMonth },
        { "xlsx", ConditionOperator.DateIs, [], DatePeriod.LastMonth },
        { "ods", ConditionOperator.DateIs, [], DatePeriod.NextYear },
        { "xlsx", ConditionOperator.DateIs, [], DatePeriod.Last7Days },
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void LeRegoleSullInsiemeDelleCelleFannoIlGiroNeiDueFormati(string format, ConditionOperator op, string[] operands, DatePeriod period)
    {
        var read = Assert.Single(Through(format, Workbook(op, operands, period)).Sheets[0].ConditionalRules);

        Assert.Equal(op, read.Operator);
        Assert.Equal(operands, read.Operands);
        Assert.Null(read.Formula);
        if (op == ConditionOperator.DateIs) Assert.Equal(period, read.DatePeriod);
        Assert.Equal("#FFCC00", read.Style.BackgroundHex);
    }

    [Fact]
    public void LeDeviazioniStandardFannoIlGiroInXlsxESonoDichiarateInOds()
    {
        var workbook = Workbook(ConditionOperator.AboveAverage, [], DatePeriod.Today);
        workbook.Sheets[0].ConditionalRules[0] = workbook.Sheets[0].ConditionalRules[0] with { StandardDeviations = 2 };

        Assert.Equal(2, Assert.Single(Through("xlsx", workbook).Sheets[0].ConditionalRules).StandardDeviations);
        Assert.Contains(new OdsSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "ODF102");
    }

    [Fact]
    public void LeVirgoletteRaddoppiateDiUnConfrontoSiTolgonoInLettura()
    {
        var read = Assert.Single(Through("xlsx", Workbook(ConditionOperator.Equal, ["a\"b"], DatePeriod.Today)).Sheets[0].ConditionalRules);

        Assert.Equal(["a\"b"], read.Operands);
        Assert.Null(read.Formula);
    }

    [Fact]
    public void UnConfrontoConUnRiferimentoDiExcelDiventaUnaFormula()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Workbook(ConditionOperator.GreaterThan, ["5"], DatePeriod.Today), stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var rule = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<ConditionalFormattingRule>().Single();
            rule.GetFirstChild<Formula>()!.Text = "$D$1*2";
            document.Save();
        }
        stream.Position = 0;

        var read = Assert.Single(new XlsxReader().Read(stream).Value!.Sheets[0].ConditionalRules);

        Assert.Equal("=B2>$D$1*2", read.Formula);
    }

    [Fact]
    public void Long_and_comma_validation_lists_go_to_a_hidden_helper_sheet_and_come_back_typed()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Elenco");
        string[] longList = [.. Enumerable.Range(0, 30).Select(index => $"Voce {index:D3}")];
        sheet.Validations.Add(new PulseOffice.Sheets.DataValidation
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(4, 0)),
            Kind = PulseOffice.Sheets.ValidationKind.List,
            ListValues = longList,
        });
        sheet.Validations.Add(new PulseOffice.Sheets.DataValidation
        {
            Range = new CellRange(new CellAddress(0, 1), new CellAddress(0, 1)),
            Kind = PulseOffice.Sheets.ValidationKind.List,
            ListValues = ["Rossi, Mario", "Bianchi"],
        });

        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code is "XLSX103" or "XLSX034");

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var entries = package.WorkbookPart!.Workbook!.Sheets!.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().ToList();
            Assert.Equal(SheetStateValues.VeryHidden, entries[^1].State!.Value);
            var formulas = package.WorkbookPart.WorksheetParts.SelectMany(part => part.Worksheet!.Descendants<Formula1>()).Select(formula => formula.Text).ToList();
            Assert.Contains("'PulseOffice Lists'!$A$1:$A$30", formulas);
            Assert.Contains("'PulseOffice Lists'!$B$1:$B$2", formulas);
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!;
        var back = Assert.Single(read.Sheets);
        Assert.Equal(longList, back.Validations[0].ListValues);
        Assert.Null(back.Validations[0].FirstFormula);
        Assert.Equal(["Rossi, Mario", "Bianchi"], back.Validations[1].ListValues);
    }

    [Fact]
    public void LAnnoInXlsxDiventaUnaFormulaEquivalente()
    {
        var read = Assert.Single(Through("xlsx", Workbook(ConditionOperator.DateIs, [], DatePeriod.ThisYear)).Sheets[0].ConditionalRules);

        Assert.Contains("YEAR(", read.Formula, StringComparison.Ordinal);
        Assert.Contains("TODAY()", read.Formula, StringComparison.Ordinal);
    }

    [Fact]
    public void IlPeriodoSiScriveComeTimePeriodConLaFormulaDiExcel()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Workbook(ConditionOperator.DateIs, [], DatePeriod.Today), stream);
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);
        var rule = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<ConditionalFormattingRule>().Single();

        Assert.Equal(ConditionalFormatValues.TimePeriod, rule.Type!.Value);
        Assert.Equal(TimePeriodValues.Today, rule.TimePeriod!.Value);
        Assert.Equal("FLOOR(B2,1)=TODAY()", rule.GetFirstChild<Formula>()!.Text);
    }

    private static Workbook Workbook(ConditionOperator op, string[] operands, DatePeriod period)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Regole");
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(new CellAddress(1, 1), new CellAddress(9, 1)),
            Operator = op,
            Operands = operands,
            DatePeriod = period,
            Style = CellStyle.Default with { BackgroundHex = "#FFCC00" },
        });
        return workbook;
    }

    private static Workbook Through(string format, Workbook workbook)
    {
        using var stream = new MemoryStream();
        if (format == "ods") new OdsWriter().Write(workbook, stream); else new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return (format == "ods" ? new OdsReader().Read(stream) : new XlsxReader().Read(stream)).Value!;
    }
}
