using System.IO.Compression;
using System.Text;
using PulseOffice.Document.Metadata;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsSheetFeatureRoundTripTests
{
    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static CellAddress At(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static CellRange Range(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    [Fact]
    public void StileDiPaginaInterruzioniEColonneDaRipetereSopravvivono()
    {
        var source = Workbook.CreateDefault();
        var paper = new PaperCatalog().Find("a3")!;
        source.PageStyles.Define("Report Annuale", new PageSetup
        {
            Paper = paper,
            Orientation = PageOrientation.Landscape,
            Margins = new Margins(Twip.FromMillimeters(15), Twip.FromMillimeters(12), Twip.FromMillimeters(15), Twip.FromMillimeters(18)),
            Header = new HeaderFooter("Riservato", "&A", "&D"),
            Footer = new HeaderFooter(Center: "Pagina &P di &N"),
            Scale = 0.8,
            PrintGridLines = true,
            CenterHorizontally = true,
        });
        var sheet = source.Sheets[0];
        sheet.PageStyleName = "Report Annuale";
        sheet.SetValue(At("A1"), CellValue.Number(1));
        sheet.RowBreaks.Add(10);
        sheet.ColumnBreaks.Add(4);
        sheet.RepeatedPrintColumns = 2;
        sheet.RepeatedPrintRows = 1;

        var read = RoundTrip(source);
        var back = read.Sheets[0];
        Assert.Equal("Report Annuale", back.PageStyleName);
        var setup = read.PageStyles.For(back).Setup;
        Assert.Equal("a3", setup.Paper.Id);
        Assert.Equal(PageOrientation.Landscape, setup.Orientation);
        Assert.InRange(setup.Margins.Top.Value, Twip.FromMillimeters(12).Value - 1, Twip.FromMillimeters(12).Value + 1);
        Assert.Equal(new HeaderFooter("Riservato", "&A", "&D"), setup.Header);
        Assert.Equal("Pagina &P di &N", setup.Footer.Center);
        Assert.Equal(0.8, setup.Scale, 3);
        Assert.True(setup.PrintGridLines);
        Assert.True(setup.CenterHorizontally);
        Assert.Contains(10, back.RowBreaks);
        Assert.Contains(4, back.ColumnBreaks);
        Assert.Equal(2, back.RepeatedPrintColumns);
        Assert.Equal(1, back.RepeatedPrintRows);
    }

    [Fact]
    public void LaFormulaMatricialeClassicaSopravvive()
    {
        var source = Workbook.CreateDefault();
        var sheet = source.Sheets[0];
        sheet.SetArrayFormula(Range("B1:C2"), "=A1:B2*2");
        sheet[At("B1")] = sheet[At("B1")] with { Value = CellValue.Number(2) };
        sheet[At("C2")] = sheet[At("C2")] with { Value = CellValue.Number(8) };

        var back = RoundTrip(source).Sheets[0];
        Assert.True(back[At("B1")].IsArrayAnchor);
        Assert.Equal(Range("B1:C2"), back[At("C2")].ArrayFormulaRange);
        Assert.False(back[At("C2")].IsArrayAnchor);
        Assert.Equal(8, back[At("C2")].Value.AsNumber());
    }

    [Fact]
    public void EtichetteEdEventiDelFoglioSopravvivono()
    {
        var source = Workbook.CreateDefault();
        var sheet = source.Sheets[0];
        sheet.Labels.Add(new LabelRange(Range("A1:C1"), Range("A2:C9"), LabelOrientation.Columns));
        sheet.Labels.Add(new LabelRange(Range("E2:E5"), Range("F2:H5"), LabelOrientation.Rows));
        sheet.Events.Bind(SheetEventKind.ContentChanged, "Ricalcola");
        sheet.Events.Bind(SheetEventKind.Activated, "Benvenuto");

        var back = RoundTrip(source).Sheets[0];
        Assert.Equal(2, back.Labels.All.Count);
        Assert.Equal(LabelOrientation.Rows, back.Labels.For(At("E3"))!.Orientation);
        Assert.Equal(Range("A2:C9"), back.Labels.For(At("B1"))!.Data);
        Assert.Equal("Ricalcola", back.Events.For(SheetEventKind.ContentChanged));
        Assert.Equal("Benvenuto", back.Events.For(SheetEventKind.Activated));
    }

    [Fact]
    public void LeModificheRegistrateELaLoroPasswordSopravvivono()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(At("B2"), CellValue.Number(20));
        source.Changes.IsRecording = true;
        source.Changes.IsProtected = true;
        source.Changes.ProtectionHash = WorkbookProtection.Hash("segreto");
        source.Changes.Import([new TrackedChange
        {
            Id = "ct1",
            SheetIndex = 0,
            Address = At("B2"),
            Before = CellValue.Number(10),
            After = CellValue.Number(20),
            Author = "Ada",
            At = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
            State = ChangeState.Accepted,
        }]);

        var changes = RoundTrip(source).Changes;
        Assert.True(changes.IsRecording);
        Assert.True(changes.IsProtected);
        Assert.True(changes.AcceptsProtection("segreto"));
        Assert.False(changes.AcceptsProtection("altro"));
        var change = Assert.Single(changes.Changes);
        Assert.Equal(10, change.Before.AsNumber());
        Assert.Equal(20, change.After.AsNumber());
        Assert.Equal("Ada", change.Author);
        Assert.Equal(ChangeState.Accepted, change.State);
    }

    [Fact]
    public void LeProprietaPersonalizzateTipizzateSopravvivono()
    {
        var source = Workbook.CreateDefault();
        source.Properties.Custom["Revisione"] = "3";
        source.Properties.CustomKinds["Revisione"] = CustomPropertyKind.Number;
        source.Properties.Custom["Approvato"] = "true";
        source.Properties.CustomKinds["Approvato"] = CustomPropertyKind.Boolean;
        source.Properties.Custom["Reparto"] = "Nord";
        source.Properties.EditingCycles = 7;

        var properties = RoundTrip(source).Properties;
        Assert.Equal(CustomPropertyKind.Number, properties.KindOf("Revisione"));
        Assert.Equal(CustomPropertyKind.Boolean, properties.KindOf("Approvato"));
        Assert.Equal(CustomPropertyKind.Text, properties.KindOf("Reparto"));
        Assert.Equal("3", properties.Custom["Revisione"]);
        Assert.Equal(7, properties.EditingCycles);
    }

    [Fact]
    public void UnaDataEsternoSiLeggeComeDataConIlSuoFormato()
    {
        const string content = """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
              xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
              xmlns:number="urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0"
              xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
              xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" office:version="1.3">
              <office:automatic-styles>
                <number:date-style style:name="N37">
                  <number:day number:style="long"/><number:text>/</number:text>
                  <number:month number:style="long"/><number:text>/</number:text>
                  <number:year number:style="long"/>
                </number:date-style>
                <style:style style:name="ce1" style:family="table-cell" style:data-style-name="N37"/>
              </office:automatic-styles>
              <office:body><office:spreadsheet><table:table table:name="F">
                <table:table-row>
                  <table:table-cell table:style-name="ce1" office:value-type="date" office:date-value="2026-03-15"><text:p>15/03/2026</text:p></table:table-cell>
                </table:table-row>
              </table:table></office:spreadsheet></office:body>
            </office:document-content>
            """;

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("mimetype", CompressionLevel.NoCompression).Open()))
                writer.Write("application/vnd.oasis.opendocument.spreadsheet");
            using (var writer = new StreamWriter(archive.CreateEntry("content.xml").Open(), new UTF8Encoding(false)))
                writer.Write(content);
        }
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        var read = result.Value!;
        var cell = read.Sheets[0][new CellAddress(0, 0)];

        Assert.Equal(new DateTime(2026, 3, 15).ToOADate(), cell.Value.AsNumber());
        Assert.Equal("dd/mm/yyyy", read.NumberFormats.CodeOf(read.Styles.Get(cell.StyleId).NumberFormatId));
    }

    [Fact]
    public void FiltroAutomaticoEAreeDatiSopravvivono()
    {
        var source = Workbook.CreateDefault();
        var sheet = source.Sheets[0];
        sheet.SetValue(At("A1"), CellValue.Text("Zona"));
        sheet.SetValue(At("A2"), CellValue.Text("Nord"));
        sheet.AutoFilter.Activate(Range("A1:B5"), true);
        sheet.AutoFilter.Set(new ColumnFilter(0) { AllowedValues = new HashSet<string>(["Nord"], StringComparer.Ordinal) });
        source.DatabaseRanges.Define(new DatabaseRange { Name = "Vendite", SheetIndex = 0, Range = Range("D1:F9"), HasHeader = true });

        var read = RoundTrip(source);
        Assert.Equal(Range("A1:B5"), read.Sheets[0].AutoFilter.Range);
        Assert.Contains("Nord", read.Sheets[0].AutoFilter.Filters.Single().AllowedValues!);
        Assert.Equal(Range("D1:F9"), Assert.Single(read.DatabaseRanges.All).Range);
    }
}
