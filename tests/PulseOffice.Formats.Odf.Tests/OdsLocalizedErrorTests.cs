using System.IO.Compression;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsLocalizedErrorTests
{
    [Theory]
    [InlineData("#N/D", CellError.NotAvailable)]
    [InlineData("#VALORE!", CellError.Value)]
    [InlineData("#WERT!", CellError.Value)]
    [InlineData("#¡REF!", CellError.Reference)]
    [InlineData("#NOMBRE!", CellError.Number)]
    [InlineData("Err:523", CellError.Number)]
    [InlineData("Err:532", CellError.DivideByZero)]
    public void GliErroriScrittiDaUnaSuiteLocalizzataSiRiconoscono(string shown, CellError expected)
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        workbook.Sheets[0][new CellAddress(0, 0)] = new PulseOffice.Sheets.Cell
        {
            Formula = "=NA()",
            Value = CellValue.FromError(CellError.NotAvailable),
        };

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            Assert.Contains(">#N/A<", content, StringComparison.Ordinal);
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(content.Replace(">#N/A<", $">{shown}<", StringComparison.Ordinal));
        }

        stream.Position = 0;
        var value = new OdsReader().Read(stream).Require().Sheets[0].GetValue(new CellAddress(0, 0));

        Assert.True(value.IsError);
        Assert.Equal(expected, value.Error);
    }
}
