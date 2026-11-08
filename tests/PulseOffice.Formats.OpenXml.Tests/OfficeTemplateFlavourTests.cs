using System.IO.Compression;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class OfficeTemplateFlavourTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private static string ContentTypes(MemoryStream buffer)
    {
        buffer.Position = 0;
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(archive.GetEntry("[Content_Types].xml")!.Open());
        var text = reader.ReadToEnd();
        buffer.Position = 0;
        return text;
    }

    [Fact]
    public void Il_modello_di_Ledger_si_scrive_e_si_rilegge_come_xltx()
    {
        Assert.True(CellAddress.TryParseA1("B2", out var b2));
        var workbook = new Workbook();
        workbook.AddSheet("Modello").SetValue(b2, CellValue.Number(42));

        var writer = new XlsxSpreadsheetWriter(new SignatureLineImage(), template: true);
        using var buffer = new MemoryStream();
        Assert.True(writer.Write(workbook, buffer).Succeeded);

        Assert.Equal(".xltx", Assert.Single(writer.Format.Extensions));
        Assert.Contains("spreadsheetml.template.main+xml", ContentTypes(buffer), StringComparison.Ordinal);

        var reader = new XlsxSpreadsheetReader(XlsxFormat.Template);
        Assert.True(reader.CanRead(buffer));
        var back = reader.Read(buffer);
        Assert.True(back.Succeeded, string.Join("; ", back.Diagnostics));
        Assert.Equal(CellValue.Number(42), back.Value!.Sheets[0].GetValue(b2));
    }

    [Fact]
    public void Il_modello_di_Deck_si_scrive_e_si_rilegge_come_potx()
    {
        var theme = _themes.Find("Sand")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Standard };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleSlide));

        var writer = new PptxWriter(PptxFlavour.Template);
        using var buffer = new MemoryStream();
        Assert.True(writer.Write(deck, buffer).Succeeded);

        Assert.Equal(".potx", Assert.Single(writer.Format.Extensions));
        Assert.Contains("presentationml.template.main+xml", ContentTypes(buffer), StringComparison.Ordinal);

        var back = new PptxReader(_themes, _masters, PptxFlavour.Template).Read(buffer);
        Assert.True(back.Succeeded, string.Join("; ", back.Diagnostics));
        Assert.Single(back.Value!.Slides);
    }
}
