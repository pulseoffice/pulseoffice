using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round214ShapeTextLayoutTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private readonly TextLayout _layout = new() { Flow = TextFlow.Vertical270, Columns = 2, ColumnSpacing = 18 };

    private ShapeStyle Style() => ShapeStyle.Default with { Text = ShapeTextStyle.Default with { Layout = _layout, RotationDegrees = 30 } };

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleAndContent);
        slide.Add(new ObjectElement { Id = "a", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(2000), new Twip(1000)), Content = new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle, Text = "x", Style = Style() } });
        var body = slide.Elements.OfType<TextElement>().Single(element => element.Text.Role == PlaceholderRole.Body);
        slide.Replace(body with { Text = body.Text with { Frame = body.Text.Frame with { Layout = _layout with { Flow = TextFlow.Stacked }, RotationDegrees = 45 } } });
        deck.AddExisting(slide);
        return deck;
    }

    private void AssertDeck(Presentation deck)
    {
        var slide = deck.Slides[0];
        var shape = slide.Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single();
        Assert.Equal(_layout, shape.Style.Text.Layout);
        Assert.Equal(30, shape.Style.Text.RotationDegrees, 1);
        var body = slide.Elements.OfType<TextElement>().Single(element => element.Text.Role == PlaceholderRole.Body);
        Assert.Equal(_layout with { Flow = TextFlow.Stacked }, body.Text.Frame.Layout);
        Assert.Equal(45, body.Text.Frame.RotationDegrees, 1);
    }

    [Fact]
    public void Shape_and_text_box_layout_survive_PPTX()
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(), buffer).Succeeded);
        buffer.Position = 0;
        var read = new PptxReader(_themes, _masters).Read(buffer);
        AssertDeck(read.Value!);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "PPTX226");
    }

    [Fact]
    public void Shape_and_text_box_layout_survive_ODP()
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(), buffer).Succeeded);
        buffer.Position = 0;
        var read = new OdpReader(_themes, _masters).Read(buffer);
        AssertDeck(read.Value!);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODP217");
    }

    private Workbook Sheet()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Data").Objects.Add(new PlacedObject
        {
            Id = "s",
            Content = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Text = "x", Style = Style() },
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
        });
        return workbook;
    }

    private ShapeObject Shape(Workbook workbook) => workbook.Sheets[0].Objects.Select(placed => placed.Content).OfType<ShapeObject>().Single();

    [Fact]
    public void Sheet_shape_layout_survives_XLSX_and_ODS()
    {
        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(Sheet(), xlsx);
        xlsx.Position = 0;
        Assert.Equal(_layout, Shape(new XlsxReader().Read(xlsx).Value!).Style.Text.Layout);

        using var ods = new MemoryStream();
        new OdsWriter().Write(Sheet(), ods);
        ods.Position = 0;
        var shape = Shape(new OdsReader().Read(ods).Value!);
        Assert.Equal(_layout, shape.Style.Text.Layout);
        Assert.Equal(30, shape.Style.Text.RotationDegrees, 1);
    }
}
