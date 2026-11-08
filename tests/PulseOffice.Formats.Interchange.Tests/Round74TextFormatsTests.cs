using System.Text;
using PulseOffice.Documents.Blocks;
using PulseOffice.Foundation.Text;
using PulseOffice.Formats.Interchange.Tests.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests;

public sealed class Round74TextFormatsTests
{
    private readonly TextInterchange _formats = new();

    [Fact]
    public void FontNameWithSemicolonSurvivesTheRtfFontTable()
    {
        var rtf = @"{\rtf1\ansi\deff0{\fonttbl{\f0\fnil Liberation Serif;}{\f1\fnil Semi\'3bColon;}}\pard{\f1 x}\par}";
        var first = TextInterchange.Read(Encoding.ASCII.GetBytes(rtf), _formats.RtfReader);
        Assert.Equal("Semi;Colon", ((ParagraphBlock)first.Body[0]).Content.Runs[0].Font);

        var again = TextInterchange.Read(TextInterchange.Bytes(first, _formats.RtfWriter), _formats.RtfReader);

        Assert.Equal("Semi;Colon", ((ParagraphBlock)again.Body[0]).Content.Runs[0].Font);
    }

    [Fact]
    public void GuardedCsvExportPrefixesFormulaLikeTextWithApostrophe()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("=HYPERLINK(\"http://example.invalid\";\"x\")"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("@SUM(1)"));
        sheet.SetValue(new CellAddress(0, 2), CellValue.Text("plain"));
        sheet.SetValue(new CellAddress(0, 3), CellValue.Number(-5));
        var writer = new CsvSpreadsheetWriter(new DelimitedText(), CsvOptions.Default with { GuardFormulas = true });

        using var stream = new MemoryStream();
        writer.Write(workbook, stream);
        var text = Encoding.UTF8.GetString(stream.ToArray()).TrimStart('﻿');

        Assert.StartsWith("\"'=HYPERLINK", text, StringComparison.Ordinal);
        Assert.Contains(",'@SUM(1),plain,-5", text, StringComparison.Ordinal);
        Assert.DoesNotContain(writer.CheckFidelity(workbook).Losses, loss => loss.Code == "ODF259");
    }

    [Fact]
    public void UnguardedCsvExportKeepsTextVerbatim()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Data").SetValue(new CellAddress(0, 0), CellValue.Text("@SUM(1)"));
        using var stream = new MemoryStream();
        new CsvSpreadsheetWriter(new DelimitedText()).Write(workbook, stream);

        Assert.Equal("@SUM(1)", Encoding.UTF8.GetString(stream.ToArray()).Trim());
    }
}
