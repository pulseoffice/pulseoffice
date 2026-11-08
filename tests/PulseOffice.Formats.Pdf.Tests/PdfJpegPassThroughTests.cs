using System.Text;
using PulseOffice.Formats.Imaging;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class PdfJpegPassThroughTests
{
    private static byte[] Jpeg(int components, bool adobe, byte[]? profile = null, int precision = 8)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };
        void Segment(byte marker, byte[] payload)
        {
            bytes.AddRange([0xFF, marker, (byte)((payload.Length + 2) >> 8), (byte)(payload.Length + 2)]);
            bytes.AddRange(payload);
        }
        if (adobe) Segment(0xEE, [.. "Adobe"u8.ToArray(), 0, 100, 0, 0, 0, 0, 2]);
        if (profile is not null)
        {
            var half = profile.Length / 2;
            Segment(0xE2, [.. "ICC_PROFILE\0"u8.ToArray(), 2, 2, .. profile[half..]]);
            Segment(0xE2, [.. "ICC_PROFILE\0"u8.ToArray(), 1, 2, .. profile[..half]]);
        }
        var frame = new List<byte> { (byte)precision, 0, 20, 0, 30, (byte)components };
        for (var i = 0; i < components; i++) frame.AddRange([(byte)(i + 1), 0x11, 0]);
        Segment(0xC0, [.. frame]);
        bytes.AddRange([0xFF, 0xDA, 0, 2, 0xFF, 0xD9]);
        return [.. bytes];
    }

    private static byte[] Profile(string space)
    {
        var profile = new byte[200];
        Encoding.ASCII.GetBytes(space).CopyTo(profile, 16);
        profile[0] = 0xAB;
        profile[199] = 0xCD;
        return profile;
    }

    private static (string Text, int Id) Write(byte[] jpeg, PdfImageOptions? options = null, byte[]? alpha = null)
    {
        using var stream = new MemoryStream();
        var file = new PdfFile(stream);
        var image = new PageImage(2, 2, new byte[12]) { Alpha = alpha, Source = new PageImageSource(jpeg, "image/jpeg") };
        var id = new PdfImageWriter().Write(file, new PdfImagePlacement(image, 30, 20), options ?? new PdfImageOptions(), false, new List<Diagnostic>());
        return (Encoding.Latin1.GetString(stream.ToArray()), id);
    }

    [Fact]
    public void InspectorReadsFrameAdobeMarkerAndProfileInOrder()
    {
        var profile = Profile("CMYK");
        var facts = new JpegInspector().Inspect(Jpeg(4, adobe: true, profile));

        Assert.NotNull(facts);
        Assert.Equal((30, 20, 4, true), (facts.Width, facts.Height, facts.Components, facts.Adobe));
        Assert.Equal(profile, facts.IccProfile);
    }

    [Fact]
    public void InspectorRefusesTruncatedOrUnsupportedFiles()
    {
        var inspector = new JpegInspector();
        var whole = Jpeg(3, adobe: false);

        Assert.Null(inspector.Inspect(whole.AsSpan(0, 9)));
        Assert.Null(inspector.Inspect(Jpeg(3, adobe: false, precision: 12)));
        Assert.Null(inspector.Inspect(Jpeg(2, adobe: false)));
        Assert.Null(inspector.Inspect("not a jpeg"u8));
    }

    [Fact]
    public void AdobeCmykJpegIsEmbeddedAsCmykWithInvertedDecode()
    {
        var jpeg = Jpeg(4, adobe: true);
        var (text, id) = Write(jpeg);

        Assert.Contains($"{id} 0 obj", text, StringComparison.Ordinal);
        Assert.Contains("/Filter /DCTDecode", text, StringComparison.Ordinal);
        Assert.Contains("/ColorSpace /DeviceCMYK", text, StringComparison.Ordinal);
        Assert.Contains("/Decode [1 0 1 0 1 0 1 0]", text, StringComparison.Ordinal);
        Assert.Contains("/Width 30 /Height 20", text, StringComparison.Ordinal);
        Assert.Contains(Encoding.Latin1.GetString(jpeg), text, StringComparison.Ordinal);
    }

    [Fact]
    public void IccProfileBecomesAnIccBasedColourSpace()
    {
        var (text, _) = Write(Jpeg(3, adobe: false, Profile("RGB ")));

        Assert.Contains("/N 3 /Alternate /DeviceRGB", text, StringComparison.Ordinal);
        Assert.Matches(@"/ColorSpace \[/ICCBased \d+ 0 R\]", text);
        Assert.DoesNotContain("/Decode", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AProfileOfTheWrongSpaceIsIgnored()
    {
        var (text, _) = Write(Jpeg(4, adobe: false, Profile("RGB ")));

        Assert.Contains("/ColorSpace /DeviceCMYK", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ICCBased", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchivesRefuseDeviceCmykWithoutAProfileAndUsePixels()
    {
        var (text, _) = Write(Jpeg(4, adobe: true), new PdfImageOptions { DeviceIndependentOnly = true });

        Assert.DoesNotContain("DCTDecode", text, StringComparison.Ordinal);
        Assert.Contains("/ColorSpace /DeviceRGB", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentOrOversizedImagesKeepThePixels()
    {
        var (transparent, _) = Write(Jpeg(3, adobe: false), alpha: [255, 0, 255, 0]);
        var (limited, _) = Write(Jpeg(3, adobe: false), new PdfImageOptions { MaxResolution = 10 });

        Assert.DoesNotContain("DCTDecode", transparent, StringComparison.Ordinal);
        Assert.DoesNotContain("DCTDecode", limited, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSameImageOnEveryPageIsWrittenOnce()
    {
        var document = new RepeatedLogo(40);
        using var stream = new MemoryStream();

        var result = new PdfExporter(new PulseOffice.Formats.Pdf.Fonts.StandardPdfFonts(new PdfFontMetrics())).Write(document, stream, new PdfExportOptions("Logo", Compress: false));

        Assert.True(result.Succeeded);
        var text = Encoding.Latin1.GetString(stream.ToArray());
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "/Subtype /Image"));
    }

    private sealed class RepeatedLogo(int pages) : IPrintableDocument
    {
        public PulseOffice.Foundation.Pages.PageSetup Setup { get; } = new() { Paper = new PulseOffice.Foundation.Pages.PaperCatalog().Default };

        public int PageCount => pages;

        public void PaintPage(int index, IPageCanvas canvas)
        {
            var pixels = new byte[16 * 16 * 3];
            Array.Fill(pixels, (byte)90);
            canvas.DrawImage(new PageImage(16, 16, pixels), new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.FromPoints(40), PulseOffice.Foundation.Units.Twip.FromPoints(40),
                PulseOffice.Foundation.Units.Twip.FromPoints(60), PulseOffice.Foundation.Units.Twip.FromPoints(60)));
        }
    }
}
