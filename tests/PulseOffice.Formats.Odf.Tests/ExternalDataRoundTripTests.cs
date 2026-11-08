using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class ExternalDataRoundTripTests
{
    private static CellRange R(string text) => CellRange.TryParseA1(text, out var range) ? range : throw new ArgumentException(text);

    [Fact]
    public void CollegamentiStreamEFornitoriSopravvivonoInOds()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(2, 1), CellValue.Text("Zona"));
        sheet.SetValue(new CellAddress(40, 5), CellValue.Number(7));
        workbook.ExternalData.Replace(new AreaLink
        {
            SheetIndex = 0, Anchor = new CellAddress(2, 1), Url = "https://example.org/dati.html",
            Format = ExternalDataFormat.Html, Source = "HTML_2", Columns = 3, Rows = 4,
        });
        workbook.ExternalData.Replace(new AreaLink
        {
            SheetIndex = 0, Anchor = new CellAddress(40, 5), Url = "/tmp/a.csv", Format = ExternalDataFormat.Csv,
        });
        workbook.ExternalData.Stream = new DataStreamSource
        {
            Url = "/tmp/stream.csv", SheetIndex = 0, Range = R("H1:J10"), Insertion = StreamInsertion.Top, RefreshOnEmptyLine = true,
        };
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Data1", SheetIndex = 0, Range = R("L1:M5") });
        workbook.ExternalData.Replace(new DataMapping { Id = "HTML_1", Url = "https://example.org/p.html", Provider = ExternalDataFormat.Html, DatabaseName = "Data1" });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var back = new OdsReader().Read(stream).Value!;

        var links = back.ExternalData.AreaLinks.OrderBy(link => link.Anchor.Row).ToList();
        Assert.Equal(2, links.Count);
        Assert.Equal(new CellAddress(2, 1), links[0].Anchor);
        Assert.Equal("https://example.org/dati.html", links[0].Url);
        Assert.Equal("HTML_2", links[0].Source);
        Assert.Equal(3, links[0].Columns);
        Assert.Equal(4, links[0].Rows);
        Assert.Equal(ExternalDataFormat.Csv, links[1].Format);
        Assert.Equal(new CellAddress(40, 5), links[1].Anchor);
        Assert.Equal("Zona", back.Sheets[0].GetValue(new CellAddress(2, 1)).AsText());

        Assert.NotNull(back.ExternalData.Stream);
        Assert.Equal(R("H1:J10"), back.ExternalData.Stream!.Range);
        Assert.Equal(StreamInsertion.Top, back.ExternalData.Stream.Insertion);
        Assert.True(back.ExternalData.Stream.RefreshOnEmptyLine);

        var mapping = Assert.Single(back.ExternalData.Mappings);
        Assert.Equal("Data1", mapping.DatabaseName);
        Assert.Equal(ExternalDataFormat.Html, mapping.Provider);
        Assert.Equal("HTML_1", mapping.Id);
    }

    [Fact]
    public void LIntervalloDiAggiornamentoSopravviveInOdsEUnoStranoSiIgnora()
    {
        var workbook = Workbook.CreateDefault();
        workbook.ExternalData.Replace(new AreaLink
        {
            SheetIndex = 0, Anchor = new CellAddress(0, 0), Url = "/tmp/a.csv", Format = ExternalDataFormat.Csv,
            RefreshSeconds = 90, Rows = 70_000,
        });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var back = new OdsReader().Read(stream).Value!;

        var link = Assert.Single(back.ExternalData.AreaLinks);
        Assert.Equal(90, link.RefreshSeconds);
        Assert.Equal(70_000, link.Rows);
    }

    [Fact]
    public void CollegamentiSeguonoFogliRigheEColonne()
    {
        var workbook = Workbook.CreateDefault();
        var second = workbook.AddSheet("Dati");
        workbook.ExternalData.Replace(new AreaLink { SheetIndex = 1, Anchor = new CellAddress(4, 2), Url = "a.csv", Rows = 3, Columns = 2 });
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Data1", SheetIndex = 1, Range = R("A1:B2") });

        workbook.InsertSheet("Primo", 0);
        Assert.Equal(2, workbook.ExternalData.AreaLinks[0].SheetIndex);
        Assert.Equal(2, workbook.DatabaseRanges.Find("Data1")!.SheetIndex);

        workbook.MoveSheet(2, 0);
        Assert.Equal(0, workbook.ExternalData.AreaLinks[0].SheetIndex);

        workbook.RemoveSheet(second);
        Assert.Empty(workbook.ExternalData.AreaLinks);
        Assert.Null(workbook.DatabaseRanges.Find("Data1"));

        workbook.InsertSheet(second, 1);
        Assert.Equal(1, workbook.ExternalData.AreaLinks[0].SheetIndex);
        Assert.Equal(1, workbook.DatabaseRanges.Find("Data1")!.SheetIndex);

    }
}
