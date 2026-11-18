using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using Xunit;
using Book = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round236TableReferenceRenumberTests
{
    private static CellAddress A(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    [Fact]
    public void Renumbered_priorities_still_give_each_rule_its_own_structured_form()
    {
        var workbook = Book.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), PulseOffice.Sheets.Values.CellValue.Text("Amount"));
        sheet.SetValue(A("A2"), PulseOffice.Sheets.Values.CellValue.Number(5));
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = new CellRange(A("A1"), A("A2")) });
        var range = new CellRange(A("C1"), A("C2"));
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Priority = 1, Formula = "=C1>MAX(Sales[Amount])" });
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Priority = 2, Formula = "=C1<MIN(Sales[Amount])" });

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var worksheet = package.WorkbookPart!.WorksheetParts.Single().Worksheet!;
            var rules = worksheet.Descendants<ConditionalFormattingRule>().ToList();
            var priorities = rules.Select(rule => rule.Priority!.Value).Reverse().ToList();
            for (var index = 0; index < rules.Count; index++) rules[index].Priority = priorities[index];
            worksheet.Save();
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!.Sheets[0].ConditionalRules;

        Assert.Contains(read, rule => rule.Formula == "=C1>MAX(Sales[Amount])");
        Assert.Contains(read, rule => rule.Formula == "=C1<MIN(Sales[Amount])");
    }
}
