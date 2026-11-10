using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PresentationTwelfthAuditTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        return deck;
    }

    private byte[] Pptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private Presentation ReadPptx(byte[] bytes)
    {
        var result = new PptxReader(_themes, _masters).Read(new MemoryStream(bytes));
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return result.Value!;
    }

    private Presentation ReadOdp(byte[] bytes)
    {
        var result = new OdpReader(_themes, _masters).Read(new MemoryStream(bytes));
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return result.Value!;
    }

    private byte[] Rewrite(byte[] package, Func<string, string?> rename, Func<string, string, string> content)
    {
        using var source = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: Encoding.UTF8))
        {
            foreach (var entry in source.Entries)
            {
                using var from = entry.Open();
                using var buffer = new MemoryStream();
                from.CopyTo(buffer);
                var name = rename(entry.FullName) ?? entry.FullName;
                var bytes = buffer.ToArray();
                if (entry.FullName.EndsWith(".xml") || entry.FullName.EndsWith(".rels"))
                    bytes = Encoding.UTF8.GetBytes(content(entry.FullName, Encoding.UTF8.GetString(bytes)));
                using var to = target.CreateEntry(name).Open();
                to.Write(bytes);
            }
        }

        return output.ToArray();
    }

    [Fact]
    public void MediaWithoutContentTypeAndWithAccentedNameOpens()
    {
        var deck = Deck();
        var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        deck.Slides[0].Add(new MediaElement
        {
            Id = "m1",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(6), Twip.FromCentimeters(4)),
            Kind = MediaKind.Audio,
            Data = bytes,
            MimeType = "audio/mpeg",
        });

        var package = Pptx(deck);
        string? mediaName = null;
        using (var archive = new ZipArchive(new MemoryStream(package)))
            mediaName = archive.Entries.Select(e => e.FullName).FirstOrDefault(n => n.Contains("media", StringComparison.OrdinalIgnoreCase) && !n.EndsWith(".png") && !n.EndsWith(".rels")) ?? throw new InvalidOperationException(string.Join(",", archive.Entries.Select(e => e.FullName)));
        var original = Path.GetFileName(mediaName);

        var broken = Rewrite(package,
            name => name == mediaName ? mediaName[..(mediaName.LastIndexOf('/') + 1)] + "Cortázar.mp3" : null,
            (name, text) => name == "[Content_Types].xml"
                ? Regex.Replace(text, "<(Default|Override)[^>]*(mp3|mpeg)[^>]*/>", string.Empty)
                : text.Replace(original, "Cortázar.mp3"));

        var media = ReadPptx(broken).Slides[0].Elements.OfType<MediaElement>().SingleOrDefault();
        Assert.True(media is not null, mediaName);
        Assert.Equal(bytes, media!.Data);
    }

    [Fact]
    public void RevealSurvivesPptx()
    {
        var deck = Deck();
        deck.Slides[0].Transition = new SlideTransition { Kind = TransitionKind.Reveal };
        Assert.Equal(TransitionKind.Reveal, ReadPptx(Pptx(deck)).Slides[0].Transition.Kind);
    }

    [Fact]
    public void MorphAndCutAreDeclared()
    {
        var deck = Deck();
        deck.Slides[0].Transition = new SlideTransition { Kind = TransitionKind.Morph };
        Assert.DoesNotContain(new PptxWriter().CheckFidelity(deck).Losses, d => d.Code == "PPTX024");
        Assert.Contains(new OdpWriter().CheckFidelity(deck).Losses, d => d.Code == "ODP021");
    }

    [Fact]
    public void MediaCommandsAreNotReadAsMotionPaths()
    {
        var deck = Deck();
        deck.Slides[0].Add(new MediaElement
        {
            Id = "m1",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(6), Twip.FromCentimeters(4)),
            Kind = MediaKind.Audio, Data = [1, 2, 3], MimeType = "audio/mpeg",
        });
        deck.Slides[0].AddAnimation(new AnimationEffect { Id = "a1", TargetId = "m1", Category = AnimationCategory.Entrance, Preset = "fade" });
        var package = Rewrite(Pptx(deck), _ => null, (name, text) => name.StartsWith("ppt/slides/slide") ? text.Replace("presetClass=\"entr\"", "presetClass=\"mediacall\"") : text);

        Assert.All(ReadPptx(package).Slides[0].Animations, effect => Assert.Equal(AnimationCategory.Media, effect.Category));
    }

    private byte[] Odp(string pageContent)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(), buffer).Succeeded);
        return Rewrite(buffer.ToArray(), _ => null, (name, text) => name == "content.xml"
            ? Regex.Replace(text, "(<draw:page [^>]*>)", "$1" + pageContent)
            : text);
    }

    [Fact]
    public void OdfPrimitiveShapesAreRead()
    {
        var bytes = Odp("""
            <draw:rect draw:id="r1" svg:x="1cm" svg:y="1cm" svg:width="4cm" svg:height="2cm"><text:p>Box</text:p></draw:rect>
            <draw:ellipse draw:id="e1" svg:x="6cm" svg:y="1cm" svg:width="3cm" svg:height="3cm"/>
            <draw:line draw:id="l1" svg:x1="10cm" svg:y1="5cm" svg:x2="2cm" svg:y2="1cm"/>
            <draw:polygon draw:id="p1" svg:x="1cm" svg:y="5cm" svg:width="4cm" svg:height="2cm" svg:viewBox="0 0 4000 2000" draw:points="0,2000 2000,0 4000,2000"/>
            <draw:path draw:id="q1" svg:x="1cm" svg:y="8cm" svg:width="4cm" svg:height="2cm" svg:viewBox="1000 1000 4000 2000" svg:d="M1000 1000 L5000 3000"/>
            """);
        var objects = ReadOdp(bytes).Slides[0].Elements.OfType<ObjectElement>().ToList();

        var rect = objects.Single(o => o.Content is PulseOffice.Drawing.Shapes.ShapeObject { Shape: PulseOffice.Drawing.Shapes.ShapeKind.Rectangle });
        Assert.Equal("Box", ((PulseOffice.Drawing.Shapes.ShapeObject)rect.Content).Text);
        Assert.Contains(objects, o => o.Content is PulseOffice.Drawing.Shapes.ShapeObject { Shape: PulseOffice.Drawing.Shapes.ShapeKind.Ellipse });
        var line = objects.Single(o => o.Content is PulseOffice.Drawing.Shapes.ShapeObject { Shape: PulseOffice.Drawing.Shapes.ShapeKind.Line });
        Assert.True(((PulseOffice.Drawing.Shapes.ShapeObject)line.Content).FlipHorizontal);
        Assert.Equal((double)Twip.FromCentimeters(8).Value, (double)line.Bounds.Width.Value, 0);

        var paths = objects.Select(o => o.Content).OfType<PulseOffice.Drawing.Freeform.FreeformObject>().ToList();
        Assert.Equal(2, paths.Count);
        foreach (var point in paths.SelectMany(p => p.Path.Figures).SelectMany(f => f.Nodes).Select(n => n.Point))
        {
            Assert.InRange(point.X, -0.001, 1.001);
            Assert.InRange(point.Y, -0.001, 1.001);
        }
    }

    [Fact]
    public void AnimationsOnParagraphsAndGroupsTargetTheirShapes()
    {
        var bytes = Odp("""
            <draw:frame xml:id="f1" draw:id="f1" svg:x="1cm" svg:y="1cm" svg:width="8cm" svg:height="3cm"><draw:text-box><text:p xml:id="p1">Uno</text:p><text:p xml:id="p2">Due</text:p></draw:text-box></draw:frame>
            <anim:par presentation:node-type="timing-root"><anim:seq presentation:node-type="main-sequence"><anim:par smil:begin="next"><anim:par smil:begin="0s"><anim:par smil:begin="0s" smil:fill="hold" presentation:node-type="on-click" presentation:preset-class="entrance" presentation:preset-id="ooo-entrance-appear"><anim:set smil:begin="0s" smil:dur="0.001s" smil:fill="hold" smil:targetElement="p2" smil:attributeName="visibility" smil:to="visible"/></anim:par></anim:par></anim:par></anim:seq></anim:par>
            """);
        var slide = ReadOdp(bytes).Slides[0];
        var effect = Assert.Single(slide.Animations);
        Assert.Contains(slide.Elements, e => e.Id == effect.TargetId);
        Assert.Single(ReadPptx(Pptx(ReadOdp(bytes))).Slides[0].Animations);
    }

    [Fact]
    public void UnAnimazioneSuUnGruppoSenzaIdentificativiNeiMembriTocca_tutti_i_membri()
    {
        var bytes = Odp("""
            <draw:g xml:id="g1" draw:id="g1"><draw:rect svg:x="1cm" svg:y="1cm" svg:width="4cm" svg:height="2cm"/><draw:ellipse svg:x="6cm" svg:y="1cm" svg:width="3cm" svg:height="3cm"/></draw:g>
            <anim:par presentation:node-type="timing-root"><anim:seq presentation:node-type="main-sequence"><anim:par smil:begin="next"><anim:par smil:begin="0s"><anim:par smil:begin="0s" smil:fill="hold" presentation:node-type="on-click" presentation:preset-class="entrance" presentation:preset-id="ooo-entrance-appear"><anim:set smil:begin="0s" smil:dur="0.001s" smil:fill="hold" smil:targetElement="g1" smil:attributeName="visibility" smil:to="visible"/></anim:par></anim:par></anim:par></anim:seq></anim:par>
            """);
        var slide = ReadOdp(bytes).Slides[0];

        Assert.Equal(2, slide.Animations.Count);
        Assert.All(slide.Animations, effect => Assert.Contains(slide.Elements, e => e.Id == effect.TargetId));
        Assert.Equal(2, ReadPptx(Pptx(ReadOdp(bytes))).Slides[0].Animations.Count);
    }

    [Fact]
    public void UnImmagineCollegataConPercorsoRelativoResta_relativa_alla_cartella_del_documento()
    {
        var bytes = Odp("""
            <draw:frame draw:id="i1" svg:x="1cm" svg:y="1cm" svg:width="4cm" svg:height="3cm"><draw:image xlink:href="../foto.jpg" xlink:type="simple" xlink:show="embed" xlink:actuate="onLoad"/></draw:frame>
            """);
        var deck = ReadOdp(bytes);
        var image = deck.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<PulseOffice.Drawing.Assets.RasterImageObject>().Single();
        Assert.Equal("foto.jpg", image.LinkPath);

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, odp).Succeeded);
        using var zip = new ZipArchive(new MemoryStream(odp.ToArray()));
        using var content = new StreamReader(zip.GetEntry("content.xml")!.Open());
        Assert.Contains("xlink:href=\"../foto.jpg\"", content.ReadToEnd(), StringComparison.Ordinal);

        using var pptx = new ZipArchive(new MemoryStream(Pptx(deck)));
        using var rels = new StreamReader(pptx.GetEntry("ppt/slides/_rels/slide1.xml.rels")!.Open());
        Assert.Contains("Target=\"foto.jpg\"", rels.ReadToEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void I_simboli_dei_punti_elenco_fanno_il_giro_in_ODP_e_PPTX()
    {
        var deck = Deck();
        var symbol = new PulseOffice.Text.Rich.BulletSymbol("✗") { Font = "DejaVu Sans", Color = 0xFFC9211E, RelativeSize = 0.45 };
        var box = new TextElement
        {
            Id = "box",
            Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(10), Twip.FromCentimeters(4)),
            Text = new PulseOffice.Text.Rich.RichText(PulseOffice.Text.Rich.PlaceholderRole.Free,
                [new PulseOffice.Text.Rich.TextParagraph("uno") { Bullet = true, BulletSymbol = symbol }, new PulseOffice.Text.Rich.TextParagraph("due") { Bullet = true }]),
        };
        deck.Slides[0].Add(box);

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, odp).Succeeded);
        foreach (var read in new[] { ReadOdp(odp.ToArray()), ReadPptx(Pptx(deck)) })
        {
            var paragraphs = read.Slides[0].Elements.OfType<TextElement>().Single(element => element.Text.Paragraphs.Any(p => p.Text == "uno")).Text.Paragraphs;
            Assert.Equal(symbol, paragraphs.Single(p => p.Text == "uno").BulletSymbol);
            Assert.True(paragraphs.Single(p => p.Text == "due").Bullet);
        }
    }
}
