using System.IO.Compression;
using System.Text;
using PulseOffice.Charts;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsTenthAuditTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static byte[] Write(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        return stream.ToArray();
    }

    private static Workbook Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static Workbook RoundTrip(Workbook workbook) => Read(Write(workbook));

    private static string Content(byte[] bytes)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] Replace(byte[] bytes, Func<string, string> change)
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
                if (entry.FullName == "content.xml")
                {
                    var changed = change(text);
                    Assert.NotEqual(text, changed);
                    text = changed;
                }
                writer.Write(text);
            }
        }
        return output.ToArray();
    }

    [Fact]
    public void IGruppiDopoLeRigheELeColonneDaRipetereNonSiSpostano()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A20"), CellValue.Number(1));
        sheet.RepeatedPrintRows = 2;
        sheet.RepeatedPrintColumns = 1;
        sheet.RowGroups.Add(new OutlineGroup { First = 11, Last = 13 });
        sheet.ColumnGroups.Add(new OutlineGroup { First = 4, Last = 5 });

        var back = RoundTrip(workbook).Sheets[0];

        Assert.Equal((11, 13), (Assert.Single(back.RowGroups).First, back.RowGroups[0].Last));
        Assert.Equal((4, 5), (Assert.Single(back.ColumnGroups).First, back.ColumnGroups[0].Last));
    }

    [Fact]
    public void LoScenarioSegueSubitoIlSuoFoglio()
    {
        var workbook = new Workbook();
        var first = workbook.AddSheet("Primo");
        workbook.AddSheet("Secondo");
        first.SetValue(A("A1"), CellValue.Number(1));
        first.Scenarios.Add(new Scenario
        {
            Name = "Alto",
            Range = new CellRange(A("A1"), A("A1")),
            Values = new Dictionary<CellAddress, CellValue> { [A("A1")] = CellValue.Number(9) },
        });

        var content = Content(Write(workbook));

        Assert.True(content.IndexOf("table:name=\"Alto\"", StringComparison.Ordinal) < content.IndexOf("table:name=\"Secondo\"", StringComparison.Ordinal));
    }

    [Fact]
    public void LoScenarioAttribuitoPerPosizioneNonPortaINomiLocaliDelFoglio()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        workbook.AddSheet("Altro");
        sheet.SetValue(A("A1"), CellValue.Number(1));
        sheet.Scenarios.Add(new Scenario
        {
            Name = "Base",
            Range = new CellRange(A("A1"), A("A1")),
            Values = new Dictionary<CellAddress, CellValue> { [A("A1")] = CellValue.Number(9) },
        });
        workbook.DefineName(new DefinedName("Locale", "Dati.$A$1", "Dati"));

        var bytes = Replace(Write(workbook), text => text.Replace(
            "<table:table-column table:number-columns-repeated=\"1\" />",
            "<table:named-expressions><table:named-range table:name=\"Locale\" table:base-cell-address=\"$Base.$A$1\" table:cell-range-address=\"$Base.$A$1\" /></table:named-expressions><table:table-column table:number-columns-repeated=\"1\" />",
            StringComparison.Ordinal));
        var back = Read(bytes);

        Assert.Single(back.Sheets[0].Scenarios);
        Assert.DoesNotContain(back.DefinedNames, name => name.Scope == "Base");
    }

    [Fact]
    public void UnErroreCostanteSiScriveComeFormulaESiRileggeComeValore()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(A("A1"), CellValue.FromError(CellError.DivideByZero));
        workbook.Sheets[0].SetValue(A("A2"), CellValue.True);

        var bytes = Write(workbook);
        Assert.Contains("of:=#DIV/0!", Content(bytes), StringComparison.Ordinal);

        var withForeignBoolean = Replace(bytes, text => text.Replace(
            "office:value-type=\"boolean\"", "table:formula=\"of:=TRUE()\" office:value-type=\"boolean\"", StringComparison.Ordinal));
        var back = Read(withForeignBoolean).Sheets[0];

        Assert.Equal(CellError.DivideByZero, back[A("A1")].Value.Error);
        Assert.Null(back[A("A1")].Formula);
        Assert.Null(back[A("A2")].Formula);
        Assert.True(back[A("A2")].Value.AsBoolean());
    }

    [Fact]
    public void IlCollegamentoSuUnaCellaNumericaViaggiaAncheNelloStileComeNelRiferimento()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("B2"), CellValue.Number(5));
        sheet[A("B2")] = sheet[A("B2")] with { Hyperlink = "https://example.org/n" };

        var bytes = Write(workbook);
        Assert.Contains("<style:hyperlink", Content(bytes), StringComparison.Ordinal);

        var styleOnly = Replace(bytes, text => System.Text.RegularExpressions.Regex.Replace(
            text, "<text:a [^>]*>(5)</text:a>", "$1"));
        Assert.Equal("https://example.org/n", Read(styleOnly).Sheets[0][A("B2")].Hyperlink);
    }

    [Fact]
    public void LeEccezioniDellaProtezioneDelFoglioSopravvivonoELeAltreSiDichiarano()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Protection = new SheetProtection
        {
            IsEnabled = true,
            AllowSelectLockedCells = false,
            AllowInsertRows = true,
            AllowDeleteColumns = true,
            AllowFilter = true,
            AllowFormatCells = true,
        };

        var back = RoundTrip(workbook).Sheets[0].Protection;

        Assert.False(back.AllowSelectLockedCells);
        Assert.True(back.AllowSelectUnlockedCells);
        Assert.True(back.AllowInsertRows);
        Assert.False(back.AllowInsertColumns);
        Assert.True(back.AllowDeleteColumns);
        Assert.True(back.AllowFilter);
        Assert.Contains(new OdsSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "ODF026");
    }

    [Fact]
    public void LaLarghezzaEAltezzaPredefiniteDelFoglioSiScrivonoESiRileggono()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Layout.DefaultColumnWidth = Twip.FromPoints(70);
        sheet.Layout.DefaultRowHeight = Twip.FromPoints(18);
        sheet.Layout.SetColumnWidth(2, Twip.FromPoints(120));
        sheet.SetValue(A("A1"), CellValue.Number(1));

        var bytes = Write(workbook);
        var back = Read(bytes).Sheets[0];

        Assert.Equal(70, back.Layout.DefaultColumnWidth.ToPoints(), 0);
        Assert.Equal(18, back.Layout.DefaultRowHeight.ToPoints(), 0);
        Assert.Equal(120, back.Layout.ColumnWidth(2).ToPoints(), 0);
        Assert.Single(back.Layout.ColumnWidths);
        Assert.DoesNotContain("<table:table-column />", Content(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void UnGraficoSuUnaSolaColonnaNonPuntaFuoriDallIntervallo()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 4; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        sheet.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = A("D2"),
            Content = new ChartDefinition { Id = "g", Type = ChartType.Line, SourceSheet = sheet.Name, SourceRange = "A1:A4" },
        });

        using var archive = new ZipArchive(new MemoryStream(Write(workbook)), ZipArchiveMode.Read);
        var chart = archive.Entries.First(entry => entry.FullName.EndsWith("content.xml", StringComparison.Ordinal) && entry.FullName.Contains("Object", StringComparison.Ordinal));
        using var reader = new StreamReader(chart.Open());
        var text = reader.ReadToEnd();

        Assert.DoesNotContain(".B", text, StringComparison.Ordinal);
        Assert.Contains(".A2:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LImmagineDiRiservaDiUnGraficoNonDiventaUnaSecondaImmagine()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), CellValue.Number(1));
        sheet.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = A("D2"),
            Content = new ChartDefinition { Id = "g", Type = ChartType.Column, SourceSheet = sheet.Name, SourceRange = "A1:A1" },
        });
        sheet.Objects.Add(new PlacedObject
        {
            Id = "foto",
            Anchor = A("D20"),
            Content = new RasterImageObject { Id = "foto", Data = Png, MediaType = "image/png" },
        });

        var bytes = Replace(Write(workbook), text => text.Replace(
            "xlink:actuate=\"onLoad\" draw:notify-on-update-of-ranges=\"A1:A1\" />",
            "xlink:actuate=\"onLoad\" draw:notify-on-update-of-ranges=\"A1:A1\" /><draw:image xlink:href=\""
                + System.Text.RegularExpressions.Regex.Match(text, "<draw:image xlink:href=\"([^\"]+)\"").Groups[1].Value + "\" />",
            StringComparison.Ordinal));
        var objects = Read(bytes).Sheets[0].Objects;

        Assert.Single(objects, placed => placed.Content is RasterImageObject);
        Assert.Single(objects, placed => placed.Content is ChartDefinition);
    }

    [Fact]
    public void UnOggettoInUnaColonnaNascostaResta()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Layout.HideColumn(3);
        sheet.Layout.HideRow(4);
        sheet.Objects.Add(new PlacedObject
        {
            Id = "foto",
            Anchor = A("D5"),
            Content = new RasterImageObject { Id = "foto", Data = Png, MediaType = "image/png" },
        });

        Assert.Equal(A("D5"), Assert.Single(RoundTrip(workbook).Sheets[0].Objects).Anchor);
    }

    [Fact]
    public void UnNomeDiFoglioCheUnAltraSuiteRifiutaSiDichiara()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Conti 'x'");

        Assert.Contains(new OdsSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "ODF027");
    }

    [Fact]
    public void LeColonneNonDichiarateInCodaTengonoLaLarghezzaPredefinita()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Layout.SetColumnWidth(0, Twip.FromPoints(14));
        sheet.Layout.SetColumnWidth(1, Twip.FromPoints(14));
        sheet.SetValue(A("A1"), CellValue.Number(1));
        var standard = sheet.Layout.DefaultColumnWidth;

        var bytes = Replace(Write(workbook), content =>
        {
            var columns = System.Text.RegularExpressions.Regex.Matches(content, "<table:table-column [^>]*/>");
            var first = columns[0].Value;
            var narrow = System.Text.RegularExpressions.Regex.Match(first, "table:style-name=\"([^\"]+)\"").Groups[1].Value;
            var kept = $"<table:table-column table:style-name=\"{narrow}\" table:number-columns-repeated=\"2\"/>";
            var stripped = System.Text.RegularExpressions.Regex.Replace(content, "<table:table-column [^>]*/>", string.Empty);
            var firstRow = stripped.IndexOf("<table:table-row", StringComparison.Ordinal);
            return stripped.Insert(firstRow, kept);
        });
        var back = Read(bytes).Sheets[0];

        Assert.Equal(standard, back.Layout.DefaultColumnWidth);
        Assert.Equal(14, back.Layout.ColumnWidth(0).ToPoints(), 0);
        Assert.Equal(standard, back.Layout.ColumnWidth(2));
    }
}
