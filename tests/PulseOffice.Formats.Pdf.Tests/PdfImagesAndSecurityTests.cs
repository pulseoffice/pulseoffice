using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Pdf;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Formats.Rasterizing;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed partial class PdfImagesAndSecurityTests
{
    private sealed class Picture(PageImage image, double sizePoints) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => 1;

        public void PaintPage(int index, IPageCanvas canvas)
        {
            canvas.DrawText("Riservato", new DocumentRect(Twip.FromPoints(50), Twip.FromPoints(40), Twip.FromPoints(300), Twip.FromPoints(20)),
                PrintFont.Default, PrintColor.Black);
            canvas.DrawImage(image, new DocumentRect(Twip.FromPoints(50), Twip.FromPoints(80), Twip.FromPoints(sizePoints), Twip.FromPoints(sizePoints)));
        }
    }

    private readonly IPdfExporter _exporter = new PdfExporter(
        new StandardPdfFonts(new PdfFontMetrics()), new PdfArchiveParts(new SrgbProfile()), new PdfNavigationWriter(),
        new PdfImageWriter(new SkiaJpegEncoder(), new BoxImageResampler(), new JpegInspector()), new Aes256EncryptionFactory());

    private static PageImage Gradient(int size)
    {
        var rgb = new byte[size * size * 3];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var pixel = (y * size + x) * 3;
            rgb[pixel] = (byte)(x * 255 / size);
            rgb[pixel + 1] = (byte)(y * 255 / size);
            rgb[pixel + 2] = 128;
        }
        return new PageImage(size, size, rgb);
    }

    private (string Text, byte[] Bytes, IReadOnlyList<PulseOffice.Foundation.Diagnostics.Diagnostic> Diagnostics) Export(PdfExportOptions options, int size = 400, double points = 72)
    {
        using var stream = new MemoryStream();
        var result = _exporter.Write(new Picture(Gradient(size), points), stream, options);
        Assert.True(result.Succeeded, result.Diagnostics.FirstOrDefault()?.Message);
        var bytes = stream.ToArray();
        return (Encoding.Latin1.GetString(bytes), bytes, result.Diagnostics);
    }

    [Fact]
    public void Senza_perdita_l_immagine_resta_intera_e_compressa_in_Flate()
    {
        var (text, _, _) = Export(new PdfExportOptions("Prova"));

        Assert.DoesNotContain("/DCTDecode", text, StringComparison.Ordinal);
        Assert.Contains("/Width 400 /Height 400", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Con_JPEG_l_immagine_e_un_flusso_DCT_che_comincia_con_il_marcatore_SOI()
    {
        var (text, bytes, _) = Export(new PdfExportOptions("Prova") { Images = new PdfImageOptions { Compression = PdfImageCompression.Jpeg, JpegQuality = 80 } });

        Assert.Contains("/Filter /DCTDecode", text, StringComparison.Ordinal);
        var start = text.IndexOf("stream\n", text.IndexOf("/DCTDecode", StringComparison.Ordinal), StringComparison.Ordinal) + 7;
        Assert.Equal(0xFF, bytes[start]);
        Assert.Equal(0xD8, bytes[start + 1]);
    }

    [Fact]
    public void La_qualita_JPEG_piu_bassa_produce_un_file_piu_piccolo()
    {
        var high = Export(new PdfExportOptions("Prova") { Images = new PdfImageOptions { Compression = PdfImageCompression.Jpeg, JpegQuality = 95 } }).Bytes.Length;
        var low = Export(new PdfExportOptions("Prova") { Images = new PdfImageOptions { Compression = PdfImageCompression.Jpeg, JpegQuality = 10 } }).Bytes.Length;

        Assert.True(low < high, $"{low} >= {high}");
    }

    [Fact]
    public void La_risoluzione_massima_ricampiona_l_immagine_alla_misura_stampata()
    {
        var (text, _, _) = Export(new PdfExportOptions("Prova") { Images = new PdfImageOptions { MaxResolution = 150 } }, size: 400, points: 72);

        Assert.Contains("/Width 150 /Height 150", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/Width 400", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_immagine_gia_sotto_la_risoluzione_massima_non_cambia()
    {
        var (text, _, _) = Export(new PdfExportOptions("Prova") { Images = new PdfImageOptions { MaxResolution = 300 } }, size: 100, points: 72);

        Assert.Contains("/Width 100 /Height 100", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Con_la_password_di_apertura_il_file_e_cifrato_e_si_decifra_solo_con_quella()
    {
        var (text, bytes, _) = Export(new PdfExportOptions("Titolo segreto", Compress: true) { Security = new PdfSecurityOptions { OpenPassword = "apri" } });

        Assert.Contains("/Filter /Standard /V 5 /R 6", text, StringComparison.Ordinal);
        Assert.Matches(EncryptReference(), text);
        Assert.DoesNotContain("Titolo segreto", text, StringComparison.Ordinal);
        Assert.Contains("/ExtensionLevel 8", text, StringComparison.Ordinal);

        var reader = new Decryptor(text);
        Assert.Null(reader.UserKey("sbagliata"));
        var key = Assert.IsType<byte[]>(reader.UserKey("apri"));

        var content = reader.Streams(bytes).Select(stream => Inflate(Decrypt(key, stream))).Select(Encoding.Latin1.GetString).ToList();
        Assert.Contains(content, stream => stream.Contains(" Do", StringComparison.Ordinal) && stream.Contains("TJ", StringComparison.Ordinal));

        var title = TitleString().Match(text).Groups[1].Value;
        var plain = Decrypt(key, Convert.FromHexString(title));
        Assert.Equal("Titolo segreto", Encoding.Latin1.GetString(plain));
    }

    [Fact]
    public void I_permessi_negati_finiscono_nella_P_e_nella_Perms_firmata_con_la_chiave()
    {
        var (text, _, _) = Export(new PdfExportOptions("Prova")
        {
            Security = new PdfSecurityOptions { PermissionsPassword = "capo", Printing = PdfPrintPermission.LowResolution, AllowCopy = false, AllowChanges = false },
        });

        var p = int.Parse(Regex.Match(text, @"/P (-?\d+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotEqual(0, p & 4);
        Assert.Equal(0, p & 2048);
        Assert.Equal(0, p & 16);
        Assert.Equal(0, p & 8);
        Assert.NotEqual(0, p & 32);

        var reader = new Decryptor(text);
        var userKey = Assert.IsType<byte[]>(reader.UserKey(""));
        var ownerKey = Assert.IsType<byte[]>(reader.OwnerKey("capo"));
        Assert.Equal(userKey, ownerKey);
        Assert.Null(reader.OwnerKey("altro"));

        using var aes = Aes.Create();
        aes.Key = userKey;
        var perms = aes.DecryptEcb(reader.Value("Perms"), PaddingMode.None);
        Assert.Equal(p, BitConverter.ToInt32(perms, 0));
        Assert.Equal("adb", Encoding.ASCII.GetString(perms, 9, 3));
    }

    [Fact]
    public void Senza_password_e_senza_limiti_il_file_non_e_cifrato()
    {
        var (text, _, _) = Export(new PdfExportOptions("Prova") { Security = new PdfSecurityOptions() });

        Assert.DoesNotContain("/Encrypt", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PDF_A_e_password_insieme_tengono_l_archivio_e_lo_dichiarano()
    {
        var (text, _, diagnostics) = Export(new PdfExportOptions("Prova") { Archive = true, Security = new PdfSecurityOptions { OpenPassword = "x" } });

        Assert.DoesNotContain("/Encrypt", text, StringComparison.Ordinal);
        Assert.Contains(diagnostics, item => item.Code == "PDF014");
    }

    [Fact]
    public void Le_stringhe_letterali_annidate_e_con_escape_si_leggono_intere()
    {
        var tokens = new PdfStringTokens();
        var seen = new List<string>();
        var result = tokens.Replace("<< /T (a\\(b\\)c (d) e\\101) /H <4142> >>", bytes =>
        {
            seen.Add(Encoding.Latin1.GetString(bytes));
            return "X";
        });

        Assert.Equal(["a(b)c (d) eA", "AB"], seen);
        Assert.Equal("<< /T X /H X >>", result);
    }

    private static byte[] Decrypt(byte[] key, byte[] payload)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.DecryptCbc(payload[16..], payload[..16], PaddingMode.PKCS7);
    }

    private static byte[] Inflate(byte[] payload)
    {
        try
        {
            using var input = new ZLibStream(new MemoryStream(payload), CompressionMode.Decompress);
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return payload;
        }
    }

    [GeneratedRegex(@"/Encrypt \d+ 0 R")]
    private static partial Regex EncryptReference();

    [GeneratedRegex(@"/Title <([0-9A-F]+)>")]
    private static partial Regex TitleString();

    private sealed class Decryptor(string text)
    {
        public byte[] Value(string name) =>
            Convert.FromHexString(Regex.Match(text, "/" + name + @" <([0-9A-F]+)>").Groups[1].Value);

        public byte[]? UserKey(string password)
        {
            var u = Value("U");
            var bytes = Encoding.UTF8.GetBytes(password);
            if (!Hash(bytes, u[32..40], []).SequenceEqual(u[..32])) return null;
            return Unwrap(Hash(bytes, u[40..48], []), Value("UE"));
        }

        public byte[]? OwnerKey(string password)
        {
            var o = Value("O");
            var u = Value("U");
            var bytes = Encoding.UTF8.GetBytes(password);
            if (!Hash(bytes, o[32..40], u).SequenceEqual(o[..32])) return null;
            return Unwrap(Hash(bytes, o[40..48], u), Value("OE"));
        }

        public IEnumerable<byte[]> Streams(byte[] bytes)
        {
            foreach (Match match in Regex.Matches(text, @"<< /Length (\d+)[^\n]*>>\nstream\n"))
            {
                var length = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                var start = match.Index + match.Length;
                yield return bytes[start..(start + length)];
            }
        }

        private static byte[] Unwrap(byte[] key, byte[] wrapped)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            return aes.DecryptCbc(wrapped, new byte[16], PaddingMode.None);
        }

        private static byte[] Hash(byte[] password, byte[] salt, byte[] userKey)
        {
            var k = SHA256.HashData([.. password, .. salt, .. userKey]);
            var e = Array.Empty<byte>();
            for (var i = 0; i < 64 || e[^1] > i - 32; i++)
            {
                var unit = password.Concat(k).Concat(userKey).ToArray();
                var k1 = Enumerable.Repeat(unit, 64).SelectMany(part => part).ToArray();
                using var aes = Aes.Create();
                aes.Key = k[..16];
                e = aes.EncryptCbc(k1, k[16..32], PaddingMode.None);
                var mod = new System.Numerics.BigInteger(e[..16], isUnsigned: true, isBigEndian: true) % 3;
                k = (int)mod switch { 0 => SHA256.HashData(e), 1 => SHA384.HashData(e), _ => SHA512.HashData(e) };
            }
            return k[..32];
        }
    }
}
