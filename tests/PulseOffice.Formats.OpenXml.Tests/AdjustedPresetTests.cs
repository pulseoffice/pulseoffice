using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class AdjustedPresetTests
{
    private readonly XNamespace _draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly PresetGeometry _rounded = new("roundRect") { Adjustments = [new GeometryAdjustment("adj", 30000)], ReferenceWidth = 5292, ReferenceHeight = 3528 };

    private ShapeObject Rounded => new() { Id = "forma", Shape = ShapeKind.RoundedRectangle, Computed = _rounded };

    private Presentation Deck(ShapeObject shape)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement { Id = "forma", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(3000), new Twip(2000)), Content = shape });
        deck.AddExisting(slide);
        return deck;
    }

    private ShapeObject Single(Presentation deck) =>
        deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single();

    private void AssertRounded(ShapeObject shape)
    {
        Assert.Equal(ShapeKind.RoundedRectangle, shape.Shape);
        var preset = Assert.IsType<PresetGeometry>(shape.Computed);
        Assert.Equal("roundRect", preset.Name);
        Assert.Equal(30000, preset.Adjustments.Single(adjustment => adjustment.Name == "adj").Value, 0);
    }

    [Fact]
    public void Il_raggio_di_un_rettangolo_arrotondato_si_legge_da_pptx()
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(new ShapeObject { Id = "forma", Shape = ShapeKind.RoundedRectangle }), buffer).Succeeded);
        buffer.Position = 0;
        using (var document = PresentationDocument.Open(buffer, true))
        {
            var geometry = document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<D.PresetGeometry>()).Single(item => item.Preset?.InnerText == "roundRect");
            geometry.AdjustValueList!.RemoveAllChildren();
            geometry.AdjustValueList.Append(new D.ShapeGuide { Name = "adj", Formula = "val 30000" });
        }
        buffer.Position = 0;
        AssertRounded(Single(new PptxReader(_themes, _masters).Read(buffer).Value!));
    }

    [Fact]
    public void Il_raggio_resta_predefinito_senza_geometria_calcolata_quando_coincide()
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(new ShapeObject { Id = "forma", Shape = ShapeKind.RoundedRectangle }), buffer).Succeeded);
        buffer.Position = 0;
        var shape = Single(new PptxReader(_themes, _masters).Read(buffer).Value!);
        Assert.Equal(ShapeKind.RoundedRectangle, shape.Shape);
        Assert.Null(shape.Computed);
    }

    [Fact]
    public void Il_raggio_attraversa_pptx_e_odp_con_le_maniglie()
    {
        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(Rounded), pptx).Succeeded);
        pptx.Position = 0;
        var fromPptx = new PptxReader(_themes, _masters).Read(pptx).Value!;
        AssertRounded(Single(fromPptx));

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(fromPptx, odp).Succeeded);
        odp.Position = 0;
        XDocument content;
        using (var archive = new ZipArchive(odp, ZipArchiveMode.Read, leaveOpen: true))
        using (var entry = archive.GetEntry("content.xml")!.Open())
            content = XDocument.Load(entry);
        var geometry = content.Descendants(_draw + "enhanced-geometry").Single();
        Assert.Equal("ooxml-roundRect", (string?)geometry.Attribute(_draw + "type"));
        Assert.Equal("30000", (string?)geometry.Attribute(_draw + "modifiers"));
        var handle = Assert.Single(geometry.Elements(_draw + "handle"));
        Assert.Equal("50000", (string?)handle.Attribute(_draw + "handle-range-x-maximum"));
        odp.Position = 0;
        AssertRounded(Single(new OdpReader(_themes, _masters).Read(odp).Value!));
    }

    [Fact]
    public void Il_raggio_attraversa_xlsx_e_ods()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "forma",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
            Content = Rounded,
        });
        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(workbook, xlsx);
        xlsx.Position = 0;
        var fromXlsx = new XlsxReader().Read(xlsx).Value!;
        AssertRounded(Assert.IsType<ShapeObject>(Assert.Single(fromXlsx.Sheets[0].Objects).Content));

        using var ods = new MemoryStream();
        new OdsWriter().Write(fromXlsx, ods);
        ods.Position = 0;
        AssertRounded(Assert.IsType<ShapeObject>(Assert.Single(new OdsReader().Read(ods).Value!.Sheets[0].Objects).Content));
    }

    [Fact]
    public void Il_raggio_attraversa_docx_e_odt()
    {
        var document = new TextDocument();
        var paragraph = new PulseOffice.Documents.Blocks.ParagraphBlock("p1", "Testo");
        document.Body.Replace([paragraph]);
        document.Objects.Add(new AnchoredObject("obj1", new ParagraphAnchor(paragraph.Id), new EmbeddedContent(Rounded), new DocumentSize(new Twip(2880), new Twip(1440))));

        using var docx = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, docx).Succeeded);
        docx.Position = 0;
        var fromDocx = _composer.Reader(DocxFlavour.Document).Read(docx).Value!;
        AssertRounded(Shape(fromDocx));

        using var odt = new MemoryStream();
        Assert.True(new OdtWriter().Write(fromDocx, odt).Succeeded);
        odt.Position = 0;
        AssertRounded(Shape(new OdtReader().Read(odt).Value!));
    }

    [Fact]
    public void Le_ombreggiature_dei_tracciati_attraversano_la_geometria_libera_ooxml()
    {
        var formula = new FormulaGeometry("M 0 0 L 21600 0 21600 21600 0 21600 Z N H S M 0 0 L 10800 0 10800 10800 Z N F M 0 0 L 21600 21600 N")
        {
            ReferenceWidth = 5292,
            ReferenceHeight = 3528,
        };
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle, Computed = formula }), buffer).Succeeded);
        buffer.Position = 0;
        using var document = PresentationDocument.Open(buffer, false);
        var paths = document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<D.Path>()).ToList();
        Assert.Equal(3, paths.Count);
        Assert.Equal(D.PathFillModeValues.Darken, paths[1].Fill!.Value);
        Assert.False(paths[1].Stroke!.Value);
        Assert.Equal(D.PathFillModeValues.None, paths[2].Fill!.Value);
    }

    private void Native(MemoryStream package, string type, string modifiers, bool mirrored = false)
    {
        using var archive = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true);
        var entry = archive.GetEntry("content.xml")!;
        XDocument content;
        using (var stream = entry.Open()) content = XDocument.Load(stream);
        var geometry = content.Descendants(_draw + "enhanced-geometry").Single();
        geometry.Elements().Remove();
        geometry.SetAttributeValue(_draw + "type", type);
        geometry.SetAttributeValue(_draw + "modifiers", modifiers);
        geometry.SetAttributeValue(_draw + "enhanced-path", "M 0 0 L 21600 0 21600 21600 0 21600 Z N");
        if (mirrored) geometry.SetAttributeValue(_draw + "mirror-horizontal", "true");
        entry.Delete();
        using var rewritten = archive.CreateEntry("content.xml").Open();
        content.Save(rewritten);
    }

    [Theory]
    [InlineData("12000 5400", false)]
    [InlineData("16200 5400", true)]
    public void Una_freccia_nativa_con_modificatori_propri_resta_fedele_e_in_PPTX_la_perdita_si_dichiara(string modifiers, bool preset)
    {
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle }), odp).Succeeded);
        Native(odp, "right-arrow", modifiers);
        Equation(odp);
        odp.Position = 0;
        var deck = new OdpReader(_themes, _masters).Read(odp).Value!;
        var shape = Single(deck);
        Assert.Equal(ShapeKind.ArrowRight, shape.Shape);
        Assert.Equal(preset, shape.Computed is null);

        using var pptx = new MemoryStream();
        var writer = new PptxWriter();
        Assert.True(writer.Write(deck, pptx).Succeeded);
        pptx.Position = 0;
        using var document = PresentationDocument.Open(pptx, false);
        var slide = document.PresentationPart!.SlideParts.Single().Slide!;
        Assert.Equal(!preset, slide.Descendants<D.CustomGeometry>().Any());
        Assert.Equal(!preset, writer.CheckFidelity(deck).Losses.Any(diagnostic => diagnostic.Code == "PPTX190"));
    }

    private void Equation(MemoryStream package)
    {
        using var archive = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true);
        var entry = archive.GetEntry("content.xml")!;
        XDocument content;
        using (var stream = entry.Open()) content = XDocument.Load(stream);
        var geometry = content.Descendants(_draw + "enhanced-geometry").Single();
        geometry.SetAttributeValue(_draw + "enhanced-path", "M 0 ?f1 L ?f0 ?f1 ?f0 0 21600 10800 ?f0 21600 ?f0 ?f2 0 ?f2 Z N");
        geometry.Add(new XElement(_draw + "equation", new XAttribute(_draw + "name", "f0"), new XAttribute(_draw + "formula", "$0")),
            new XElement(_draw + "equation", new XAttribute(_draw + "name", "f1"), new XAttribute(_draw + "formula", "$1")),
            new XElement(_draw + "equation", new XAttribute(_draw + "name", "f2"), new XAttribute(_draw + "formula", "21600-$1")));
        entry.Delete();
        using var rewritten = archive.CreateEntry("content.xml").Open();
        content.Save(rewritten);
    }

    private PresetGeometry Preset(ShapeObject shape, string name)
    {
        var preset = Assert.IsType<PresetGeometry>(shape.Computed);
        Assert.Equal(name, preset.Name);
        return preset;
    }

    [Fact]
    public void I_modificatori_di_un_rettangolo_arrotondato_nativo_di_riferimento_diventano_la_regolazione_e_avLst()
    {
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(new ShapeObject { Id = "forma", Shape = ShapeKind.RoundedRectangle }), odp).Succeeded);
        Native(odp, "round-rectangle", "5400");
        odp.Position = 0;
        var deck = new OdpReader(_themes, _masters).Read(odp).Value!;
        var shape = Single(deck);
        Assert.Equal(ShapeKind.RoundedRectangle, shape.Shape);
        Assert.Equal(25000, Preset(shape, "roundRect").Adjustments.Single().Value, 0);

        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, pptx).Succeeded);
        pptx.Position = 0;
        using var document = PresentationDocument.Open(pptx, false);
        var geometry = document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<D.PresetGeometry>()).Single();
        Assert.Equal("roundRect", geometry.Preset!.InnerText);
        Assert.Equal("val 25000", geometry.AdjustValueList!.Elements<D.ShapeGuide>().Single().Formula!.Value);
    }

    [Fact]
    public void Un_rettangolo_arrotondato_nativo_con_i_modificatori_predefiniti_resta_senza_geometria()
    {
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(new ShapeObject { Id = "forma", Shape = ShapeKind.RoundedRectangle }), odp).Succeeded);
        Native(odp, "round-rectangle", "3600");
        odp.Position = 0;
        var shape = Single(new OdpReader(_themes, _masters).Read(odp).Value!);
        Assert.Equal(ShapeKind.RoundedRectangle, shape.Shape);
        Assert.Null(shape.Computed);
    }

    [Fact]
    public void Un_cubo_nativo_ribaltato_diventa_il_preset_cube_con_la_sua_regolazione()
    {
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle }), odp).Succeeded);
        Native(odp, "cube", "2700", mirrored: true);
        odp.Position = 0;
        var deck = new OdpReader(_themes, _masters).Read(odp).Value!;
        var shape = Single(deck);
        Assert.True(shape.FlipHorizontal);
        Assert.Equal(12500, Preset(shape, "cube").Adjustments.Single().Value, 0);

        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, pptx).Succeeded);
        pptx.Position = 0;
        var back = Single(new PptxReader(_themes, _masters).Read(pptx).Value!);
        Assert.True(back.FlipHorizontal);
        Assert.Equal(12500, Preset(back, "cube").Adjustments.Single().Value, 0);
    }

    [Fact]
    public void Il_cilindro_nativo_porta_la_regolazione_come_nel_riferimento()
    {
        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(new ShapeObject { Id = "forma", Shape = ShapeKind.Cylinder }), odp).Succeeded);
        Native(odp, "can", "4320");
        odp.Position = 0;
        var shape = Single(new OdpReader(_themes, _masters).Read(odp).Value!);
        Assert.Equal(ShapeKind.Cylinder, shape.Shape);
        Assert.Equal(20000, Preset(shape, "can").Adjustments.Single().Value, 0);
    }

    [Fact]
    public void I_modificatori_nativi_si_leggono_anche_da_ods_e_odt()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "forma",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
            Content = new ShapeObject { Id = "forma", Shape = ShapeKind.RoundedRectangle },
        });
        using var ods = new MemoryStream();
        new OdsWriter().Write(workbook, ods);
        Native(ods, "round-rectangle", "5400");
        ods.Position = 0;
        var sheetShape = Assert.IsType<ShapeObject>(Assert.Single(new OdsReader().Read(ods).Value!.Sheets[0].Objects).Content);
        Assert.Equal(25000, Preset(sheetShape, "roundRect").Adjustments.Single().Value, 0);

        var document = new TextDocument();
        var paragraph = new PulseOffice.Documents.Blocks.ParagraphBlock("p1", "Testo");
        document.Body.Replace([paragraph]);
        document.Objects.Add(new AnchoredObject("obj1", new ParagraphAnchor(paragraph.Id), new EmbeddedContent(new ShapeObject { Id = "forma", Shape = ShapeKind.RoundedRectangle }), new DocumentSize(new Twip(2880), new Twip(1440))));
        using var odt = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, odt).Succeeded);
        Native(odt, "round-rectangle", "5400");
        odt.Position = 0;
        Assert.Equal(25000, Preset(Shape(new OdtReader().Read(odt).Value!), "roundRect").Adjustments.Single().Value, 0);
    }

    private ShapeObject Shape(TextDocument document) =>
        Assert.IsType<ShapeObject>(Assert.IsType<EmbeddedContent>(document.Objects.All.Single().Content).Value);
}
