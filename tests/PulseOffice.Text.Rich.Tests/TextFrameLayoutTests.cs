using PulseOffice.Foundation.Units;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Text.Rich.Tests;

public class TextFrameLayoutTests
{
    private readonly FirstLineBreaker _breaker = new();
    private readonly TextFrameResize _resize = new();

    private double Width(string content, int end) => end * 10.0;

    [Fact]
    public void LaPrimaRigaSiSpezzaDopoLUltimaParolaCheEntra()
    {
        const string content = "uno due tre quattro";

        var split = _breaker.Break(content, 95, end => Width(content, end));

        Assert.Equal(7, split.End);
        Assert.Equal(8, split.Next);
        Assert.Equal("tre quattro", content[split.Next..]);
    }

    [Fact]
    public void UnParagrafoCheEntraNonSiSpezza()
    {
        const string content = "breve";

        var split = _breaker.Break(content, 500, end => Width(content, end));

        Assert.Equal(new LineSplit(5, 5), split);
    }

    [Fact]
    public void IlRitornoACapoChiudeLaPrimaRiga()
    {
        const string content = "ab\ncd ef";

        var split = _breaker.Break(content, 500, end => Width(content, end));

        Assert.Equal(new LineSplit(2, 3), split);
    }

    [Fact]
    public void UnaParolaTroppoLungaRestaSullaPrimaRiga()
    {
        const string content = "lunghissima parola";

        var split = _breaker.Break(content, 30, end => Width(content, end));

        Assert.Equal(11, split.End);
        Assert.Equal(12, split.Next);
    }

    [Fact]
    public void AncorataInAltoCresceVersoIlBasso()
    {
        var bounds = new DocumentRect(new Twip(100), new Twip(1000), new Twip(500), new Twip(400));

        var fitted = _resize.Fit(bounds, new Twip(600), TextAnchor.Top);

        Assert.Equal(1000, fitted.Top.Value);
        Assert.Equal(600, fitted.Height.Value);
    }

    [Fact]
    public void AncorataInBassoTieneFermoIlBordoInferiore()
    {
        var bounds = new DocumentRect(new Twip(100), new Twip(1000), new Twip(500), new Twip(400));

        var fitted = _resize.Fit(bounds, new Twip(600), TextAnchor.Bottom);

        Assert.Equal(1400, fitted.Top.Value + fitted.Height.Value);
        Assert.Equal(800, fitted.Top.Value);
    }

    [Fact]
    public void AncorataAlCentroCresceDaiDueLati()
    {
        var bounds = new DocumentRect(new Twip(100), new Twip(1000), new Twip(500), new Twip(400));

        var fitted = _resize.Fit(bounds, new Twip(200), TextAnchor.Middle);

        Assert.Equal(1100, fitted.Top.Value);
        Assert.Equal(200, fitted.Height.Value);
    }

    [Fact]
    public void IRientriSiApplicanoAiParagrafiDellaSelezione()
    {
        var formatter = new RichTextFormatter();
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph("alfa"), new TextParagraph("beta")]);

        var updated = formatter.SetIndents(text, null, 18, 24);
        var read = formatter.ReadParagraph(updated, null);

        Assert.Equal(18, read.MarginLeft);
        Assert.Equal(24, read.Indent);
        Assert.Equal(TextAnchor.Bottom, formatter.SetAnchor(updated, TextAnchor.Bottom).Frame.Anchor);
    }

    [Fact]
    public void ALongWordStopsAtTheHardBreak()
    {
        var split = new FirstLineBreaker().Break("lunghissima\nb", 1, end => end * 10);
        Assert.Equal(new LineSplit(11, 12), split);
    }

    [Fact]
    public void IlTestoCineseSenzaSpaziSiSpezzaFraGliIdeogrammi()
    {
        const string content = "我们今天去学校。明天回家";

        var split = _breaker.Break(content, 45, end => Width(content, end));

        Assert.Equal(new LineSplit(4, 4), split);
    }

    [Fact]
    public void LaPunteggiaturaDiChiusuraNonApreLaRigaGiapponese()
    {
        const string content = "学校。明日";

        var split = _breaker.Break(content, 25, end => Width(content, end));

        Assert.Equal(new LineSplit(1, 1), split);
    }

    [Fact]
    public void UnIdeogrammaFuoriDalPianoBaseNonSiSpezza()
    {
        const string content = "\U00020000\U00020001\U00020002";

        var split = _breaker.Break(content, 25, end => Width(content, end));

        Assert.Equal(new LineSplit(2, 2), split);
    }

    [Fact]
    public void IlSelettoreDiVarianteRestaConLIdeogramma()
    {
        const string content = "葛\U000E0100葛葛";

        var split = _breaker.Break(content, 25, end => Width(content, end));

        Assert.Equal(new LineSplit(3, 3), split);
    }
}
