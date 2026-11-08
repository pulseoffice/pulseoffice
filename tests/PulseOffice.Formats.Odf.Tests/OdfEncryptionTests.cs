using System.IO.Compression;
using PulseOffice.Formats.Packaging.Encryption;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdfEncryptionTests
{
    private readonly OdfPackageEncryption _encryption = new();

    private static MemoryStream Fixture(string name) =>
        new(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Calc", name)));

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Theory]
    [InlineData("password.ods")]
    [InlineData("passwordOld.ods")]
    public void IFogliCifratiDaCalcSiAprono(string file)
    {
        using var stream = Fixture(file);
        Assert.True(_encryption.IsEncrypted(stream));

        var result = _encryption.Decrypt(stream, new Packaging.Encryption.OdfPassword("test"));

        Assert.Equal(OdfDecryptionStatus.Decrypted, result.Status);
        var read = new OdsSpreadsheetReader().Read(result.Package!);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics));
        Assert.Equal("Sheet1", read.Value!.Sheets[0].Name);
    }

    [Fact]
    public void LaPasswordSbagliataSiRiconosce()
    {
        using var stream = Fixture("password.ods");

        Assert.Equal(OdfDecryptionStatus.WrongPassword, _encryption.Decrypt(stream, new Packaging.Encryption.OdfPassword("sbagliata")).Status);
    }

    [Fact]
    public void SenzaPasswordIlLettoreLoDice()
    {
        using var stream = Fixture("password.ods");

        var result = new OdsSpreadsheetReader().Read(stream);

        Assert.False(result.Succeeded);
        Assert.Equal("ODF004", result.Diagnostics[0].Code);
    }

    [Fact]
    public void IlPacchettoCifratoConArgon2EAesGcmSiApre()
    {
        using var stream = Fixture("encrypted_scriptsig_lo242.odt");

        var result = _encryption.Decrypt(stream, new Packaging.Encryption.OdfPassword("password"));

        Assert.Equal(OdfDecryptionStatus.Decrypted, result.Status);
        using var archive = new ZipArchive(result.Package!);
        Assert.NotNull(archive.GetEntry("content.xml"));
    }

    [Fact]
    public void UnFoglioCifratoDaNoiSiRiapreSoloConLaPassword()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Conti").SetValue(A("A1"), CellValue.Number(42));
        using var plain = new MemoryStream();
        new OdsSpreadsheetWriter().Write(workbook, plain);
        plain.Position = 0;

        using var encrypted = _encryption.Encrypt(plain, new Packaging.Encryption.PasswordProtection("Segreto"));

        Assert.True(_encryption.IsEncrypted(encrypted));
        Assert.Equal(OdfDecryptionStatus.WrongPassword, _encryption.Decrypt(encrypted, new Packaging.Encryption.OdfPassword("segreto")).Status);
        encrypted.Position = 0;
        var opened = _encryption.Decrypt(encrypted, new Packaging.Encryption.OdfPassword("Segreto"));
        var reread = new OdsSpreadsheetReader().Read(opened.Package!).Value!;
        Assert.Equal(CellValue.Number(42), reread.Sheets[0].GetValue(A("A1")));
    }

    [Fact]
    public void UnPacchettoCheChiedeTroppeDerivazioniDiChiaveSiRifiuta()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Conti").SetValue(A("A1"), CellValue.Number(42));
        using var plain = new MemoryStream();
        new OdsSpreadsheetWriter().Write(workbook, plain);
        plain.Position = 0;
        using var encrypted = _encryption.Encrypt(plain, new Packaging.Encryption.PasswordProtection("Segreto"));
        var stingy = new OdfPackageEncryption(new Packaging.Legacy.LegacyOdfUpgrader(), new Packaging.Encryption.OpenPgp.BouncyCastleOpenPgp(), budget: new OdfDerivationBudget(150_000, 0));

        Assert.Equal(OdfDecryptionStatus.Unsupported, stingy.Decrypt(encrypted, new Packaging.Encryption.OdfPassword("Segreto")).Status);
    }

    [Fact]
    public void LoStileBooleanoConTestoSopravviveAlGiro()
    {
        using var stream = Fixture("tdf149484.ods");
        var workbook = new OdsSpreadsheetReader().Read(stream).Value!;
        var code = workbook.NumberFormats.CodeOf(workbook.Styles.Get(workbook.Sheets[0].StyleIdAt(A("A3"))).NumberFormatId);

        using var output = new MemoryStream();
        new OdsSpreadsheetWriter().Write(workbook, output);
        output.Position = 0;
        var reread = new OdsSpreadsheetReader().Read(output).Value!;

        Assert.Equal("-BOOLEAN-", code);
        Assert.Equal(code, reread.NumberFormats.CodeOf(reread.Styles.Get(reread.Sheets[0].StyleIdAt(A("A3"))).NumberFormatId));
    }

    [Fact]
    public void UnManifestMalformatoSiDichiaraComeDatiNonValidi()
    {
        var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("mimetype").Open())) writer.Write("application/vnd.oasis.opendocument.spreadsheet");
            using (var writer = new StreamWriter(archive.CreateEntry("META-INF/manifest.xml").Open())) writer.Write("<manifest:manifest");
        }

        buffer.Position = 0;
        Assert.Throws<InvalidDataException>(() => _encryption.Decrypt(buffer, new OdfPassword("x")));
    }
}
