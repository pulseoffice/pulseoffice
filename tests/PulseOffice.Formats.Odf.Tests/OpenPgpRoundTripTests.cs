using System.IO.Compression;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using PulseOffice.Formats.Packaging.Encryption;
using PulseOffice.Formats.Packaging.Encryption.OpenPgp;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public enum TestKeyKind { Rsa, NistP256, NistP384, NistP521, Curve25519 }

public sealed class TestKeys
{
    private readonly SecureRandom _random = new();

    public (byte[] PublicKey, byte[] SecretKey) Generate(TestKeyKind kind, string passphrase, SymmetricKeyAlgorithmTag protection = SymmetricKeyAlgorithmTag.Aes256)
    {
        var now = DateTime.UtcNow;
        var generator = new RsaKeyPairGenerator();
        generator.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(0x10001), _random, 2048, 12));
        var master = new PgpKeyPair(PublicKeyAlgorithmTag.RsaSign, generator.GenerateKeyPair(), now);

        var ring = new PgpKeyRingGenerator(PgpSignature.PositiveCertification, master, "Prova <prova@example.org>",
            protection, passphrase.ToCharArray(), true, null, null, _random);
        ring.AddSubKey(new PgpKeyPair(kind == TestKeyKind.Rsa ? PublicKeyAlgorithmTag.RsaGeneral : PublicKeyAlgorithmTag.ECDH, Subkey(kind), now));

        return (Armor(ring.GeneratePublicKeyRing().GetEncoded()), ring.GenerateSecretKeyRing().GetEncoded());
    }

    private AsymmetricCipherKeyPair Subkey(TestKeyKind kind)
    {
        switch (kind)
        {
            case TestKeyKind.Rsa:
                var rsa = new RsaKeyPairGenerator();
                rsa.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(0x10001), _random, 2048, 12));
                return rsa.GenerateKeyPair();
            case TestKeyKind.Curve25519:
                var x25519 = new X25519KeyPairGenerator();
                x25519.Init(new X25519KeyGenerationParameters(_random));
                return x25519.GenerateKeyPair();
            default:
                var curve = kind switch { TestKeyKind.NistP256 => "P-256", TestKeyKind.NistP384 => "P-384", _ => "P-521" };
                var ec = new ECKeyPairGenerator("ECDH");
                ec.Init(new ECKeyGenerationParameters(ECNamedCurveTable.GetOid(curve), _random));
                return ec.GenerateKeyPair();
        }
    }

    private byte[] Armor(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var armored = new ArmoredOutputStream(output)) armored.Write(bytes);
        return output.ToArray();
    }
}

public class OpenPgpRoundTripTests
{
    private readonly BouncyCastleOpenPgp _openPgp = new();
    private readonly OdfPackageEncryption _encryption = new();
    private readonly TestKeys _keys = new();

    private MemoryStream Spreadsheet()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Conti").SetValue(new CellAddress(0, 0), CellValue.Text("riservato"));
        var output = new MemoryStream();
        new OdsSpreadsheetWriter().Write(workbook, output);
        output.Position = 0;
        return output;
    }

    private OpenPgpProtection For(params byte[][] publicKeys) =>
        new OpenPgpProtectionFactory(_openPgp).ForRecipients([.. publicKeys.SelectMany(_openPgp.ReadRecipients)]);

    [Theory]
    [InlineData(TestKeyKind.Rsa, SymmetricKeyAlgorithmTag.Aes256)]
    [InlineData(TestKeyKind.Rsa, SymmetricKeyAlgorithmTag.Cast5)]
    [InlineData(TestKeyKind.NistP256, SymmetricKeyAlgorithmTag.Aes256)]
    [InlineData(TestKeyKind.NistP384, SymmetricKeyAlgorithmTag.Aes128)]
    [InlineData(TestKeyKind.NistP521, SymmetricKeyAlgorithmTag.Aes256)]
    [InlineData(TestKeyKind.Curve25519, SymmetricKeyAlgorithmTag.Aes256)]
    public void UnFoglioCifratoPerUnDestinatarioSiRiapreConLaSuaChiave(TestKeyKind kind, SymmetricKeyAlgorithmTag protection)
    {
        var (publicKey, secretKey) = _keys.Generate(kind, "segreta", protection);

        using var encrypted = _encryption.Encrypt(Spreadsheet(), For(publicKey));
        Assert.Equal(-1, encrypted.ToArray().AsSpan().IndexOf("riservato"u8));
        Assert.True(_encryption.RequiresOpenPgp(encrypted));

        var result = _encryption.Decrypt(encrypted, new OpenPgpSecretKey(secretKey, "segreta"));

        Assert.Equal(OdfDecryptionStatus.Decrypted, result.Status);
        Assert.IsType<OpenPgpProtection>(result.Protection);
        var workbook = new OdsSpreadsheetReader().Read(result.Package!).Value!;
        Assert.Equal(CellValue.Text("riservato"), workbook.Sheets[0].GetValue(new CellAddress(0, 0)));
    }

    [Fact]
    public void PassphraseSbagliataChiaveEstraneaEFileNonValidoSiDistinguono()
    {
        var (publicKey, secretKey) = _keys.Generate(TestKeyKind.Rsa, "segreta");
        var (_, otherSecret) = _keys.Generate(TestKeyKind.Curve25519, "altra");
        using var encrypted = _encryption.Encrypt(Spreadsheet(), For(publicKey));

        Assert.Equal(OdfDecryptionStatus.WrongPassword, _encryption.Decrypt(encrypted, new OpenPgpSecretKey(secretKey, "sbagliata")).Status);
        Assert.Equal(OdfDecryptionStatus.NoMatchingKey, _encryption.Decrypt(encrypted, new OpenPgpSecretKey(otherSecret, "altra")).Status);
        Assert.Equal(OdfDecryptionStatus.InvalidKeyFile, _encryption.Decrypt(encrypted, new OpenPgpSecretKey("non una chiave"u8.ToArray(), "x")).Status);
        Assert.Equal(OdfDecryptionStatus.RequiresOpenPgp, _encryption.Decrypt(encrypted, new OdfPassword("segreta")).Status);
    }

    [Fact]
    public void OgniDestinatarioApreLoStessoDocumentoERisalvandoNonServeLaChiavePubblica()
    {
        var (firstPublic, firstSecret) = _keys.Generate(TestKeyKind.Rsa, "uno");
        var (secondPublic, secondSecret) = _keys.Generate(TestKeyKind.NistP256, "due");
        using var encrypted = _encryption.Encrypt(Spreadsheet(), For(firstPublic, secondPublic));

        var first = _encryption.Decrypt(encrypted, new OpenPgpSecretKey(firstSecret, "uno"));
        Assert.Equal(OdfDecryptionStatus.Decrypted, first.Status);
        Assert.Equal(2, ((OpenPgpProtection)first.Protection!).Slots.Count);

        using var resaved = _encryption.Encrypt(first.Package!, first.Protection!);
        Assert.Equal(OdfDecryptionStatus.Decrypted, _encryption.Decrypt(resaved, new OpenPgpSecretKey(secondSecret, "due")).Status);
    }

    [Theory]
    [InlineData("encryptedGPG.odt")]
    [InlineData("encryptedGPG_odf13.odt")]
    public void IDocumentiCifratiDaUnAltraSuiteConGpgSiAprono(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OpenPgp");
        using var stream = new MemoryStream(File.ReadAllBytes(Path.Combine(folder, name)));
        var secret = File.ReadAllBytes(Path.Combine(folder, "secring.gpg"));

        Assert.True(_encryption.RequiresOpenPgp(stream));
        var result = _encryption.Decrypt(stream, new OpenPgpSecretKey(secret, string.Empty));

        Assert.Equal(OdfDecryptionStatus.Decrypted, result.Status);
        using var archive = new ZipArchive(result.Package!, ZipArchiveMode.Read);
        using var content = new StreamReader(archive.GetEntry("content.xml")!.Open());
        Assert.Contains("office:document-content", content.ReadToEnd(), StringComparison.Ordinal);
    }
}
