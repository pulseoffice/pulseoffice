using System.IO.Compression;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round234OdpImageClipTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Fact]
    public void The_clip_of_a_cropped_slide_image_is_measured_on_the_original_image()
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        slide.Add(new ObjectElement
        {
            Id = "img1",
            Bounds = new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromCentimeters(10), Twip.FromCentimeters(3)),
            Content = new RasterImageObject { Id = "img1", Data = PngHeader(100, 40), MediaType = "image/png", Crop = (0.1, 0, 0.2, 0.25) },
        });

        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        var bytes = stream.ToArray();
        string content;
        using (var zip = new ZipArchive(new MemoryStream(bytes)))
        using (var reader = new StreamReader(zip.GetEntry("content.xml")!.Open()))
            content = reader.ReadToEnd();

        Assert.Contains("rect(0pt, 15pt, 7.5pt, 7.5pt)", content, StringComparison.Ordinal);
        var read = new OdpReader(_themes, _masters).Read(new MemoryStream(bytes)).Value!;
        var crop = read.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<RasterImageObject>().Single().Crop;
        Assert.Equal(0.1, crop.Left, 3);
        Assert.Equal(0.2, crop.Right, 3);
        Assert.Equal(0.25, crop.Bottom, 3);
    }

    private static byte[] PngHeader(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }
}
