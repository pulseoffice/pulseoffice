using PulseOffice.Localization;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckInteractionFormTests
{
    private const string Sound = "Media/applauso.wav";

    private readonly DeckInteractionForm _form = new();

    private readonly IReadOnlyList<Slide> _slides = [new Slide("s1", SlideLayout.Blank), new Slide("s2", SlideLayout.Blank)];

    [Fact]
    public void Un_suono_al_clic_compare_come_Riproduci_un_suono_ed_e_selezionato()
    {
        var current = new InteractiveAction { TargetId = "forma", Kind = InteractionKind.PlaySound, SoundPath = Sound };

        var choices = _form.Open(current, _slides);

        Assert.Equal(InteractionKind.PlaySound, choices.Kinds[choices.Kind]);
        Assert.Equal("deck.interactionDialog.playSound", _form.LabelKey(choices.Kinds[choices.Kind]));
    }

    [Fact]
    public void Confermando_senza_cambiare_il_suono_resta()
    {
        var current = new InteractiveAction { TargetId = "forma", Kind = InteractionKind.PlaySound, SoundPath = Sound };
        var choices = _form.Open(current, _slides);

        var built = _form.Build("forma", _form.Read(choices, Values(choices.Kind), _slides), current);

        Assert.Equal(InteractionKind.PlaySound, built!.Kind);
        Assert.Equal(Sound, built.SoundPath);
    }

    [Fact]
    public void Cambiando_azione_il_suono_resta()
    {
        var current = new InteractiveAction { TargetId = "forma", Kind = InteractionKind.PlaySound, SoundPath = Sound };
        var choices = _form.Open(current, _slides);
        var next = choices.Kinds.ToList().IndexOf(InteractionKind.GoToSlide);

        var built = _form.Build("forma", _form.Read(choices, Values(next, slide: 1), _slides), current);

        Assert.Equal(InteractionKind.GoToSlide, built!.Kind);
        Assert.Equal("s2", built.Reference);
        Assert.Equal(Sound, built.SoundPath);
    }

    [Fact]
    public void Un_azione_con_suono_offre_anche_Riproduci_un_suono()
    {
        var current = new InteractiveAction { TargetId = "forma", Kind = InteractionKind.NextSlide, SoundPath = Sound };

        var choices = _form.Open(current, _slides);

        Assert.Contains(InteractionKind.PlaySound, choices.Kinds);
        Assert.Equal(InteractionKind.NextSlide, choices.Kinds[choices.Kind]);
    }

    [Fact]
    public void Senza_suono_Riproduci_un_suono_non_compare()
    {
        Assert.DoesNotContain(InteractionKind.PlaySound, _form.Open(null, _slides).Kinds);
    }

    [Fact]
    public void Nessuna_azione_toglie_l_interazione()
    {
        var current = new InteractiveAction { TargetId = "forma", Kind = InteractionKind.PlaySound, SoundPath = Sound };
        var choices = _form.Open(current, _slides);

        Assert.Null(_form.Build("forma", _form.Read(choices, Values(0), _slides), current));
    }

    [Theory]
    [InlineData(UiLanguage.Italian)]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.German)]
    [InlineData(UiLanguage.Spanish)]
    [InlineData(UiLanguage.French)]
    public void Le_voci_dell_azione_sono_tradotte_in_ogni_lingua(UiLanguage language)
    {
        var keys = new TextCatalog(language).KeysIn(language);
        var current = new InteractiveAction { TargetId = "forma", Kind = InteractionKind.PlaySound, SoundPath = Sound };

        foreach (var kind in _form.Open(current, _slides).Kinds)
            Assert.Contains(_form.LabelKey(kind), keys);
    }

    private Dictionary<string, object> Values(int kind, int slide = 0) => new()
    {
        [DeckInteractionForm.KindKey] = kind,
        [DeckInteractionForm.SlideKey] = slide,
        [DeckInteractionForm.ReferenceKey] = string.Empty,
    };
}
