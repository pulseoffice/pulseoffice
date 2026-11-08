using PulseOffice.Document.Metadata;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxSheetFeatureRoundTripTests
{
    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
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
    public void ImpostazioneDellaPaginaInterruzioniETitoliSopravvivono()
    {
        var source = Workbook.CreateDefault();
        source.PageStyles.Define("Report", new PageSetup
        {
            Paper = new PaperCatalog().Find("letter")!,
            Orientation = PageOrientation.Landscape,
            Margins = new Margins(Twip.FromInches(0.5), Twip.FromInches(0.3), Twip.FromInches(0.5), Twip.FromInches(0.3)),
            HeaderHeight = Twip.FromInches(0.4),
            FooterHeight = Twip.FromInches(0.4),
            Header = new HeaderFooter("Riservato", "&A", "&D"),
            Footer = new HeaderFooter(Center: "Pagina &P di &N"),
            PrintHeadings = true,
        });
        var sheet = source.Sheets[0];
        sheet.PageStyleName = "Report";
        sheet.SetValue(At("A1"), CellValue.Number(1));
        sheet.RowBreaks.Add(20);
        sheet.ColumnBreaks.Add(3);
        sheet.RepeatedPrintColumns = 2;
        sheet.RepeatedPrintRows = 3;

        var read = RoundTrip(source);
        var back = read.Sheets[0];
        var setup = read.PageStyles.For(back).Setup;
        Assert.Equal("letter", setup.Paper.Id);
        Assert.Equal(PageOrientation.Landscape, setup.Orientation);
        Assert.Equal(Twip.FromInches(0.3), setup.Margins.Top);
        Assert.Equal(Twip.FromInches(0.4), setup.HeaderHeight);
        Assert.Equal(new HeaderFooter("Riservato", "&A", "&D"), setup.Header);
        Assert.Equal("Pagina &P di &N", setup.Footer.Center);
        Assert.True(setup.PrintHeadings);
        Assert.Contains(20, back.RowBreaks);
        Assert.Contains(3, back.ColumnBreaks);
        Assert.Equal(2, back.RepeatedPrintColumns);
        Assert.Equal(3, back.RepeatedPrintRows);
    }

    [Fact]
    public void LaFormulaMatricialeClassicaSopravvive()
    {
        var source = Workbook.CreateDefault();
        var sheet = source.Sheets[0];
        sheet.SetArrayFormula(Range("B1:C2"), "=A1:B2*2");
        sheet[At("C2")] = sheet[At("C2")] with { Value = CellValue.Number(8) };

        var back = RoundTrip(source).Sheets[0];
        Assert.True(back[At("B1")].IsArrayAnchor);
        Assert.NotNull(back[At("B1")].Formula);
        Assert.Equal(Range("B1:C2"), back[At("C2")].ArrayFormulaRange);
        Assert.Equal(8, back[At("C2")].Value.AsNumber());
    }

    [Fact]
    public void ProprietaTipizzateERevisioneSopravvivono()
    {
        var source = Workbook.CreateDefault();
        source.Properties.Custom["Pezzi"] = "12";
        source.Properties.CustomKinds["Pezzi"] = CustomPropertyKind.Number;
        source.Properties.Custom["Peso"] = "2.5";
        source.Properties.CustomKinds["Peso"] = CustomPropertyKind.Number;
        source.Properties.Custom["Approvato"] = "true";
        source.Properties.CustomKinds["Approvato"] = CustomPropertyKind.Boolean;
        source.Properties.Custom["Scadenza"] = "2026-05-01T00:00:00";
        source.Properties.CustomKinds["Scadenza"] = CustomPropertyKind.Date;
        source.Properties.Custom["Reparto"] = "Nord";
        source.Properties.EditingCycles = 5;

        var properties = RoundTrip(source).Properties;
        Assert.Equal(CustomPropertyKind.Number, properties.KindOf("Pezzi"));
        Assert.Equal("12", properties.Custom["Pezzi"]);
        Assert.Equal("2.5", properties.Custom["Peso"]);
        Assert.Equal(CustomPropertyKind.Boolean, properties.KindOf("Approvato"));
        Assert.Equal(CustomPropertyKind.Date, properties.KindOf("Scadenza"));
        Assert.Equal("2026-05-01T00:00:00", properties.Custom["Scadenza"]);
        Assert.Equal(CustomPropertyKind.Text, properties.KindOf("Reparto"));
        Assert.Equal(5, properties.EditingCycles);
    }

    [Fact]
    public void IlFormatoNumericoPersonalizzatoSiRileggeNelCatalogo()
    {
        var source = Workbook.CreateDefault();
        var id = source.NumberFormats.Register("0.000\" kg\"");
        source.Sheets[0].SetValue(At("A1"), CellValue.Number(2));
        source.Sheets[0].SetStyle(At("A1"), source.Styles.Intern(new CellStyle { NumberFormatId = id }));

        var read = RoundTrip(source);
        Assert.Equal("0.000\" kg\"", read.NumberFormats.CodeOf(read.Styles.Get(read.Sheets[0][At("A1")].StyleId).NumberFormatId));
    }

    [Fact]
    public void LaProtezioneDelleModificheSiDichiaraPersa()
    {
        var source = Workbook.CreateDefault();
        source.Changes.IsProtected = true;
        source.Changes.ProtectionHash = WorkbookProtection.Hash("x");

        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(source).Losses, loss => loss.Code == "XLSX032");
    }

    [Fact]
    public void IlFiltroAutomaticoSopravvive()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(At("A1"), CellValue.Text("Zona"));
        source.Sheets[0].AutoFilter.Activate(Range("A1:B5"), true);

        Assert.Equal(Range("A1:B5"), RoundTrip(source).Sheets[0].AutoFilter.Range);
    }
}
