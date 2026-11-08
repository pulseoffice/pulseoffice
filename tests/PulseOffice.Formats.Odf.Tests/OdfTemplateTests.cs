using System.IO.Compression;
using System.Text;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfTemplateTests
{
    private static string MimeTypeOf(Stream stream)
    {
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(zip.GetEntry("mimetype")!.Open(), Encoding.ASCII);
        return reader.ReadToEnd();
    }

    [Fact]
    public void UnModelloDiPresentazioneHaIlSuoTipoESiRilegge()
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var theme = themes.Find("Light")!;
        var master = masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleSlide));

        var writer = new OdpWriter(template: true);
        using var stream = new MemoryStream();
        Assert.True(writer.Write(deck, stream).Succeeded);

        Assert.Equal(".otp", writer.Format.Extensions.Single());
        Assert.Equal(OdfPackage.MimeTypes.PresentationTemplate, MimeTypeOf(stream));

        stream.Position = 0;
        var reader = new OdpReader(themes, masters);
        Assert.True(reader.CanRead(stream));
        Assert.Single(reader.Read(stream).Value!.Slides);
    }

    [Fact]
    public void UnModelloDiFoglioHaIlSuoTipoESiRilegge()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Text("Intestazione"));

        var writer = new OdsSpreadsheetWriter(template: true);
        using var stream = new MemoryStream();
        Assert.True(writer.Write(workbook, stream).Succeeded);

        Assert.Equal(".ots", writer.Format.Extensions.Single());
        Assert.Equal(OdfPackage.MimeTypes.SpreadsheetTemplate, MimeTypeOf(stream));

        stream.Position = 0;
        var reader = new OdsSpreadsheetReader();
        Assert.True(reader.CanRead(stream));
        Assert.Contains(".ots", reader.Format.Extensions);
        Assert.Single(reader.Read(stream).Value!.Sheets);
    }

    [Fact]
    public void IlFormatoNativoNonCambia()
    {
        Assert.Equal(".odp", new OdpWriter().Format.Extensions.Single());
        Assert.True(new OdpWriter().Format.IsNative);
        Assert.False(new OdpWriter(template: true).Format.IsNative);
        Assert.Equal(OdsFormat.Descriptor, new OdsSpreadsheetWriter().Format);
    }
}
