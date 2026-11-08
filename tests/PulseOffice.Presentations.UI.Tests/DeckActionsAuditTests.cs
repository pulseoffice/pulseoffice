using PulseOffice.Document.Commands;
using PulseOffice.Document.Forms;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckActionsAuditTests
{
    private readonly Presentation _deck = new();
    private readonly UndoStack _history = new();
    private readonly DeckActions _actions;
    private readonly ISlideFactory _factory = new SlideFactory();

    public DeckActionsAuditTests()
    {
        _deck.Master = new MasterFactory().CreateStandard(_deck.Theme);
        _actions = new DeckActions(_deck, _history, _factory, new FakeTextCatalog());
        _deck.AddExisting(_factory.Create(_deck.NextId(), _deck.Master, SlideLayout.TitleAndContent));
        _deck.AddExisting(_factory.Create(_deck.NextId(), _deck.Master, SlideLayout.TitleAndContent));
    }

    [Fact]
    public void DuplicareUnaDiapositivaFuoriDallaPresentazioneNonFaNulla()
    {
        var foreign = _factory.Create("altrove", _deck.Master, SlideLayout.Blank);

        Assert.Null(_actions.DuplicateSlide(foreign));
        Assert.Equal(2, _deck.Slides.Count);
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void SpostareOltreLaFineLUltimaDiapositivaNonLasciaUnPassoVuotoNelloStorico()
    {
        _actions.MoveSlide(_deck.Slides[^1], 99);

        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void LaStrutturaApplicataATutteLeDiapositiveEUnSoloAnnullamento()
    {
        _actions.SetSlideTexts(
        [
            (_deck.Slides[0], PlaceholderRole.Title, "Uno"),
            (_deck.Slides[0], PlaceholderRole.Body, "a"),
            (_deck.Slides[1], PlaceholderRole.Title, "Due"),
        ]);

        Assert.True(_history.CanUndo);
        Assert.Equal("Due", _deck.Slides[1].Placeholder(PlaceholderRole.Title)!.Text.PlainText);
        _history.Undo();
        Assert.False(_history.CanUndo);
        Assert.Equal(string.Empty, _deck.Slides[0].Placeholder(PlaceholderRole.Title)!.Text.PlainText);
    }

    [Fact]
    public void Un_corpo_con_gli_a_capo_diventa_paragrafi_veri_e_non_resta_vuoto()
    {
        _actions.SetSlideText(_deck.Slides[0], PlaceholderRole.Body, "a\nb");

        var body = _deck.Slides[0].Placeholder(PlaceholderRole.Body)!.Text;
        Assert.False(body.IsEmpty);
        Assert.Equal(["a", "b"], body.Paragraphs.Select(p => p.Text));
        Assert.All(body.Paragraphs, p => Assert.Equal(p.Text, string.Concat(p.EffectiveRuns().Select(r => r.Text))));

        _actions.SetSlideText(_deck.Slides[1], PlaceholderRole.Body, "uno\r\ndue\rtre\u2029quattro\u0085cinque");
        Assert.Equal(["uno", "due", "tre", "quattro", "cinque"], _deck.Slides[1].Placeholder(PlaceholderRole.Body)!.Text.Paragraphs.Select(p => p.Text));
    }

    [Fact]
    public void TestiUgualiNonRegistranoNulla()
    {
        _actions.SetSlideTexts([(_deck.Slides[0], PlaceholderRole.Title, string.Empty)]);
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void AssegnareLoStessoMasterNonRegistraNulla()
    {
        _actions.AssignMaster([_deck.Slides[0]], _deck.Master);
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void IlTemaPrincipaleUsaIlMasterPrincipale()
    {
        var other = new MasterFactory().CreateStandard(new ThemeCatalog().Themes[1]) with { Id = "m2" };
        _deck.AddMaster(other);

        var lookup = new ThemeMasterLookup();

        Assert.Same(_deck.Master, lookup.Find(_deck, _deck.Theme));
        Assert.Same(other, lookup.Find(_deck, other.Theme));
    }

    [Fact]
    public void NomiDoppiNellOrdineDiAttivazioneNonFannoCadereNulla()
    {
        ObjectElement Control(string id) => new()
        {
            Id = id,
            Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(100), new Twip(100)),
            Content = new FormControlObject { Id = id, ControlKind = FormControlKind.CheckBox, Properties = new FormControlProperties { Name = "Spunta" } },
        };

        var updated = new ControlTabOrder().Apply([Control("a"), Control("b")], ["Spunta", "Spunta"]);

        Assert.Equal([0, 1], updated.Select(e => ((FormControlObject)((ObjectElement)e).Content).Properties.TabIndex));
    }

    [Fact]
    public void UnaModificaDImmagineSuUnaDiapositivaEliminataNonRegistraNulla()
    {
        var slide = _deck.Slides[1];
        slide.Add(Image());
        _actions.DeleteSlide(slide);
        var count = _history.StatePoint;

        _actions.EditImage(slide, "img", raster => raster with { Name = "nuova" }, "label");

        Assert.Same(count, _history.StatePoint);
    }

    [Fact]
    public void UnaModificaDImmagineSenzaDifferenzeNonRegistraNulla()
    {
        _deck.Slides[0].Add(Image());
        _actions.EditImage(_deck.Slides[0], "img", raster => raster, "label");
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void SostituireUnOggettoConSeStessoNonRegistraNulla()
    {
        var element = Image();
        _deck.Slides[0].Add(element);
        _actions.ReplaceElement(_deck.Slides[0], element, "label");
        Assert.False(_history.CanUndo);
    }

    private ObjectElement Image() => new()
    {
        Id = "img",
        Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(100), new Twip(100)),
        Content = new RasterImageObject { Id = "raster", Data = new byte[] { 1, 2, 3 }, MediaType = "image/png" },
    };

    [Fact]
    public void Lo_sfondo_a_gradiente_sostituisce_l_immagine_e_si_annulla()
    {
        var slide = _deck.Slides[0];
        _actions.SetBackgroundImage(slide, new RasterImageObject { Id = "i", Data = new byte[] { 1 }, MediaType = "image/png" });
        var gradient = PulseOffice.Drawing.Shapes.ShapeFill.Default with { Kind = PulseOffice.Drawing.Shapes.ShapeFillKind.Gradient };

        _actions.SetBackgroundFill(slide, gradient);
        Assert.Same(gradient, slide.BackgroundFill);
        Assert.Null(slide.BackgroundImage);

        _history.Undo();
        Assert.Null(slide.BackgroundFill);
        Assert.NotNull(slide.BackgroundImage);
    }

    [Fact]
    public void LOggettoInseritoStaSopraATuttoAncheConZOrderAltiDaUnFile()
    {
        var slide = _deck.Slides[0];
        slide.Add(new ObjectElement { Id = "alto", ZOrder = 40, Bounds = new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(10), Twip.FromPoints(10)), Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "alto", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle } });

        _actions.InsertObject(slide, new ObjectElement { Id = "nuovo", ZOrder = slide.Elements.Count, Bounds = new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(10), Twip.FromPoints(10)), Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "nuovo", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle } });

        Assert.Equal("nuovo", slide.InZOrder().Last().Id);
    }
}
