using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DeckPictureBulletTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAAEUlEQVR4nGP4z8DwHwyBNAMAP+IF+0xnmpAAAAAASUVORK5CYII=");

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Deck(BulletSymbol symbol)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank));
        deck.Slides[0].Add(new TextElement
        {
            Id = "box",
            Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(20), Twip.FromCentimeters(8)),
            Text = new RichText(PlaceholderRole.Free,
            [
                new TextParagraph("uno") { Bullet = true, BulletSymbol = symbol, FontSize = 20 },
                new TextParagraph("due") { Bullet = true, BulletSymbol = symbol, BulletLevel = 1, FontSize = 20 },
            ]),
        });
        return deck;
    }

    private byte[] Odp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private byte[] Pptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private (Presentation Deck, IReadOnlyList<string> Losses) ReadOdp(byte[] bytes)
    {
        var result = new OdpReader(_themes, _masters).Read(new MemoryStream(bytes));
        Assert.True(result.Succeeded);
        return (result.Value!, [.. result.Diagnostics.Select(d => d.Code)]);
    }

    private Presentation ReadPptx(byte[] bytes)
    {
        var result = new PptxReader(_themes, _masters).Read(new MemoryStream(bytes));
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private IReadOnlyList<TextParagraph> Paragraphs(Presentation deck) =>
        deck.Slides[0].Elements.OfType<TextElement>().Single(element => element.Id == "box" || element.Text.Paragraphs.Any(p => p.Text == "uno")).Text.Paragraphs;

    private string Entry(byte[] package, string name)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry(name)!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private IReadOnlyList<string> Entries(byte[] package)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        return [.. archive.Entries.Select(entry => entry.FullName)];
    }

    [Fact]
    public void A_fixed_picture_bullet_crosses_ODP_with_its_size()
    {
        var symbol = new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 16, 8) };
        var odp = Odp(Deck(symbol));

        Assert.Contains("text:list-level-style-image", Entry(odp, "content.xml"));
        Assert.Single(Entries(odp), entry => entry.StartsWith("Pictures/bullet-", StringComparison.Ordinal));
        var (read, losses) = ReadOdp(odp);
        Assert.DoesNotContain("ODP103", losses);
        var picture = Paragraphs(read)[1].BulletSymbol!.Picture!;
        Assert.Equal(Png, picture.Data.ToArray());
        Assert.Equal("image/png", picture.MediaType);
        Assert.Equal(16, picture.Width, 2);
        Assert.Equal(8, picture.Height, 2);
        Assert.False(picture.ScalesWithFont);
    }

    [Fact]
    public void A_fixed_picture_bullet_crosses_PPTX_as_buBlip_with_points()
    {
        var symbol = new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 16, 8) };
        var pptx = Pptx(Deck(symbol));

        var slide = Entry(pptx, "ppt/slides/slide1.xml");
        Assert.Contains("<a:buSzPts val=\"800\"", slide);
        Assert.Contains("<a:buBlip><a:blip r:embed=\"", slide);
        Assert.DoesNotContain("pending", slide);
        var picture = Paragraphs(ReadPptx(pptx))[0].BulletSymbol!.Picture!;
        Assert.Equal(Png, picture.Data.ToArray());
        Assert.Equal(8, picture.Height, 2);
        Assert.Equal(16, picture.Width, 2);
        Assert.False(picture.ScalesWithFont);
    }

    [Fact]
    public void A_font_relative_picture_keeps_its_percentage_in_PPTX_and_a_fixed_size_in_ODP()
    {
        var symbol = new BulletSymbol("•") { RelativeSize = 0.5, Picture = new ListBulletPicture(Png, "image/png", 24, 12) { ScalesWithFont = true } };
        var pptx = Pptx(Deck(symbol));

        Assert.Contains("<a:buSzPct val=\"50000\"", Entry(pptx, "ppt/slides/slide1.xml"));
        var read = Paragraphs(ReadPptx(pptx))[0].BulletSymbol!;
        Assert.True(read.Picture!.ScalesWithFont);
        Assert.Equal(0.5, read.RelativeSize!.Value, 3);
        Assert.Equal(2, read.Picture.Width / read.Picture.Height, 3);

        var (odp, _) = ReadOdp(Odp(ReadPptx(pptx)));
        var fixedPicture = Paragraphs(odp)[0].BulletSymbol!.Picture!;
        Assert.False(fixedPicture.ScalesWithFont);
        Assert.Equal(10, fixedPicture.Height, 2);
        Assert.Equal(20, fixedPicture.Width, 2);
    }

    [Fact]
    public void ODP_and_PPTX_agree_after_crossing_both()
    {
        var symbol = new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 12, 6) };
        var (fromOdp, _) = ReadOdp(Odp(ReadPptx(Pptx(Deck(symbol)))));
        var throughBoth = ReadPptx(Pptx(fromOdp));

        foreach (var paragraph in Paragraphs(throughBoth))
        {
            var picture = paragraph.BulletSymbol!.Picture!;
            Assert.Equal(Png, picture.Data.ToArray());
            Assert.Equal(12, picture.Width, 2);
            Assert.Equal(6, picture.Height, 2);
        }
    }

    [Fact]
    public void A_linked_or_missing_list_picture_is_still_declared()
    {
        var odp = Odp(Deck(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 12, 6) }));
        using var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(odp), ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var entry in source.Entries.Where(entry => !entry.FullName.StartsWith("Pictures/bullet-", StringComparison.Ordinal)))
            {
                var copy = target.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                using var from = entry.Open();
                using var to = copy.Open();
                from.CopyTo(to);
            }

        var (read, losses) = ReadOdp(output.ToArray());
        Assert.Contains("ODP103", losses);
        Assert.Null(Paragraphs(read)[0].BulletSymbol?.Picture);
    }

    [Fact]
    public void A_list_without_a_style_name_takes_the_picture_of_its_presentation_outline_style()
    {
        var odp = Rewrite(Odp(Deck(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 12, 6) })), "content.xml", content =>
        {
            XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
            XNamespace style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
            XNamespace presentation = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0";
            XNamespace office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
            var document = XDocument.Parse(content);
            var lists = document.Descendants(text + "list").ToList();
            var name = (string)lists.First().Attribute(text + "style-name")!;
            var listStyle = document.Descendants(text + "list-style").Single(element => (string?)element.Attribute(style + "name") == name);
            foreach (var list in lists) list.Attribute(text + "style-name")?.Remove();
            var outline = new XElement(listStyle);
            outline.Attribute(style + "name")!.Remove();
            document.Root!.Element(office + "automatic-styles")!.Add(new XElement(style + "style",
                new XAttribute(style + "name", "prOutline"),
                new XAttribute(style + "family", "presentation"),
                new XElement(style + "graphic-properties", outline)));
            lists.First().Ancestors().First(element => element.Name.LocalName == "frame").SetAttributeValue(presentation + "style-name", "prOutline");
            return document.ToString(SaveOptions.DisableFormatting);
        });

        var (read, _) = ReadOdp(odp);

        Assert.All(Paragraphs(read), paragraph => Assert.Equal(Png, paragraph.BulletSymbol!.Picture!.Data.ToArray()));
    }

    [Fact]
    public void Many_bullets_sharing_one_picture_write_one_image_part_per_slide_and_one_ODP_picture()
    {
        var deck = Deck(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 12, 6) });
        var box = deck.Slides[0].Elements.OfType<TextElement>().Single();
        var symbol = box.Text.Paragraphs[0].BulletSymbol!;
        deck.Slides[0].Replace(box with
        {
            Text = new RichText(PlaceholderRole.Free, [.. Enumerable.Range(0, 40).Select(index => new TextParagraph($"voce {index}") { Bullet = true, BulletLevel = index % 3, BulletSymbol = symbol with { Picture = symbol.Picture! with { Data = Png.ToArray() } } })]),
        });

        var pptx = Pptx(deck);
        Assert.Single(Entries(pptx), entry => entry.StartsWith("ppt/media/", StringComparison.Ordinal));
        var odp = Odp(deck);
        Assert.Single(Entries(odp), entry => entry.StartsWith("Pictures/", StringComparison.Ordinal));
    }

    [Fact]
    public void A_slide_picture_with_the_same_bytes_as_a_bullet_does_not_collide_in_ODP()
    {
        var deck = Deck(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 12, 6) });
        deck.Slides[0].Add(new ObjectElement
        {
            Id = "img",
            Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(10), Twip.FromCentimeters(2), Twip.FromCentimeters(1)),
            Content = new RasterImageObject { Id = "img", Data = Png, MediaType = "image/png" },
        });

        var odp = Odp(deck);
        var entries = Entries(odp);
        Assert.Equal(entries.Count, entries.Distinct(StringComparer.Ordinal).Count());
        var manifest = XDocument.Parse(Entry(odp, "META-INF/manifest.xml")).Descendants().Select(element => (string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "full-path")).OfType<string>().ToList();
        Assert.Equal(manifest.Count, manifest.Distinct(StringComparer.Ordinal).Count());
        var (read, _) = ReadOdp(odp);
        Assert.Equal(Png, Paragraphs(read)[0].BulletSymbol!.Picture!.Data.ToArray());
        Assert.Contains(read.Slides[0].Elements, element => element is ObjectElement { Content: RasterImageObject });
    }

    [Fact]
    public void A_picture_without_data_is_written_as_a_character_bullet()
    {
        var deck = Deck(new BulletSymbol("•") { Picture = new ListBulletPicture(Array.Empty<byte>(), "image/png", 12, 6) });

        var pptx = Pptx(deck);
        Assert.DoesNotContain("buBlip", Entry(pptx, "ppt/slides/slide1.xml"));
        Assert.DoesNotContain("rIdPulsePending", Entry(pptx, "ppt/slides/slide1.xml"));
        var odp = Odp(deck);
        Assert.DoesNotContain("list-level-style-image", Entry(odp, "content.xml"));
        Assert.DoesNotContain(Entries(odp), entry => entry.StartsWith("Pictures/bullet-", StringComparison.Ordinal));
    }

    [Fact]
    public void Huge_bullet_sizes_in_PPTX_are_bounded_to_the_schema()
    {
        var pptx = Rewrite(Pptx(Deck(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 16, 8) })), "ppt/slides/slide1.xml",
            xml => System.Text.RegularExpressions.Regex.Replace(xml, "<a:buSzPts val=\"800\"", "<a:buSzPts val=\"2147483647\""));
        var picture = Paragraphs(ReadPptx(pptx))[0].BulletSymbol!.Picture!;
        Assert.Equal(4000, picture.Height, 2);

        var character = Rewrite(Pptx(Deck(new BulletSymbol("–") { RelativeSize = 2 })), "ppt/slides/slide1.xml",
            xml => System.Text.RegularExpressions.Regex.Replace(xml, "<a:buSzPct val=\"200000\"", "<a:buSzPct val=\"2147483647\""));
        Assert.Equal(4, Paragraphs(ReadPptx(character))[0].BulletSymbol!.RelativeSize!.Value, 3);

        var (odp, _) = ReadOdp(Odp(ReadPptx(pptx)));
        Assert.Equal(4000, Paragraphs(odp)[0].BulletSymbol!.Picture!.Height, 2);
    }

    [Fact]
    public void Huge_or_negative_picture_sizes_in_ODP_are_bounded()
    {
        var odp = Rewrite(Odp(Deck(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 16, 8) })), "content.xml",
            xml => System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(xml, "fo:width=\"[^\"]+\"", "fo:width=\"99999in\""), "fo:height=\"[^\"]+\"", "fo:height=\"-3pt\""));

        var (read, _) = ReadOdp(odp);

        var picture = Paragraphs(read)[0].BulletSymbol!.Picture!;
        Assert.Equal(4000, picture.Width, 2);
        Assert.Equal(9, picture.Height, 2);
    }

    [Fact]
    public void A_buBlip_part_over_the_cap_falls_back_to_a_character_bullet()
    {
        var pptx = ReplaceMedia(Pptx(Deck(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 16, 8) })), new byte[8 * 1024 * 1024 + 1]);

        var paragraphs = Paragraphs(ReadPptx(pptx));

        Assert.True(paragraphs[0].Bullet);
        Assert.Null(paragraphs[0].BulletSymbol?.Picture);
    }

    [Fact]
    public void A_buBlip_with_undecodable_data_is_kept_and_crosses_ODP()
    {
        var garbage = Encoding.ASCII.GetBytes("not a picture at all");
        var pptx = ReplaceMedia(Pptx(Deck(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 16, 8) })), garbage);

        var picture = Paragraphs(ReadPptx(pptx))[0].BulletSymbol!.Picture!;
        Assert.Equal(garbage, picture.Data.ToArray());
        Assert.Equal(16 / 2.0, picture.Height, 2);

        var (odp, _) = ReadOdp(Odp(ReadPptx(pptx)));
        Assert.Equal(garbage, Paragraphs(odp)[0].BulletSymbol!.Picture!.Data.ToArray());
    }

    [Fact]
    public void A_buBlip_naming_a_missing_relationship_falls_back_to_a_character_bullet()
    {
        var pptx = Rewrite(Pptx(Deck(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 16, 8) })), "ppt/slides/slide1.xml",
            xml => System.Text.RegularExpressions.Regex.Replace(xml, "<a:blip r:embed=\"[^\"]+\"", "<a:blip r:embed=\"rId999\""));

        var paragraphs = Paragraphs(ReadPptx(pptx));

        Assert.True(paragraphs[0].Bullet);
        Assert.Null(paragraphs[0].BulletSymbol?.Picture);
    }

    private byte[] ReplaceMedia(byte[] package, byte[] data)
    {
        using var stream = new MemoryStream();
        stream.Write(package);
        using (var document = PresentationDocument.Open(stream, true))
            foreach (var image in document.PresentationPart!.SlideParts.SelectMany(slide => slide.ImageParts))
                using (var source = new MemoryStream(data)) image.FeedData(source);
        return stream.ToArray();
    }

    private byte[] Rewrite(byte[] package, string name, Func<string, string> change)
    {
        using var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                using var from = entry.Open();
                using var to = copy.Open();
                if (entry.FullName == name)
                {
                    using var reader = new StreamReader(from, Encoding.UTF8);
                    var bytes = Encoding.UTF8.GetBytes(change(reader.ReadToEnd()));
                    to.Write(bytes);
                }
                else from.CopyTo(to);
            }
        return output.ToArray();
    }
}
