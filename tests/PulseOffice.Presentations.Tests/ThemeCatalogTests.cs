using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using PulseOffice.Presentations.Decorations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.Tests;

public sealed class ThemeCatalogTests
{
    private readonly ThemeCatalog _catalog = new();
    private readonly MasterFactory _masters = new();

    [Fact]
    public void IlCatalogoHaAlmenoVentiTemiConNomiUnici()
    {
        Assert.True(_catalog.Themes.Count >= 20);
        Assert.Equal(_catalog.Themes.Count, _catalog.Themes.Select(theme => theme.Name.ToUpperInvariant()).Distinct().Count());
        Assert.All(_catalog.Themes, theme => Assert.Matches("^[A-Z][A-Za-z]+$", theme.Name));
    }

    [Fact]
    public void CiSonoTemiChiariETemiScuri()
    {
        Assert.Contains(_catalog.Themes, theme => Luminance(theme.Colors.Background) < 0.2);
        Assert.Contains(_catalog.Themes, theme => Luminance(theme.Colors.Background) > 0.8);
    }

    [Fact]
    public void IlTestoSiLeggeSulloSfondo()
    {
        foreach (var theme in _catalog.Themes)
        {
            Assert.True(Contrast(theme.Colors.Text, theme.Colors.Background) >= 4.5, theme.Name);
            Assert.True(Contrast(theme.Colors.Text, theme.Colors.Background2) >= 4.5, theme.Name);
            Assert.True(Contrast(theme.Colors.Text2, theme.Colors.Background) >= 4.5, theme.Name);
        }
    }

    [Fact]
    public void INomiItalianiDiPrimaSonoAlias()
    {
        Assert.Equal("Light", _catalog.Find("Chiaro")!.Name);
        Assert.Equal("Slate", _catalog.Find("Ardesia")!.Name);
        Assert.Equal("Sand", _catalog.Find("sabbia")!.Name);
        Assert.Null(_catalog.Find("Inesistente"));
    }

    [Fact]
    public void OgniTemaDecoratoHaGraficaSulMasterESulTitolo()
    {
        foreach (var theme in _catalog.Themes.Where(theme => theme.Decoration != ThemeDecorationKind.None))
        {
            var master = _masters.CreateStandard(theme);
            Assert.NotEmpty(master.Objects);
            var title = master.Template(SlideLayout.TitleSlide);
            Assert.True(title.HideMasterObjects, theme.Name);
            Assert.NotEmpty(title.Objects);
            Assert.NotEmpty(master.Template(SlideLayout.SectionHeader).Objects);
            Assert.Empty(master.Template(SlideLayout.TitleAndContent).Objects);
        }
    }

    [Fact]
    public void LaDecorazioneStaSullaDiapositivaEHaIdentificativiUnici()
    {
        var size = SlideSize.Widescreen;
        foreach (var theme in _catalog.Themes)
        {
            var master = _masters.CreateStandard(theme);
            var all = master.Objects.Concat(master.Layouts.SelectMany(layout => layout.Objects)).ToList();
            foreach (var element in all)
            {
                Assert.True(element.Bounds.Width.Value > 0 && element.Bounds.Height.Value > 0, theme.Name);
                Assert.True(element.Bounds.Left.Value < size.Width.Value && element.Bounds.Left.Value + element.Bounds.Width.Value > 0, theme.Name);
                Assert.True(element.Bounds.Top.Value < size.Height.Value && element.Bounds.Top.Value + element.Bounds.Height.Value > 0, theme.Name);
                Assert.IsType<ShapeObject>(Assert.IsType<ObjectElement>(element).Content);
            }

            Assert.Equal(master.Objects.Count, master.Objects.Select(element => element.Id).Distinct().Count());
        }
    }

    [Fact]
    public void LaGraficaDelMasterNonCopreIlTitoloDelContenuto()
    {
        foreach (var theme in _catalog.Themes)
        {
            var master = _masters.CreateStandard(theme);
            var title = master.Template(SlideLayout.TitleAndContent).Placeholders.First(p => p.Role == PlaceholderRole.Title).Bounds;
            foreach (var element in master.Objects.Where(element => Opaque(element)))
            {
                var covered = Overlap(element.Bounds, title);
                Assert.True(covered < 0.05 * title.Width.Value * title.Height.Value, $"{theme.Name} {element.Id}");
            }
        }
    }

    [Fact]
    public void CambiandoTemaLaGraficaPrendeIColoriNuovi()
    {
        var meridian = _masters.CreateStandard(_catalog.Find("Meridian")!);
        var slate = _masters.CreateStandard(_catalog.Find("Slate")!);
        var first = (ShapeObject)((ObjectElement)meridian.Objects[0]).Content;
        var other = (ShapeObject)((ObjectElement)slate.Objects[0]).Content;

        Assert.Equal("#1E3A8A", first.Style.Fill.ColorHex);
        Assert.Equal("#60A5FA", other.Style.Fill.ColorHex);
    }

    [Fact]
    public void LaGraficaSiDistribuisceSuDiapositiveECambiaFormato()
    {
        var master = _masters.CreateStandard(_catalog.Find("Prism")!);
        Assert.Equal(master.Template(SlideLayout.TitleSlide).Objects, master.DecorationFor(SlideLayout.TitleSlide));
        Assert.Equal(master.Objects, master.DecorationFor(SlideLayout.TitleAndContent));

        var fitted = new SlideResize().Fit(master, SlideSize.Widescreen, new SlideSize(SlideSize.Widescreen.Width, SlideSize.Widescreen.Width));
        Assert.NotEqual(master.Objects[0].Bounds, fitted.Objects[0].Bounds);
    }

    [Fact]
    public void UnTemaSenzaDecorazioneNonAggiungeNulla()
    {
        var master = _masters.CreateStandard(_catalog.Default);
        Assert.Empty(master.Objects);
        Assert.Same(MasterDecoration.Empty, new ThemeDecorations().Build(_catalog.Find("Prism")! with { Decoration = ThemeDecorationKind.None }));
    }

    private bool Opaque(SlideElement element) =>
        element is ObjectElement { Content: ShapeObject shape } && shape.Style.Fill.Kind == ShapeFillKind.Solid && shape.Style.Fill.Opacity > 0.5;

    private double Overlap(PulseOffice.Foundation.Units.DocumentRect a, PulseOffice.Foundation.Units.DocumentRect b)
    {
        var width = Math.Min(a.Left.Value + a.Width.Value, b.Left.Value + b.Width.Value) - Math.Max(a.Left.Value, b.Left.Value);
        var height = Math.Min(a.Top.Value + a.Height.Value, b.Top.Value + b.Height.Value) - Math.Max(a.Top.Value, b.Top.Value);
        return width > 0 && height > 0 ? (double)width * height : 0;
    }

    private double Contrast(uint a, uint b)
    {
        var (light, dark) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (light + 0.05) / (dark + 0.05);
    }

    private double Luminance(uint argb)
    {
        double Channel(int shift)
        {
            var value = ((argb >> shift) & 0xFF) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(16) + 0.7152 * Channel(8) + 0.0722 * Channel(0);
    }
}
