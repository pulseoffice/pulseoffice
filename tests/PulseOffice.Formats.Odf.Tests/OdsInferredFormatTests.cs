using System.IO.Compression;
using PulseOffice.Sheets.Addressing;
using ModelValue = PulseOffice.Sheets.Values.CellValue;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsInferredFormatTests
{
    [Fact]
    public void IlFormatoDedottoDiUnaFormulaSiScriveSullaCellaComeInXlsx()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        var currency = workbook.NumberFormats.Register("#,##0.00 \"€\"");
        workbook.Sheets[0][new CellAddress(0, 2)] = new PulseOffice.Sheets.Cell
        {
            Value = ModelValue.Number(751580),
            Formula = "=SUM(A1:A2)",
            InferredFormatId = currency,
        };

        using var stream = new MemoryStream();
        new OdsSpreadsheetWriter().Write(workbook, stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
        var content = reader.ReadToEnd();

        Assert.Contains("style:data-style-name", content, StringComparison.Ordinal);
        Assert.Contains("table:style-name=\"ce1\" table:formula=\"of:=SUM", content, StringComparison.Ordinal);
    }
}
