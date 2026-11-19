using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using Xunit;
using Book = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round241TableReferenceSiblingTests
{
    private static CellAddress A(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    [Fact]
    public void Rules_with_the_same_flat_form_keep_their_structured_forms_when_renumbered_and_reordered()
    {
        var workbook = Book.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), PulseOffice.Sheets.Values.CellValue.Text("Amount"));
        sheet.SetValue(A("A2"), PulseOffice.Sheets.Values.CellValue.Number(5));
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = new CellRange(A("A1"), A("A2")) });
        var range = new CellRange(A("C1"), A("C2"));
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Priority = 1, Formula = "=C1>MAX(Sales[Amount])" });
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Priority = 2, Formula = "=C1>MAX(Sales[[Amount]])" });

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var worksheet = package.WorkbookPart!.WorksheetParts.Single().Worksheet!;
            foreach (var rule in worksheet.Descendants<ConditionalFormattingRule>()) rule.Priority = rule.Priority!.Value + 10;
            foreach (var formatting in worksheet.Descendants<ConditionalFormatting>())
            {
                var rules = formatting.Elements<ConditionalFormattingRule>().ToList();
                foreach (var rule in rules) rule.Remove();
                foreach (var rule in Enumerable.Reverse(rules)) formatting.Append(rule);
            }
            worksheet.Save();
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!.Sheets[0].ConditionalRules;

        Assert.Equal("=C1>MAX(Sales[Amount])", read.Single(rule => rule.Priority == 11).Formula);
        Assert.Equal("=C1>MAX(Sales[[Amount]])", read.Single(rule => rule.Priority == 12).Formula);
    }
}
