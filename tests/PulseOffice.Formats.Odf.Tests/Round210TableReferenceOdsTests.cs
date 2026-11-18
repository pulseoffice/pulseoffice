using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round210TableReferenceOdsTests
{
    private static CellAddress A(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    [Fact]
    public void Names_conditional_formats_and_validations_keep_their_table_references()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), CellValue.Text("Amount"));
        sheet.SetValue(A("A2"), CellValue.Number(5));
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = new CellRange(A("A1"), A("A2")) });
        workbook.DefineName(new DefinedName("Amounts", "Sales[Amount]"));
        sheet.ConditionalRules.Add(new ConditionalRule { Range = new CellRange(A("C1"), A("C2")), Formula = "=C1>MAX(Sales[Amount])" });
        sheet.Validations.Add(new DataValidation
        {
            Range = new CellRange(A("D1"), A("D1")), Kind = ValidationKind.List, FirstFormula = "Sales[Amount]",
        });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        var read = new OdsReader().Read(stream).Value!;

        Assert.Equal("Sales[Amount]", read.DefinedNames.Single(name => name.Name == "Amounts").Expression);
        Assert.Equal("=C1>MAX(Sales[Amount])", Assert.Single(read.Sheets[0].ConditionalRules).Formula);
        Assert.Equal("Sales[Amount]", Assert.Single(read.Sheets[0].Validations).FirstFormula);
    }
}
