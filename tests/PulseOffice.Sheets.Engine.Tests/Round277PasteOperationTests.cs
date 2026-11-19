using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round277PasteOperationTests
{
    private static readonly SheetClipboard Clipboard = new();

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public void Blank_sources_count_as_zero_and_blank_targets_as_zero()
    {
        var sheet = new Worksheet("Data");
        sheet.SetValue(A("A1"), CellValue.Number(3));
        sheet.SetValue(A("D1"), CellValue.Number(10));
        sheet.SetValue(A("D2"), CellValue.Number(7));
        var snapshot = Clipboard.Copy(sheet, new CellRange(A("A1"), A("A2")));

        Clipboard.Paste(snapshot, sheet, A("D1"), A("A1"), new PasteRequest { Operation = PasteOperation.Multiply });
        Clipboard.Paste(snapshot, sheet, A("F1"), A("A1"), new PasteRequest { Operation = PasteOperation.Subtract });

        Assert.Equal(30d, sheet[A("D1")].Value.AsNumber());
        Assert.Equal(0d, sheet[A("D2")].Value.AsNumber());
        Assert.Equal(-3d, sheet[A("F1")].Value.AsNumber());
    }

    [Fact]
    public void An_operation_onto_a_formula_keeps_the_formula_and_applies_the_value()
    {
        var sheet = new Worksheet("Data");
        sheet.SetValue(A("A1"), CellValue.Number(5));
        sheet.SetFormula(A("D1"), "=B1*2");
        sheet.SetFormula(A("A3"), "=B3");
        sheet.SetFormula(A("D3"), "=C3");

        Clipboard.Paste(Clipboard.Copy(sheet, new CellRange(A("A1"), A("A1"))), sheet, A("D1"), A("A1"),
            new PasteRequest { Operation = PasteOperation.Add, Parts = PasteParts.ValuesOnly });
        Clipboard.Paste(Clipboard.Copy(sheet, new CellRange(A("A3"), A("A3"))), sheet, A("D3"), A("A3"),
            new PasteRequest { Operation = PasteOperation.Multiply });

        Assert.Equal("=(B1*2)+5", sheet[A("D1")].Formula);
        Assert.Equal("=(C3)*(E3)", sheet[A("D3")].Formula);
    }
}
