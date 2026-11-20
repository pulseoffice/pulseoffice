using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round332OdpFurtherLayoutDecorationTests
{
    [Fact]
    public void A_further_layout_of_a_type_keeps_its_own_decoration()
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var theme = themes.Find("Light")!;
        var master = masters.CreateStandard(theme);
        var basis = master.Template(SlideLayout.TitleOnly);
        var band = new ObjectElement
        {
            Id = "band",
            Bounds = new DocumentRect(new Twip(500), new Twip(9000), new Twip(4000), new Twip(600)),
            Content = new ShapeObject { Id = "band", Shape = ShapeKind.Ellipse },
        };
        var custom = basis with { Id = "layout.custom.21", Name = "Banner", Objects = [band] };
        var deck = new Presentation { Theme = theme, Master = master with { Layouts = [.. master.Layouts, custom] } };

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var back = new OdpReader(themes, masters).Read(buffer).Value!;

        var banner = Assert.Single(back.Master.Layouts, layout => layout.Id == "layout.custom.21");
        var shape = Assert.IsType<ObjectElement>(Assert.Single(banner.Objects));
        Assert.Equal(ShapeKind.Ellipse, Assert.IsType<ShapeObject>(shape.Content).Shape);
        Assert.Equal(band.Bounds.Left, shape.Bounds.Left);
        Assert.Equal(back.Master.Template(SlideLayout.TitleOnly).Objects.Count, basis.Objects.Count);
    }
}
