using PulseOffice.Formats.Packaging.Encryption;
using PulseOffice.Formats.Packaging.Legacy;
using PulseOffice.Presentations;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class LegacyFormatTests
{
    private static MemoryStream Fixture(string name) =>
        new(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Legacy", name)));

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public void UnFoglioOpenOffice1SiApre()
    {
        using var stream = Fixture("Calc_6.sxc");
        var reader = new OdsSpreadsheetReader();

        Assert.True(reader.CanRead(stream));
        var result = reader.Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        var sheet = result.Value!.Sheets[0];
        Assert.Equal("NAT_NUM_1", sheet.Name);
        Assert.Contains(sheet.NonEmptyCells(), pair => sheet.GetValue(pair.Key) == CellValue.Number(12345.678));
    }

    [Fact]
    public void Forme_ImmaginiEControlliDiOpenOffice1SiLeggono()
    {
        using var stream = Fixture("calcshapes.sxc");

        var workbook = new OdsSpreadsheetReader().Read(stream).Value!;
        var kinds = workbook.Sheets.SelectMany(sheet => sheet.Objects).Select(placed => placed.Content.GetType().Name).ToList();

        Assert.Equal(9, kinds.Count);
        Assert.Contains("RasterImageObject", kinds);
        Assert.Equal(4, kinds.Count(kind => kind == "FormControlObject"));
    }

    [Fact]
    public void UnaPresentazioneOpenOffice1SiApre()
    {
        using var stream = Fixture("imp1.sxi");
        var reader = new OdpReader(new ThemeCatalog(), new MasterFactory());

        Assert.True(reader.CanRead(stream));
        var result = reader.Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        var deck = result.Value!;
        Assert.Equal(18, deck.Slides.Count);
        var titles = deck.Slides.SelectMany(slide => slide.Elements).OfType<TextElement>().Select(text => text.Text.PlainText).ToList();
        Assert.Contains(titles, text => text.Contains("Goals", StringComparison.Ordinal));
    }

    [Fact]
    public void IlPacchettoOpenOffice1CifratoSiRiconosceESiApre()
    {
        using var stream = Fixture("passwordWrongSHA.ods");
        var encryption = new OdfPackageEncryption();

        Assert.True(encryption.IsEncrypted(stream));
        Assert.Equal("ODF004", new OdsSpreadsheetReader().Read(stream).Diagnostics[0].Code);
        Assert.Equal(OdfDecryptionStatus.WrongPassword, encryption.Decrypt(stream, new Packaging.Encryption.OdfPassword("sbagliata")).Status);

        stream.Position = 0;
        var result = encryption.Decrypt(stream, new Packaging.Encryption.OdfPassword("test"));

        Assert.Equal(OdfDecryptionStatus.Decrypted, result.Status);
        var read = new OdsSpreadsheetReader().Read(result.Package!);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics));
        Assert.NotEmpty(read.Value!.Sheets);
    }

    [Fact]
    public void IlVocabolarioDiStileSiDivideNelleProprietaOdf()
    {
        const string legacy = """
            <office:document-content xmlns:office="http://openoffice.org/2000/office" xmlns:style="http://openoffice.org/2000/style" xmlns:fo="http://www.w3.org/1999/XSL/Format" xmlns:table="http://openoffice.org/2000/table" xmlns:text="http://openoffice.org/2000/text" office:class="spreadsheet">
              <office:automatic-styles>
                <style:style style:name="ce1" style:family="table-cell">
                  <style:properties fo:font-weight="bold" fo:background-color="#ff0000" fo:text-align="center" style:text-underline="double"/>
                </style:style>
              </office:automatic-styles>
              <office:body>
                <table:table table:name="Foglio">
                  <table:table-row><table:table-cell table:style-name="ce1" table:value-type="float" table:value="42"><text:p>42</text:p></table:table-cell></table:table-row>
                </table:table>
              </office:body>
            </office:document-content>
            """;

        var translated = new LegacyXmlTranslator().Translate(System.Xml.Linq.XDocument.Parse(legacy));
        var style = Packaging.OdfPackage.Namespaces.Style;
        var office = Packaging.OdfPackage.Namespaces.Office;
        var styleElement = translated.Descendants(style + "style").Single();

        Assert.Equal("bold", (string?)styleElement.Element(style + "text-properties")?.Attribute(Packaging.OdfPackage.Namespaces.Fo + "font-weight"));
        Assert.Equal("double", (string?)styleElement.Element(style + "text-properties")?.Attribute(style + "text-underline-type"));
        Assert.Equal("#ff0000", (string?)styleElement.Element(style + "table-cell-properties")?.Attribute(Packaging.OdfPackage.Namespaces.Fo + "background-color"));
        Assert.Equal("center", (string?)styleElement.Element(style + "paragraph-properties")?.Attribute(Packaging.OdfPackage.Namespaces.Fo + "text-align"));
        var cell = translated.Descendants(Packaging.OdfPackage.Namespaces.Table + "table-cell").Single();
        Assert.Equal("float", (string?)cell.Attribute(office + "value-type"));
        Assert.NotNull(translated.Root!.Element(office + "body")!.Element(office + "spreadsheet"));
    }
}

public class OpenPgpEncryptionTests
{
    [Fact]
    public void UnDocumentoCifratoConOpenPgpLoDiceSenzaChiedereLaPassword()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write(Packaging.OdfPackage.MimeTypes.Spreadsheet);
            using var manifest = new StreamWriter(archive.CreateEntry("META-INF/manifest.xml").Open());
            manifest.Write("""
                <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" xmlns:loext="urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0" manifest:version="1.3">
                  <manifest:keyinfo><loext:KeyInfo><loext:PGPKeyID>QUJDRA==</loext:PGPKeyID></loext:KeyInfo></manifest:keyinfo>
                  <manifest:file-entry manifest:full-path="encrypted-package" manifest:media-type="application/vnd.oasis.opendocument.spreadsheet">
                    <manifest:encryption-data manifest:checksum-type="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#sha256-1k" manifest:checksum="AA==">
                      <manifest:algorithm manifest:algorithm-name="http://www.w3.org/2001/04/xmlenc#aes256-cbc" manifest:initialisation-vector="AA=="/>
                    </manifest:encryption-data>
                  </manifest:file-entry>
                </manifest:manifest>
                """);
        }

        var encryption = new Packaging.Encryption.OdfPackageEncryption();
        stream.Position = 0;

        Assert.True(encryption.RequiresOpenPgp(stream));
        Assert.Equal(Packaging.Encryption.OdfDecryptionStatus.RequiresOpenPgp, encryption.Decrypt(stream, new Packaging.Encryption.OdfPassword("qualunque")).Status);
        stream.Position = 0;
        Assert.Equal("ODF030", new OdsSpreadsheetReader().Read(stream).Diagnostics[0].Code);
    }
}
