using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using Book = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round312LongValidationListTests
{
    [Fact]
    public void A_typed_list_over_the_formula_limit_stays_out_of_the_standard_rules_and_round_trips()
    {
        var workbook = Book.CreateDefault();
        var entries = Enumerable.Range(0, 40).Select(index => $"Entry {index:D3}").ToList();
        workbook.Sheets[0].Validations.Add(new PulseOffice.Sheets.DataValidation
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(3, 0)), Kind = ValidationKind.List, ListValues = entries,
        });
        workbook.Sheets[0].Validations.Add(new PulseOffice.Sheets.DataValidation
        {
            Range = new CellRange(new CellAddress(0, 1), new CellAddress(0, 1)), Kind = ValidationKind.List, ListValues = ["a", "b"],
        });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);

        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var rules = package.WorkbookPart!.WorksheetParts.Single().Worksheet!.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.DataValidations>()!;
            Assert.Equal(1u, rules.Count!.Value);
            Assert.All(rules.Elements<DocumentFormat.OpenXml.Spreadsheet.DataValidation>(), rule => Assert.True(rule.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.Formula1>()!.Text.Length <= 257));
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!.Sheets[0].Validations;

        Assert.Equal(2, read.Count);
        Assert.Contains(read, rule => rule.ListValues.SequenceEqual(entries) && rule.Range == new CellRange(new CellAddress(0, 0), new CellAddress(3, 0)));
        Assert.Contains(read, rule => rule.ListValues.SequenceEqual(["a", "b"]));
    }
}
