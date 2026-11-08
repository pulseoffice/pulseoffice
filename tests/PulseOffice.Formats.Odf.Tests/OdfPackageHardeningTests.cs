using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Formats.Packaging.Encryption;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdfPackageHardeningTests
{
    private static MemoryStream Package(params (string Path, string Text)[] entries)
    {
        var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, text) in entries)
                using (var writer = new StreamWriter(archive.CreateEntry(path).Open())) writer.Write(text);
        buffer.Position = 0;
        return buffer;
    }

    [Fact]
    public void UnaParteCheSiEspandeOltreIlLimiteSiRifiuta()
    {
        using var stream = Package(("content.xml", new string('a', 10_000)));
        using var package = OdfPackage.OpenRead(stream, 1_000);

        Assert.Throws<InvalidDataException>(() => package.ReadBytes("content.xml"));
        Assert.Throws<InvalidDataException>(() => package.ReadXml("content.xml"));
    }

    [Fact]
    public void UnDocumentoLegacyAnnidatoSenzaFineNonFaTraboccareLaPila()
    {
        XNamespace legacy = "http://openoffice.org/2000/text";
        var nested = new XElement(legacy + "span");
        for (var depth = 0; depth < 100_000; depth++) nested = new XElement(legacy + "span", nested);
        var document = new XDocument(new XElement(Packaging.Legacy.LegacyVocabulary.LegacyOffice + "document-content", nested));

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { new Packaging.Legacy.LegacyXmlTranslator().Translate(document); }
            catch (Exception ex) { failure = ex; }
        }, 256 * 1024);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    [Theory]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><style>rect { fill: \\75 rl(http://evil.example/a.png) }</style><rect/></svg>")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><style>@\\69mport 'http://evil.example/a.css';</style><rect/></svg>")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><rect style='fill: u\\72l(http://evil.example/a.png)'/></svg>")]
    public void UnSvgConEscapeCssNonRichiamaRisorseEsterne(string svg)
    {
        var cleaned = new SvgSanitizer().Sanitize(Encoding.UTF8.GetBytes(svg))!.ToString();

        Assert.DoesNotContain("\\", cleaned, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaParteConEntitaDichiarateNonVieneEspansa()
    {
        const string laughs = "<?xml version=\"1.0\"?><!DOCTYPE r [<!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\"><!ENTITY c \"&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;\">]><r>&c;</r>";
        using var stream = Package(("content.xml", laughs));
        using var package = OdfPackage.OpenRead(stream);

        Assert.Throws<XmlException>(() => package.ReadXml("content.xml"));
    }

    [Fact]
    public void UnaParteConDoctypeSenzaEntitaSiLeggeIgnorandoLaDtd()
    {
        using var stream = Package(("content.xml", "<?xml version=\"1.0\"?><!DOCTYPE r SYSTEM \"http://example.invalid/r.dtd\"><r>testo</r>"));
        using var package = OdfPackage.OpenRead(stream);

        Assert.Equal("testo", package.ReadXml("content.xml")!.Root!.Value);
    }

    [Fact]
    public void UnaMemoriaArgon2SproporzionataNelManifestNonVieneAllocata()
    {
        using var source = new MemoryStream(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Calc", "encrypted_scriptsig_lo242.odt")));
        using var tampered = Rewrite(source, manifest =>
        {
            foreach (var attribute in manifest.Descendants().Attributes().Where(attribute => attribute.Name.LocalName == "argon2-memory"))
                attribute.Value = "4194304";
        });

        var result = new OdfPackageEncryption().Decrypt(tampered, new OdfPassword("password"));

        Assert.NotEqual(OdfDecryptionStatus.Decrypted, result.Status);
    }

    private static MemoryStream Rewrite(Stream source, Action<XDocument> change)
    {
        var output = new MemoryStream();
        using (var input = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                using var read = entry.Open();
                using var buffer = new MemoryStream();
                read.CopyTo(buffer);
                var bytes = buffer.ToArray();
                if (entry.FullName == "META-INF/manifest.xml")
                {
                    var manifest = XDocument.Parse(Encoding.UTF8.GetString(bytes));
                    change(manifest);
                    bytes = Encoding.UTF8.GetBytes(manifest.ToString(SaveOptions.DisableFormatting));
                }

                using var written = target.CreateEntry(entry.FullName).Open();
                written.Write(bytes);
            }
        }

        output.Position = 0;
        return output;
    }

    [Fact]
    public void UnPoligonoDellaMappaConCoordinateMalformateNonImpedisceLaLettura()
    {
        var frame = new XElement(OdfPackage.Namespaces.Draw + "frame",
            new XElement(OdfPackage.Namespaces.Draw + "image-map",
                new XElement(OdfPackage.Namespaces.Draw + "area-polygon",
                    new XAttribute(OdfPackage.Namespaces.Draw + "points", "0,0 1000,x 1000,1000 0,1000 abc"))));

        var map = new OdfImageMapCodec().Read(frame, new PulseOffice.Foundation.Units.DocumentSize(
            PulseOffice.Foundation.Units.Twip.FromCentimeters(1), PulseOffice.Foundation.Units.Twip.FromCentimeters(1)));

        var area = Assert.Single(map.Areas);
        Assert.Equal(3, area.Points.Count);
    }

    [Fact]
    public void LaSanificazioneSvgTogliLeAnimazioniCheRiscrivonoUnCollegamento()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\"><a xlink:href=\"#x\"><set attributeName=\"xlink:href\" to=\"javascript:alert(1)\"/><animate attributeName=\"opacity\" from=\"0\" to=\"1\"/></a></svg>";

        var document = new SvgSanitizer().Sanitize(Encoding.UTF8.GetBytes(svg))!;

        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "set");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "animate");
    }
}
