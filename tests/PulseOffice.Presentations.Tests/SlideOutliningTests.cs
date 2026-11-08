using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.Tests;

public sealed class SlideOutliningTests
{
    private readonly SlideOutlining _outlining = new();

    private Slide WithText(string id, string title, params TextParagraph[] body)
    {
        var slide = new Slide(id, SlideLayout.TitleAndContent);
        slide.Add(new TextElement { Id = $"{id}.t", Text = new RichText(PlaceholderRole.Title, [new TextParagraph(title)]) });
        slide.Add(new TextElement { Id = $"{id}.b", Text = new RichText(PlaceholderRole.Body, body) });
        return slide;
    }

    [Fact]
    public void IlRiepilogoElencaITitoliVisibiliNonVuoti()
    {
        var hidden = WithText("s3", "Nascosta");
        hidden.Hidden = true;

        var summary = _outlining.Summary([WithText("s1", "Introduzione"), WithText("s2", " "), hidden, WithText("s4", "Conclusioni")]);

        Assert.NotNull(summary);
        Assert.Equal(["Introduzione", "Conclusioni"], summary.Body.Select(p => p.Text));
        Assert.All(summary.Body, p => Assert.True(p.Bullet));
    }

    [Fact]
    public void SenzaTitoliNonCeRiepilogo() =>
        Assert.Null(_outlining.Summary([WithText("s1", "")]));

    [Fact]
    public void EspandereFaUnaDiapositivaPerOgniPuntoDiPrimoLivello()
    {
        var slide = WithText("s1", "Piano",
            new TextParagraph("Obiettivi") { Bullet = true },
            new TextParagraph("Crescita") { Bullet = true, BulletLevel = 1 },
            new TextParagraph("Dettaglio") { Bullet = true, BulletLevel = 2 },
            new TextParagraph("") { Bullet = true },
            new TextParagraph("Rischi") { Bullet = true });

        var expanded = _outlining.Expand(slide);

        Assert.Equal(["Obiettivi", "Rischi"], expanded.Select(e => e.Title));
        Assert.Equal(["Crescita", "Dettaglio"], expanded[0].Body.Select(p => p.Text));
        Assert.Equal([0, 1], expanded[0].Body.Select(p => p.BulletLevel));
        Assert.Empty(expanded[1].Body);
    }
}
