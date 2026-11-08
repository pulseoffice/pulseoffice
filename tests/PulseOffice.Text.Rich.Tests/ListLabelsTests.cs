using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Text.Rich.Tests;

public sealed class ListLabelsTests
{
    private TextParagraph N(int level = 0) => new("x") { Numbered = true, BulletLevel = level };

    [Fact]
    public void LaNumerazioneContaPerLivelloERiparteSottoUnPadre()
    {
        var labels = new ListLabels().For([N(), N(), N(1), N(1), N(), N(1)]);
        Assert.Equal(["1.", "2.", "1.", "2.", "3.", "1."], labels);
    }

    [Fact]
    public void UnParagrafoFuoriElencoRicominciaLaNumerazione()
    {
        var labels = new ListLabels().For([N(), new TextParagraph("y"), N()]);
        Assert.Equal(["1.", "", "1."], labels);
    }

    [Fact]
    public void IPuntiNonContano()
    {
        var labels = new ListLabels().For([new TextParagraph("a") { Bullet = true }, N()]);
        Assert.Equal([BulletMarker.Symbol, "1."], labels);
    }

    [Fact]
    public void IlCicloDelleMaiuscoleProcede()
    {
        Assert.Equal("CIAO MONDO", new PulseOffice.Text.TextCases().NextInCycle("Ciao mondo").Apply("Ciao mondo"));
        Assert.Equal("ciao mondo", new PulseOffice.Text.TextCases().NextInCycle("CIAO MONDO").Apply("CIAO MONDO"));
        Assert.Equal("Ciao Mondo", new PulseOffice.Text.TextCases().NextInCycle("ciao mondo").Apply("ciao mondo"));
        Assert.Equal("Ciao mondo", new PulseOffice.Text.TextCases().NextInCycle("Ciao Mondo").Apply("Ciao Mondo"));
    }

    [Fact]
    public void UnCatalogoIncompletoVieneRifiutatoSubito()
    {
        Assert.Throws<ArgumentException>(() => new PulseOffice.Text.TextCases([new PulseOffice.Text.UpperCase()]));
    }

    [Fact]
    public void Gli_schemi_di_numerazione_usano_stile_affissi_e_inizio()
    {
        var format = new ListNumberFormat { Style = ListNumberStyle.UpperRoman, Prefix = "(", Suffix = ")", Start = 3 };
        var letters = new ListNumberFormat { Style = ListNumberStyle.LowerLetter, Suffix = ")", Start = 26 };

        var labels = new ListLabels().For([N() with { NumberFormat = format }, N() with { NumberFormat = format }, N(1) with { NumberFormat = letters }, N(1) with { NumberFormat = letters }]);

        Assert.Equal(["(III)", "(IV)", "z)", "aa)"], labels);
    }

    [Fact]
    public void Un_formato_ostile_si_normalizza()
    {
        var formats = new ListNumberFormats();

        var normal = formats.Normalized(new ListNumberFormat { Style = (ListNumberStyle)99, Prefix = "\u0001" + new string('x', 100), Start = -5, DrawingScheme = "\n" });

        Assert.Equal(ListNumberStyle.Arabic, normal!.Style);
        Assert.Equal(ListNumberFormat.LongestAffix, normal.Prefix.Length);
        Assert.Equal(1, normal.Start);
        Assert.Null(normal.DrawingScheme);
        Assert.Null(formats.Normalized(new ListNumberFormat()));
        Assert.Equal("4000.", formats.Label(new ListNumberFormat { Style = ListNumberStyle.LowerRoman, Start = 4000 }, 1));
        Assert.Equal("2147483647.", formats.Label(new ListNumberFormat { Start = ListNumberFormat.LargestStart }, int.MaxValue));
    }
}
