using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class MotionModelTests
{
    private readonly TransitionCatalog _catalog = new();

    [Fact]
    public void Il_catalogo_copre_ogni_transizione_una_volta_sola_in_tre_gruppi()
    {
        Assert.Equal(Enum.GetValues<TransitionKind>().Order(), _catalog.Entries.Select(entry => entry.Kind).Order());
        Assert.Equal(_catalog.Entries.Count, _catalog.Entries.Select(entry => entry.Kind).Distinct().Count());
        Assert.Equal(3, _catalog.Entries.Select(entry => entry.Group).Distinct().Count());
    }

    [Theory]
    [InlineData(TransitionKind.Push)]
    [InlineData(TransitionKind.Wipe)]
    [InlineData(TransitionKind.Cover)]
    [InlineData(TransitionKind.Uncover)]
    public void Le_transizioni_direzionali_offrono_i_quattro_lati_che_i_formati_sanno_scrivere(TransitionKind kind) =>
        Assert.Equal(
            [TransitionDirection.Left, TransitionDirection.Right, TransitionDirection.Up, TransitionDirection.Down],
            _catalog.Find(kind).Directions);

    [Fact]
    public void Scegliere_un_effetto_conserva_la_direzione_se_vale_e_la_toglie_se_non_serve()
    {
        var push = _catalog.Choose(new SlideTransition { Kind = TransitionKind.Wipe, Direction = TransitionDirection.Up, DurationMs = 1200 }, TransitionKind.Push);
        Assert.Equal(TransitionDirection.Up, push.Direction);
        Assert.Equal(1200, push.DurationMs);

        var fade = _catalog.Choose(push, TransitionKind.Fade);
        Assert.Equal(TransitionDirection.None, fade.Direction);

        var cover = _catalog.Choose(fade, TransitionKind.Cover);
        Assert.Equal(TransitionDirection.Left, cover.Direction);
    }

    [Fact]
    public void I_suoni_del_documento_si_elencano_una_volta_sola()
    {
        var deck = new Presentation();
        var master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);
        deck.Master = master;
        var factory = new SlideFactory();
        for (var index = 0; index < 3; index++) deck.AddExisting(factory.Create(deck.NextId(), master, SlideLayout.Blank));
        deck.Slides[0].Transition = deck.Slides[0].Transition with { SoundPath = "/a/zeta.wav" };
        deck.Slides[1].Transition = deck.Slides[1].Transition with { SoundPath = "/a/alfa.wav" };
        deck.Slides[2].Transition = deck.Slides[2].Transition with { SoundPath = "/a/zeta.wav" };

        Assert.Equal(["/a/alfa.wav", "/a/zeta.wav"], _catalog.SoundsIn(deck));
    }

    [Fact]
    public void La_linea_temporale_numera_i_clic_e_proporziona_le_barre()
    {
        var timeline = new AnimationTimeline(new AnimationScheduler());
        var view = timeline.Build(
        [
            Effect("a", AnimationTrigger.OnClick, 1000),
            Effect("b", AnimationTrigger.WithPrevious, 500),
            Effect("c", AnimationTrigger.AfterPrevious, 1000),
            Effect("d", AnimationTrigger.OnClick, 1000),
        ]);

        Assert.Equal([1, null, null, 2], view.Rows.Select(row => row.ClickNumber));
        Assert.Equal(0, view.Rows[1].StartMs);
        Assert.Equal(500, view.Rows[2].StartMs);
        Assert.True(view.TotalMs >= view.Rows[3].EndMs);
        Assert.InRange(view.LengthShare(view.Rows[0]), 0.01, 1);
        Assert.Equal(0, view.StartShare(view.Rows[0]));
    }

    [Fact]
    public void Riordinare_sostituire_e_togliere_lasciano_intatti_gli_altri_effetti()
    {
        var ordering = new AnimationOrdering();
        IReadOnlyList<AnimationEffect> effects = [Effect("a"), Effect("b"), Effect("c")];

        Assert.Equal(["b", "c", "a"], ordering.Move(effects, 0, 2).Select(effect => effect.Id));
        Assert.Equal(["c", "a", "b"], ordering.Move(effects, 2, 0).Select(effect => effect.Id));
        Assert.Same(effects, ordering.Move(effects, 5, 0));
        Assert.Equal(900, ordering.Replace(effects, effects[1] with { DurationMs = 900 })[1].DurationMs);
        Assert.Equal(["a", "c"], ordering.Remove(effects, "b").Select(effect => effect.Id));
    }

    [Fact]
    public void L_anteprima_dei_preset_parte_nascosta_e_arriva_a_riposo()
    {
        var preview = new AnimationPresetPreview();
        var registry = new AnimationPresetRegistry();
        foreach (var category in new[] { AnimationCategory.Entrance, AnimationCategory.Exit })
            foreach (var preset in registry.Presets(category))
            {
                var start = preview.At(category, preset, category == AnimationCategory.Entrance ? 0 : 1);
                var end = preview.At(category, preset, category == AnimationCategory.Entrance ? 1 : 0);
                Assert.True(start.Opacity < 0.01 || start.Reveal < 0.01 || start.Scale < 0.5 || Math.Abs(start.OffsetY) > 0.1, $"{category} {preset}");
                Assert.Equal(1, end.Opacity, 3);
                Assert.Equal(1, end.Reveal, 3);
            }

        Assert.NotEqual(0, preview.At(AnimationCategory.Emphasis, "spin", 0.5).Rotation);
        Assert.NotEqual(0, preview.At(AnimationCategory.MotionPath, "line", 0.5).OffsetX + 0.5);
    }

    [Fact]
    public void Le_presentazioni_personalizzate_inseriscono_spostano_e_tolgono_per_posizione()
    {
        var editing = new CustomShowEditing();
        IReadOnlyList<string> show = ["a", "b", "c"];
        Assert.Equal(["a", "x", "b", "c"], editing.Insert(show, ["x"], 1));
        Assert.Equal(["b", "c", "a"], editing.Move(show, 0, 3));
        Assert.Equal(["c", "a", "b"], editing.Move(show, 2, 0));
        Assert.Equal(["a", "c"], editing.RemoveAt(show, 1));
        Assert.Same(show, editing.RemoveAt(show, 9));
    }

    [Fact]
    public void Lo_sfondo_immagine_diventa_riempimento_e_torna_immagine()
    {
        var backgrounds = new SlideBackgrounds();
        var slide = new Slide("s", SlideLayout.Blank)
        {
            BackgroundImage = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "i", Data = new byte[] { 1, 2 }, MediaType = "image/png" },
        };
        var fill = backgrounds.Current(slide);
        Assert.Equal(PulseOffice.Drawing.Shapes.ShapeFillKind.Bitmap, fill.Kind);
        var choice = backgrounds.Choose(fill, hideGraphics: true);
        Assert.NotNull(choice.Image);
        Assert.Null(choice.Fill);
        Assert.True(choice.HideGraphics);
        Assert.Null(backgrounds.Choose(PulseOffice.Drawing.Shapes.ShapeFill.Default with { Kind = PulseOffice.Drawing.Shapes.ShapeFillKind.None }, false).Fill);
    }

    private static AnimationEffect Effect(string id, AnimationTrigger trigger = AnimationTrigger.OnClick, int duration = 500) =>
        new() { Id = id, TargetId = "t" + id, Trigger = trigger, DurationMs = duration };
}
