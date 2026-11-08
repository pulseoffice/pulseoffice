using System.IO.Compression;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsForeignContentTests
{
    private static MemoryStream Patched(PulseOffice.Sheets.Workbook workbook, Func<string, string> patch)
    {
        var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(patch(content));
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void UnoSfondoTrasparenteNonDiventaUnColore()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(new CellAddress(0, 0), CellValue.Number(1));
        workbook.Sheets[0].SetStyle(new CellAddress(0, 0), workbook.Styles.Intern(CellStyle.Default with { BackgroundHex = "#ABCDEF" }));

        using var stream = Patched(workbook, content => content.Replace("\"#ABCDEF\"", "\"transparent\"", StringComparison.OrdinalIgnoreCase));
        var read = new OdsReader().Read(stream).Require();

        Assert.Null(read.Styles.Get(read.Sheets[0].StyleIdAt(new CellAddress(0, 0))).BackgroundHex);
    }

    [Fact]
    public void IlContenutoOltreIlimitiDelFoglioNonSiCaricaESiSegnala()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(new CellAddress(0, 0), CellValue.Number(1));

        using var stream = Patched(workbook, content => content.Replace("</table:table-row>",
            "<table:table-cell table:number-columns-repeated=\"16384\"/><table:table-cell office:value-type=\"float\" office:value=\"3\"><text:p>3</text:p></table:table-cell></table:table-row>",
            StringComparison.Ordinal));
        var result = new OdsReader().Read(stream);

        Assert.All(result.Require().Sheets[0].NonEmptyCells(), pair => Assert.True(pair.Key.IsValid));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ODF043");
    }

    private static PulseOffice.Sheets.Workbook OneValue()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(new CellAddress(0, 0), CellValue.Number(7));
        return workbook;
    }

    [Fact]
    public void A_value_row_repeated_ten_thousand_times_is_read_in_full()
    {
        using var stream = Patched(OneValue(), content => new System.Text.RegularExpressions.Regex("<table:table-row").Replace(content, "<table:table-row table:number-rows-repeated=\"10000\"", 1));
        var result = new OdsReader().Read(stream);
        var sheet = result.Require().Sheets[0];

        Assert.Equal(7, sheet.GetValue(new CellAddress(9_999, 0)).AsNumber());
        Assert.Equal(10_000, sheet.NonEmptyCells().Count());
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "ODF270");
    }

    [Fact]
    public void A_value_cell_repeated_across_five_thousand_columns_is_read_in_full()
    {
        using var stream = Patched(OneValue(), content => new System.Text.RegularExpressions.Regex("<table:table-cell office:value-type").Replace(content, "<table:table-cell table:number-columns-repeated=\"5000\" office:value-type", 1));
        var sheet = new OdsReader().Read(stream).Require().Sheets[0];

        Assert.Equal(7, sheet.GetValue(new CellAddress(0, 4_999)).AsNumber());
    }

    [Fact]
    public void A_repeated_block_beyond_the_cell_budget_is_cut_and_reported()
    {
        using var stream = Patched(OneValue(), content => new System.Text.RegularExpressions.Regex("<table:table-row").Replace(content, "<table:table-row table:number-rows-repeated=\"1048576\"", 1)
            .Replace("<table:table-cell office:value-type", "<table:table-cell table:number-columns-repeated=\"16384\" office:value-type", StringComparison.Ordinal));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = new OdsReader().Read(stream);

        Assert.True(watch.ElapsedMilliseconds < 60_000, $"{watch.ElapsedMilliseconds} ms");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ODF270");
    }

    [Fact]
    public void Hidden_rows_repeated_beyond_four_thousand_stay_hidden()
    {
        using var stream = Patched(OneValue(), content => new System.Text.RegularExpressions.Regex("<table:table-row").Replace(content, "<table:table-row table:visibility=\"collapse\" table:number-rows-repeated=\"10000\"", 1));
        var sheet = new OdsReader().Read(stream).Require().Sheets[0];

        Assert.True(sheet.Layout.IsRowHidden(9_999));
        Assert.False(sheet.Layout.IsRowHidden(10_000));
    }

    [Fact]
    public void Formatting_filler_repeated_to_the_sheet_end_does_not_use_up_the_budget_of_later_content()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        workbook.Sheets[0].SetStyle(new CellAddress(0, 0), workbook.Styles.Intern(CellStyle.Default with { BackgroundHex = "#ABCDEF" }));
        var second = workbook.AddSheet("Second");
        second.SetValue(new CellAddress(0, 0), CellValue.Number(7));

        using var stream = Patched(workbook, content => new System.Text.RegularExpressions.Regex("<table:table-cell table:style-name").Replace(
            new System.Text.RegularExpressions.Regex("<table:table-row").Replace(content, "<table:table-row table:number-rows-repeated=\"1000000\"", 1),
            "<table:table-cell table:number-columns-repeated=\"1000\" table:style-name", 1));
        var result = new OdsReader().Read(stream);

        Assert.Equal(7, result.Require().Sheets[1].GetValue(new CellAddress(0, 0)).AsNumber());
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "ODF270");
    }
}
