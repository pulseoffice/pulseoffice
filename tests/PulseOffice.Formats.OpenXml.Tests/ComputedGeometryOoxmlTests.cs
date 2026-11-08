using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ComputedGeometryOoxmlTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly PresetGeometry _snip = new("round2DiagRect") { Adjustments = [new GeometryAdjustment("adj1", 25000), new GeometryAdjustment("adj2", 0)] };

    private Presentation Deck(ComputedGeometry geometry)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement
        {
            Id = "forma",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(3000), new Twip(2000)),
            Content = new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle, Computed = geometry },
        });
        deck.AddExisting(slide);
        return deck;
    }

    [Fact]
    public void Una_forma_predefinita_attraversa_pptx_con_le_regolazioni()
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(_snip), buffer).Succeeded);
        buffer.Position = 0;
        using (var document = PresentationDocument.Open(buffer, false))
            Assert.Contains(document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<D.PresetGeometry>()), geometry => geometry.Preset?.InnerText == "round2DiagRect");
        buffer.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(buffer).Value!;

        var shape = back.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single();
        var read = Assert.IsType<PresetGeometry>(shape.Computed);
        Assert.Equal(_snip.Adjustments, read.Adjustments);
    }

    [Fact]
    public void Una_geometria_a_formule_esce_in_pptx_come_tracciato_e_si_dichiara()
    {
        var formula = new FormulaGeometry("M ?f0 0 L 21600 0 21600 21600 0 21600 Z N") { Modifiers = [5400], Equations = [new EnhancedEquation("f0", "$0*2")] };
        var deck = Deck(formula);
        Assert.Contains(new PptxWriter().CheckFidelity(deck).Losses, loss => loss.Code == "PPTX190");
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        using var document = PresentationDocument.Open(buffer, false);
        Assert.Contains(document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<D.CustomGeometry>()), _ => true);
    }

    [Fact]
    public void Una_forma_predefinita_attraversa_xlsx()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "forma",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
            Content = new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle, Computed = _snip },
        });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var shape = Assert.IsType<ShapeObject>(Assert.Single(new XlsxReader().Read(stream).Value!.Sheets[0].Objects).Content);
        Assert.Equal(_snip, shape.Computed);
    }
}
