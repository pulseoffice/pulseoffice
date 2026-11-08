using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.UI;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class NameBoxResolverTests
{
    private readonly NameBoxResolver _resolver = new();

    private static Workbook Book()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Dati 2024");
        workbook.DefineName(new DefinedName("Totali", "$'Dati 2024'.$B$2:$B$5"));
        workbook.DefineName(new DefinedName("Cella", "C3"));
        return workbook;
    }

    private static string Text(NameBoxTarget? target) =>
        target is { } t ? $"{t.SheetIndex}:{t.Range.Start.ToA1()}:{t.Range.End.ToA1()}" : "null";

    [Theory]
    [InlineData("B2", "0:B2:B2")]
    [InlineData("a1:c3", "0:A1:C3")]
    [InlineData("C3:A1", "0:A1:C3")]
    [InlineData("$D$4", "0:D4:D4")]
    [InlineData("'Dati 2024'.A2", "1:A2:A2")]
    [InlineData("'Dati 2024'!A2:B3", "1:A2:B3")]
    [InlineData("Totali", "1:B2:B5")]
    [InlineData("Cella", "0:C3:C3")]
    [InlineData("Inesistente", "null")]
    [InlineData("", "null")]
    public void LaCasellaNomeCapisceIntervalliFogliENomi(string text, string expected) =>
        Assert.Equal(expected, Text(_resolver.Resolve(text, Book(), 0)));
}
