using System.IO.Compression;
using System.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsTrailingBlankCellsTests
{
    private const string Trailing = "<table:table-cell table:style-name=\"Default\" table:number-columns-repeated=\"16382\" />";

    [Fact]
    public void LaCodaDiCelleVuoteEsternoNonCreaCelleELaColonnaStilizzataTieneIlSuoStile()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(2));
        sheet.Layout.SetColumnStyle(2, workbook.Styles.Intern(new CellStyle { NumberFormatId = 14 }));

        var back = Read(WithTrailingCells(Write(workbook)));
        var read = back.Sheets[0];

        Assert.Equal(CellValue.Number(1), read.GetValue(new CellAddress(0, 0)));
        Assert.Equal(read.StyleIdAt(new CellAddress(5, 2)), read.StyleIdAt(new CellAddress(0, 2)));
        Assert.NotEqual(StyleCatalog.DefaultStyleId, read.StyleIdAt(new CellAddress(5, 2)));
        Assert.Equal(2, read.CellCount);
    }

    private byte[] Write(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        return stream.ToArray();
    }

    private Workbook Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private byte[] WithTrailingCells(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in source.Entries)
            {
                using var input = new StreamReader(entry.Open(), Encoding.UTF8);
                var text = input.ReadToEnd();
                var copy = target.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                using var writer = new StreamWriter(copy.Open(), new UTF8Encoding(false));
                writer.Write(entry.FullName == "content.xml" ? Rows(text) : text);
            }
        }
        return output.ToArray();
    }

    private string Rows(string content)
    {
        var builder = new StringBuilder();
        var index = 0;
        while (content.IndexOf("</table:table-row>", index, StringComparison.Ordinal) is var end and >= 0)
        {
            builder.Append(content, index, end - index);
            builder.Append(Trailing).Append("</table:table-row>");
            index = end + "</table:table-row>".Length;
        }
        builder.Append(content, index, content.Length - index);
        Assert.Contains(Trailing, builder.ToString());
        return builder.ToString();
    }
}
