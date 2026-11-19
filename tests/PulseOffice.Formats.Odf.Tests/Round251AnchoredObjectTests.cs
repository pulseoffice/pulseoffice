using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round251AnchoredObjectTests
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    private static TextDocument Image(ObjectPlacement placement)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock("p1", "text", DefaultStyleNames.Standard));
        document.Objects.Add(new AnchoredObject("img", new ParagraphAnchor("p1"), new EmbeddedContent(new RasterImageObject { Id = "r", Data = new byte[] { 1 }, MediaType = "image/png" }),
            new DocumentSize(Twip.FromPoints(20), Twip.FromPoints(20))) { Placement = placement });
        return document;
    }

    private static AnchoredObject Inherited(string commonStyles, string ownProperties)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(Image(new ObjectPlacement(Twip.Zero, Twip.Zero)), stream).Succeeded);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var content = Load(zip, "content.xml");
            var frameStyle = (string)content.Descendants(Draw + "frame").First().Attribute(Draw + "style-name")!;
            var own = content.Descendants(Style + "style").First(style => (string?)style.Attribute(Style + "name") == frameStyle);
            own.SetAttributeValue(Style + "parent-style-name", "Graphics");
            own.Elements(Style + "graphic-properties").Remove();
            own.Add(XElement.Parse($"<style:graphic-properties xmlns:style=\"{Style.NamespaceName}\" {ownProperties}/>"));
            Save(zip, "content.xml", content);
            var styles = Load(zip, "styles.xml");
            var office = styles.Root!.Element(Office + "styles")!;
            office.Elements().Where(style => (string?)style.Attribute(Style + "family") == "graphic").Remove();
            foreach (var style in XElement.Parse($"<x xmlns:style=\"{Style.NamespaceName}\">{commonStyles}</x>").Elements()) office.Add(style);
            Save(zip, "styles.xml", styles);
        }
        stream.Position = 0;
        return Assert.Single(new OdtReader().Read(stream).Value!.Objects.All);
    }

    private static XDocument Load(ZipArchive zip, string name)
    {
        using var input = zip.GetEntry(name)!.Open();
        return XDocument.Load(input);
    }

    private static void Save(ZipArchive zip, string name, XDocument document)
    {
        zip.GetEntry(name)!.Delete();
        using var output = zip.CreateEntry(name).Open();
        document.Save(output, SaveOptions.DisableFormatting);
    }

    [Fact]
    public void Frame_placement_and_wrap_come_from_the_parent_graphic_style()
    {
        var styles = "<style:style style:name=\"Graphics\" style:family=\"graphic\"><style:graphic-properties style:wrap=\"none\" style:vertical-pos=\"top\" style:vertical-rel=\"paragraph\" style:horizontal-pos=\"center\" style:horizontal-rel=\"paragraph\"/></style:style>";
        var automatic = "style:mirror=\"none\"";

        var item = Inherited(styles, automatic);

        Assert.Equal(WrapMode.None, item.Wrap);
        Assert.Equal(ObjectAlignment.Center, item.Placement.HorizontalAlign);
        Assert.Equal(ObjectAlignment.Start, item.Placement.VerticalAlign);
    }

    [Fact]
    public void The_automatic_style_overrides_the_parent_and_the_default_style_fills_the_rest()
    {
        var styles = "<style:default-style style:family=\"graphic\"><style:graphic-properties style:run-through=\"foreground\" style:horizontal-rel=\"page\"/></style:default-style>"
            + "<style:style style:name=\"Graphics\" style:family=\"graphic\"><style:graphic-properties style:wrap=\"none\"/></style:style>";
        var automatic = "style:wrap=\"right\"";

        var item = Inherited(styles, automatic);

        Assert.Equal(WrapMode.Right, item.Wrap);
        Assert.Equal(HorizontalRelation.Page, item.Placement.Horizontal);
    }

    [Fact]
    public void Right_and_bottom_margin_relations_round_trip()
    {
        var placement = new ObjectPlacement(Twip.FromPoints(3), Twip.FromPoints(5)) { Horizontal = HorizontalRelation.EndMargin, Vertical = VerticalRelation.BottomMargin };
        var document = Image(placement);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;

        var read = Assert.Single(new OdtReader().Read(stream).Value!.Objects.All);

        Assert.Equal(placement, read.Placement);
    }
}
