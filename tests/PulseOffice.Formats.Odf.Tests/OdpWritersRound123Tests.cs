using System.IO.Compression;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpWritersRound123Tests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Theory]
    [InlineData("../../content.xml")]
    [InlineData("a\\..\\b.bin")]
    [InlineData("")]
    [InlineData("..")]
    public void A_packaged_object_name_never_escapes_its_folder_and_survives(string name)
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        slide.Add(new ObjectElement
        {
            Id = "ole1",
            Bounds = new DocumentRect(new Twip(600), new Twip(700), new Twip(3000), new Twip(2000)),
            Content = new PackagedObject { Id = "ole1", FileName = name, MediaType = "application/octet-stream", Data = new byte[] { 1, 2, 3 } },
        });

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true))
        {
            var objects = archive.Entries.Where(entry => entry.FullName.StartsWith("Objects/", StringComparison.Ordinal)).ToList();
            var entry = Assert.Single(objects);
            Assert.DoesNotContain("..", entry.FullName.Split('/', '\\'));
            Assert.DoesNotContain('\\', entry.FullName);
            Assert.False(entry.FullName.EndsWith('/'));
            Assert.Equal(2, entry.FullName.Count(c => c == '/'));
        }

        buffer.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(buffer);
        Assert.True(back.Succeeded);
        var package = back.Value!.Slides[0].Elements.OfType<ObjectElement>().Select(o => o.Content).OfType<PackagedObject>().Single();
        Assert.Equal(new byte[] { 1, 2, 3 }, package.Data.ToArray());
    }

    private (Presentation Deck, Slide Slide) Base()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        return (deck, slide);
    }

    private static ObjectElement Picture(string id, byte marker) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(2000), new Twip(1000)),
        Content = new PulseOffice.Drawing.Assets.RasterImageObject { Id = id, Data = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, marker }, MediaType = "image/png" },
    };

    private static byte[] PictureData(SlideElement element) =>
        ((PulseOffice.Drawing.Assets.RasterImageObject)((ObjectElement)element).Content).Data.ToArray();

    [Fact]
    public void Pictures_with_the_same_id_on_two_masters_and_a_slide_keep_their_own_data()
    {
        var (deck, slide) = Base();
        deck.Master = deck.Master with { Objects = [Picture("pic", 1)] };
        var second = _masters.CreateStandard(deck.Theme) with { Id = "second", Name = "Second", Objects = [Picture("pic", 2)] };
        deck.AddMaster(second);
        slide.Add(Picture("pic", 3));

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(buffer).Value!;

        Assert.Equal(1, PictureData(back.Master.Objects[0])[^1]);
        Assert.Equal(2, PictureData(back.Masters.Single(master => master.Id == "second").Objects[0])[^1]);
        Assert.Equal(3, PictureData(back.Slides[0].Elements.OfType<ObjectElement>().Single())[^1]);
    }

    [Fact]
    public void A_layout_page_without_its_own_background_repeats_the_explicit_master_background()
    {
        var (deck, _) = Base();
        deck.Master = deck.Master with
        {
            BackgroundColor = 0xFF123456u,
            Layouts = [.. deck.Master.Layouts.Select(layout => layout.Kind == SlideLayout.Blank ? layout with { Objects = [Picture("l1", 4)] } : layout)],
        };

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("styles.xml")!.Open());
        var styles = System.Xml.Linq.XDocument.Parse(reader.ReadToEnd());
        System.Xml.Linq.XNamespace style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        System.Xml.Linq.XNamespace draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        var page = styles.Descendants(style + "master-page").Single(element => (string?)element.Attribute(style + "name") == "Default-Blank");
        var pageStyle = (string)page.Attribute(draw + "style-name")!;
        var properties = styles.Descendants(style + "style").Single(element => (string?)element.Attribute(style + "name") == pageStyle).Element(style + "drawing-page-properties")!;

        Assert.Equal("#123456", (string?)properties.Attribute(draw + "fill-color"), ignoreCase: true);
    }

    [Fact]
    public void A_custom_show_keeps_a_slide_whose_name_contains_spaces()
    {
        var (deck, slide) = Base();
        slide.Name = "My intro";
        var other = _slides.Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(other);
        deck.AddCustomShow(new CustomShow("Short", [slide.Id, other.Id]));

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(buffer).Value!;

        Assert.Equal([back.Slides[0].Id, back.Slides[1].Id], back.CustomShows.Single().SlideIds);
    }
}
