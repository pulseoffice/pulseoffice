using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class PaginationTests
{
    private string Lines(int count) => string.Join(' ', Enumerable.Range(0, count).Select(_ => "aaaaaaaaa"));

    [Fact]
    public void IParagrafiScorronoSullePagine()
    {
        var f = new LayoutFixture(width: 60, height: 120);
        f.Add(Lines(25), p => p with { Widows = 0, Orphans = 0 });

        var layout = f.Layout();

        Assert.Equal(3, layout.Pages.Count);
        Assert.Equal([10, 10, 5], layout.Pages.Select(page => page.Lines.Count));
        Assert.Equal(0, layout.Pages[1].Lines[0].Top, 3);
    }

    [Fact]
    public void LeVedoveTiranoGiuUnaRiga()
    {
        var f = new LayoutFixture(width: 60, height: 120);
        f.Add(Lines(11));

        var layout = f.Layout();

        Assert.Equal([9, 2], layout.Pages.Select(page => page.Lines.Count));
    }

    [Fact]
    public void LeOrfaneSpostanoIlParagrafo()
    {
        var f = new LayoutFixture(width: 60, height: 120);
        f.Add(Lines(9));
        var second = f.Add(Lines(5));

        var layout = f.Layout();

        Assert.Equal(9, layout.Pages[0].Lines.Count);
        Assert.All(layout.Pages[1].Lines, line => Assert.Equal(second.Id, line.ParagraphId));
    }

    [Fact]
    public void TieniInsiemeSpostaTuttoIlParagrafo()
    {
        var f = new LayoutFixture(width: 60, height: 120);
        f.Add(Lines(6));
        var kept = f.Add(Lines(6), p => p with { KeepTogether = true });

        var layout = f.Layout();

        Assert.Equal(6, layout.Pages[0].Lines.Count);
        Assert.Equal(6, layout.Pages[1].Lines.Count(line => line.ParagraphId == kept.Id));
    }

    [Fact]
    public void TieniConIlSuccessivoPortaIlTitoloSullaPaginaDopo()
    {
        var f = new LayoutFixture(width: 60, height: 120);
        f.Add(Lines(8), p => p with { Widows = 0, Orphans = 0 });
        var title = f.Add("Titolo", p => p with { KeepWithNext = true });
        var body = f.Add(Lines(4));

        var layout = f.Layout();

        Assert.Equal(8, layout.Pages[0].Lines.Count);
        Assert.Equal(title.Id, layout.Pages[1].Lines[0].ParagraphId);
        Assert.Equal(body.Id, layout.Pages[1].Lines[1].ParagraphId);
    }

    [Fact]
    public void SenzaTieniConIlSuccessivoIlTitoloRestaInFondo()
    {
        var f = new LayoutFixture(width: 60, height: 120);
        f.Add(Lines(8), p => p with { Widows = 0, Orphans = 0 });
        var title = f.Add("Titolo");
        f.Add(Lines(4));

        var layout = f.Layout();

        Assert.Equal(title.Id, layout.Pages[0].Lines[^1].ParagraphId);
    }

    [Fact]
    public void LInterruzioneDiPaginaPrimaApreUnaPaginaNuova()
    {
        var f = new LayoutFixture();
        f.Add("uno");
        var second = f.Add("due", p => p with { PageBreakBefore = true });

        var layout = f.Layout();

        Assert.Equal(2, layout.Pages.Count);
        Assert.Equal(second.Id, layout.Pages[1].Lines[0].ParagraphId);
    }

    [Fact]
    public void LInterruzioneDiColonnaSenzaColonneApreUnaPaginaNuova()
    {
        var f = new LayoutFixture();
        f.Add("uno");
        var second = f.Add("due", p => p with { ColumnBreakBefore = true });

        var layout = f.Layout();

        Assert.Equal(2, layout.Pages.Count);
        Assert.Equal(second.Id, layout.Pages[1].Lines[0].ParagraphId);
    }

    [Fact]
    public void LInterruzioneSulPrimoParagrafoNonCreaPagineVuote()
    {
        var f = new LayoutFixture();
        f.Add("uno", p => p with { PageBreakBefore = true });

        Assert.Single(f.Layout().Pages);
    }

    [Fact]
    public void IlCambioDiStileDiPaginaApreUnaPaginaConLeSueMisure()
    {
        var f = new LayoutFixture();
        f.Document.Styles.Page.Add(f.PageOf("Wide", 300, 200));
        f.Add("uno");
        f.Add("due", p => p with { PageStyleName = "Wide" });

        var layout = f.Layout();

        Assert.Equal(2, layout.Pages.Count);
        Assert.Equal("Wide", layout.Pages[1].StyleName);
        Assert.Equal(300, layout.Pages[1].Width, 3);
    }

    [Fact]
    public void LoStileDiPaginaSeguenteSiApplicaAllePagineDopo()
    {
        var f = new LayoutFixture(width: 60, height: 120);
        f.Document.Styles.Page.Find(LayoutFixture.Page)!.NextStyleName = "Other";
        f.Document.Styles.Page.Add(f.PageOf("Other", 60, 240));
        f.Add(Lines(30), p => p with { Widows = 0, Orphans = 0 });

        var layout = f.Layout();

        Assert.Equal(["Test", "Other"], layout.Pages.Select(page => page.StyleName));
        Assert.Equal(20, layout.Pages[1].Lines.Count);
    }

    [Fact]
    public void LaSpaziaturaSiSommaFraParagrafi()
    {
        var f = new LayoutFixture();
        f.Add("uno", p => p with { SpaceAfter = 5, Explicit = ParagraphOverrides.SpaceAfter });
        f.Add("due", p => p with { SpaceBefore = 7, Explicit = ParagraphOverrides.SpaceBefore });

        var lines = f.Lines(f.Layout());

        Assert.Equal(12 + 5 + 7, lines[1].Top, 3);
    }

    [Fact]
    public void LaSpaziaturaContestualeSiAnnullaFraParagrafiDelloStessoStile()
    {
        var f = new LayoutFixture();
        f.Add("uno", p => p with { SpaceAfter = 5, Explicit = ParagraphOverrides.SpaceAfter });
        var second = f.Add("due", p => p with { SpaceBefore = 7, Explicit = ParagraphOverrides.SpaceBefore });
        f.Settings.Contextual.Add(second.Id);

        Assert.Equal(12, f.Lines(f.Layout())[1].Top, 3);
    }

    [Fact]
    public void LaSpaziaturaDopoNonPassaAllaPaginaSuccessiva()
    {
        var f = new LayoutFixture(width: 60, height: 120);
        f.Add(Lines(10), p => p with { SpaceAfter = 30, Explicit = ParagraphOverrides.SpaceAfter });
        f.Add("due");

        var layout = f.Layout();

        Assert.Equal(0, layout.Pages[1].Lines[0].Top, 3);
    }

    [Fact]
    public void UnDocumentoVuotoHaUnaPagina()
    {
        var f = new LayoutFixture();

        Assert.Single(f.Layout().Pages);
    }
}
