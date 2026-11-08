using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;
using Workbook = PulseOffice.Sheets.Workbook;
using CellValue = PulseOffice.Sheets.Values.CellValue;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxX14DataBarTests
{
    private const string Classic =
        "<conditionalFormatting xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" sqref=\"A1:A5\">"
        + "<cfRule type=\"dataBar\" priority=\"1\"><dataBar><cfvo type=\"min\"/><cfvo type=\"max\"/><color rgb=\"FF638EC6\"/></dataBar>"
        + "<extLst><ext uri=\"{B025F937-C7B1-47D3-B67F-A62EFF666E3E}\" xmlns:x14=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/main\"><x14:id>{AAAA0001-0000-0000-0000-000000000001}</x14:id></ext></extLst>"
        + "</cfRule></conditionalFormatting>";

    private const string Extension =
        "<ext xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" uri=\"{78C0D931-6437-407d-A8EE-F0AAD7539E65}\" xmlns:x14=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/main\">"
        + "<x14:conditionalFormattings><x14:conditionalFormatting xmlns:xm=\"http://schemas.microsoft.com/office/excel/2006/main\">"
        + "<x14:cfRule type=\"dataBar\" id=\"{AAAA0001-0000-0000-0000-000000000001}\">"
        + "<x14:dataBar minLength=\"0\" maxLength=\"100\" border=\"1\" gradient=\"0\" direction=\"rightToLeft\" negativeBarBorderColorSameAsPositive=\"0\" axisPosition=\"middle\">"
        + "<x14:cfvo type=\"autoMin\"/><x14:cfvo type=\"num\"><xm:f>50</xm:f></x14:cfvo>"
        + "<x14:borderColor rgb=\"FF112233\"/><x14:negativeFillColor rgb=\"FFCC0000\"/><x14:negativeBorderColor rgb=\"FF440000\"/><x14:axisColor rgb=\"FF00AA00\"/>"
        + "</x14:dataBar></x14:cfRule><xm:sqref>A1:A5</xm:sqref></x14:conditionalFormatting>"
        + "<x14:conditionalFormatting xmlns:xm=\"http://schemas.microsoft.com/office/excel/2006/main\">"
        + "<x14:cfRule type=\"dataBar\" id=\"{BBBB0002-0000-0000-0000-000000000002}\" priority=\"4\">"
        + "<x14:dataBar negativeBarColorSameAsPositive=\"1\"><x14:cfvo type=\"autoMin\"/><x14:cfvo type=\"autoMax\"/><x14:fillColor rgb=\"FF5B9BD5\"/></x14:dataBar>"
        + "</x14:cfRule><xm:sqref>C1:C3</xm:sqref></x14:conditionalFormatting>"
        + "</x14:conditionalFormattings></ext>";

    private static Workbook ReadExcelFile()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Number(-3));

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);

        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var worksheet = document.WorkbookPart!.WorksheetParts.First().Worksheet!;
            var data = worksheet.GetFirstChild<SheetData>()!;
            data.InsertAfterSelf(new ConditionalFormatting(Classic));
            var list = worksheet.GetFirstChild<WorksheetExtensionList>() ?? worksheet.AppendChild(new WorksheetExtensionList());
            list.AppendChild(new WorksheetExtension(Extension));
            worksheet.Save();
        }

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return new XlsxReader().Read(stream).Value!;
    }

    [Fact]
    public void LaBarraX14SiCollegaAllaClassicaTramiteIlSuoId()
    {
        var workbook = ReadExcelFile();

        foreach (var sheet in new[] { workbook.Sheets[0], RoundTrip(workbook).Sheets[0] })
        {
            var bars = sheet.ConditionalRules.Where(rule => rule.Visual == ConditionalVisual.DataBar).OrderBy(rule => rule.Range.Start.Column).ToList();
            Assert.Equal(2, bars.Count);

            var linked = bars[0];
            Assert.Equal("A1:A5", linked.RangeText);
            Assert.Equal("#638EC6", linked.BarColorHex);
            Assert.Equal("#CC0000", linked.NegativeBarColorHex);
            Assert.False(linked.Bar.Gradient);
            Assert.Equal(DataBarAxis.Middle, linked.Bar.Axis);
            Assert.Equal("#00AA00", linked.Bar.AxisColorHex);
            Assert.Equal("#112233", linked.Bar.BorderColorHex);
            Assert.Equal("#440000", linked.Bar.NegativeBorderColorHex);
            Assert.Equal(DataBarDirection.RightToLeft, linked.Bar.Direction);
            Assert.Equal(0, linked.Bar.MinimumLength);
            Assert.Equal(100, linked.Bar.MaximumLength);
            Assert.Equal(ConditionalThresholdKind.AutomaticMinimum, linked.Thresholds[0].Kind);
            Assert.Equal(50, linked.Thresholds[1].Value);

            var orphan = bars[1];
            Assert.Equal("C1:C3", orphan.RangeText);
            Assert.Equal("#5B9BD5", orphan.BarColorHex);
            Assert.Null(orphan.NegativeBarColorHex);
            Assert.Empty(orphan.Thresholds);
            Assert.Equal(10, orphan.Bar.MinimumLength);
            Assert.Equal(90, orphan.Bar.MaximumLength);
        }
    }

    [Fact]
    public void LaBarraScrittaPortaLaStessaIdNellaRegolaClassicaENellEstensione()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(4, 0)),
            Visual = ConditionalVisual.DataBar,
            NegativeBarColorHex = "#FF0000",
            ShowValue = false,
            Bar = new DataBarOptions { Axis = DataBarAxis.None, Gradient = false, MinimumLength = 5, MaximumLength = 80 },
        });

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);
        var worksheet = document.WorkbookPart!.WorksheetParts.First().Worksheet!;
        var rule = worksheet.Descendants<ConditionalFormattingRule>().Single();
        var id = rule.Descendants().Single(element => element.LocalName == "id").InnerText;
        var extension = worksheet.GetFirstChild<WorksheetExtensionList>()!.OuterXml;

        Assert.Contains($"id=\"{id}\"", extension, StringComparison.Ordinal);
        Assert.Contains("negativeFillColor rgb=\"FFFF0000\"", extension, StringComparison.Ordinal);
        Assert.Contains("axisPosition=\"none\"", extension, StringComparison.Ordinal);
        Assert.False(rule.GetFirstChild<DataBar>()!.ShowValue!.Value);

        stream.Position = 0;
        var reread = Assert.Single(new XlsxReader().Read(stream).Value!.Sheets[0].ConditionalRules);
        Assert.Equal(workbook.Sheets[0].ConditionalRules[0].Bar, reread.Bar);
        Assert.Equal("#FF0000", reread.NegativeBarColorHex);
        Assert.False(reread.ShowValue);
        Assert.Empty(reread.Thresholds);
    }

    [Fact]
    public void LaBarraNonDichiaraPiuLaPerditaDelColoreNegativo()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(4, 0)),
            Visual = ConditionalVisual.DataBar,
            NegativeBarColorHex = "#FF0000",
            Bar = new DataBarOptions { BorderColorHex = "#000000", Direction = DataBarDirection.LeftToRight },
        });

        Assert.True(new XlsxSpreadsheetWriter().CheckFidelity(workbook).IsPerfect);
    }
}
