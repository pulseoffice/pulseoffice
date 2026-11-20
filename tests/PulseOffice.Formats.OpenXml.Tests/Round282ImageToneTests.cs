using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round282ImageToneTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    public static TheoryData<double, double, double, ImageColorMode> Tones => new()
    {
        { 20, -30, 40, ImageColorMode.Standard },
        { 0, 0, 25, ImageColorMode.BlackWhite },
        { 0, 0, 0, ImageColorMode.Watermark },
    };

    private RasterImageObject Raster(ImageTone tone) => new() { Id = "foto", Data = Png, MediaType = "image/png", Tone = tone };

    private Presentation Deck(ImageTone tone)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement { Id = "foto", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(3000), new Twip(2000)), Content = Raster(tone) });
        deck.AddExisting(slide);
        return deck;
    }

    private Workbook Book(ImageTone tone)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "foto",
            Content = Raster(tone),
            Anchor = new CellAddress(2, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)),
        });
        return workbook;
    }

    private ImageTone Tone(Presentation deck) =>
        deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Select(element => element.Content).OfType<RasterImageObject>().Single().Tone;

    private ImageTone Tone(Workbook workbook) => Assert.IsType<RasterImageObject>(Assert.Single(workbook.Sheets[0].Objects).Content).Tone;

    [Theory]
    [MemberData(nameof(Tones))]
    public void Image_tone_and_colour_mode_cross_pptx_and_odp(double brightness, double contrast, double transparency, ImageColorMode mode)
    {
        var tone = new ImageTone(brightness, contrast, transparency, mode);

        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(tone), pptx).Succeeded);
        pptx.Position = 0;
        Assert.Equal(tone, Tone(new PptxReader(_themes, _masters).Read(pptx).Value!));

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(tone), odp).Succeeded);
        odp.Position = 0;
        Assert.Equal(tone, Tone(new OdpReader(_themes, _masters).Read(odp).Value!));
    }

    [Theory]
    [MemberData(nameof(Tones))]
    public void Image_tone_and_colour_mode_cross_xlsx_and_ods(double brightness, double contrast, double transparency, ImageColorMode mode)
    {
        var tone = new ImageTone(brightness, contrast, transparency, mode);

        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(Book(tone), xlsx);
        xlsx.Position = 0;
        Assert.Equal(tone, Tone(new XlsxReader().Read(xlsx).Value!));

        using var ods = new MemoryStream();
        new OdsWriter().Write(Book(tone), ods);
        ods.Position = 0;
        Assert.Equal(tone, Tone(new OdsReader().Read(ods).Value!));
        Assert.DoesNotContain("ODF022", new OdsSpreadsheetWriter().CheckFidelity(Book(tone)).Losses.Select(loss => loss.Code));
    }

    [Fact]
    public void A_greyscale_ledger_image_crosses_ods()
    {
        var workbook = Book(ImageTone.Neutral);
        workbook.Sheets[0].Objects[0] = workbook.Sheets[0].Objects[0] with
        {
            Content = Raster(ImageTone.Neutral) with { Filter = ImageFilter.Grayscale },
        };

        using var ods = new MemoryStream();
        new OdsWriter().Write(workbook, ods);
        ods.Position = 0;
        var raster = Assert.IsType<RasterImageObject>(Assert.Single(new OdsReader().Read(ods).Value!.Sheets[0].Objects).Content);

        Assert.Equal(ImageFilter.Grayscale, raster.Filter);
    }
}
