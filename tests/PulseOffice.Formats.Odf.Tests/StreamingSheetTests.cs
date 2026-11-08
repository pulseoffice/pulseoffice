using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class StreamingSheetTests
{
    private const int Rows = 20_000;

    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        return new OdsReader().Read(stream).Require();
    }

    private static Workbook Large()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < Rows; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
            sheet.SetValue(new CellAddress(row, 2), CellValue.Text($"Riga {row}"));
        }
        sheet.SetFormula(new CellAddress(Rows, 0), $"=SUM(A1:A{Rows})");
        sheet.Merges.Add(new CellRange(new CellAddress(10, 4), new CellAddress(11, 5)));
        sheet.Comments[new CellAddress(15_000, 2)] = new CellComment { Text = "In fondo" };
        sheet.RowGroups.Add(new OutlineGroup { First = 100, Last = 199 });
        sheet.RowGroups.Add(new OutlineGroup { First = 120, Last = 129, Level = 2, IsCollapsed = true });
        sheet.RepeatedPrintRows = 2;
        return workbook;
    }

    [Fact]
    public void UnFoglioGrandeAttraversaIlFormatoIntero()
    {
        var back = RoundTrip(Large()).Sheets[0];

        Assert.Equal(19_999d, back.GetValue(new CellAddress(Rows - 1, 0)).AsNumber());
        Assert.Equal("Riga 12345", back.GetValue(new CellAddress(12_345, 2)).AsText());
        Assert.Equal($"=SUM(A1:A{Rows})", back[new CellAddress(Rows, 0)].Formula);
        Assert.Equal("In fondo", back.Comments[new CellAddress(15_000, 2)].Text);
        Assert.Contains(new CellRange(new CellAddress(10, 4), new CellAddress(11, 5)), back.Merges.All);
        Assert.Equal(2, back.RepeatedPrintRows);
        Assert.Contains(back.RowGroups, group => group is { First: 100, Last: 199 });
        Assert.Contains(back.RowGroups, group => group is { First: 120, Last: 129, IsCollapsed: true });
    }

    [Fact]
    public void IlContenutoScrittoInStreamingDichiaraINamespaceUnaVoltaSola()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(Large(), stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream);
        using var content = archive.GetEntry("content.xml")!.Open();
        var document = XDocument.Load(content);

        var body = document.Root!.Elements().Single(element => element.Name.LocalName == "body");
        Assert.DoesNotContain(body.Attributes(), attribute => attribute.IsNamespaceDeclaration);
        Assert.Equal("automatic-styles", document.Root.Elements().ElementAt(1).Name.LocalName);
    }
}
