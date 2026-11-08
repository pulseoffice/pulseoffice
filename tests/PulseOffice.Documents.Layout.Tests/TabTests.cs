using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class TabTests
{
    private GlyphRun After(LayoutFixture f, int index) =>
        f.Lines(f.Layout())[0].Line.Runs.Where(run => run.Kind != GlyphRunKind.Tab).ElementAt(index);

    [Fact]
    public void LaTabulazionePredefinitaVaAlMultiploSuccessivo()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("a\tb");

        Assert.Equal(1.25 * 72 / 2.54, After(f, 1).X, 3);
    }

    [Fact]
    public void LaTabulazioneADestraAllineaLaFine()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("x\t123", p => p with { TabStops = [new TabStop(100) { Alignment = TabAlignment.Right, Leader = TabLeader.Dots }] });

        var run = After(f, 1);
        var tab = f.Lines(f.Layout())[0].Line.Runs.Single(r => r.Kind == GlyphRunKind.Tab);

        Assert.Equal(100, run.X + run.Width, 3);
        Assert.Equal(TabLeader.Dots, tab.Leader);
    }

    [Fact]
    public void UnaTabulazioneADestraOltreIlMargineSiFermaAlMargineSenzaAndareACapo()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("x\t123", p => p with { TabStops = [new TabStop(400) { Alignment = TabAlignment.Right, Leader = TabLeader.Dots }] });

        var lines = f.Lines(f.Layout());
        Assert.Single(lines);
        var run = After(f, 1);
        Assert.Equal(300, run.X + run.Width, 3);
    }

    [Fact]
    public void LaTabulazioneCentrataCentraIlTesto()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("x\tabcd", p => p with { TabStops = [new TabStop(100) { Alignment = TabAlignment.Center }] });

        var run = After(f, 1);

        Assert.Equal(100, run.X + run.Width / 2, 3);
    }

    [Fact]
    public void LaTabulazioneDecimaleAllineaIlSeparatore()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("x\t12.50", p => p with { TabStops = [new TabStop(100) { Alignment = TabAlignment.Decimal }] });

        var run = After(f, 1);

        Assert.Equal(100, run.X + 12, 3);
    }

    [Fact]
    public void LeTabulazioniSonoRelativeAlRientro()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("x\ty", p => p with { MarginLeft = 20, TabStops = [new TabStop(50)] });

        Assert.Equal(70, After(f, 1).X, 3);
    }

    [Fact]
    public void ConLeTabulazioniDalMargineIlRientroNonLeSposta()
    {
        var f = new LayoutFixture(width: 300);
        f.Document.Styles.TabsRelativeToIndent = false;
        f.Add("x\ty", p => p with { MarginLeft = 20, TabStops = [new TabStop(50)] });

        Assert.Equal(50, After(f, 1).X, 3);
    }

    [Fact]
    public void ConLeTabulazioniDalMargineAncheQuellePredefiniteSiMisuranoDalMargine()
    {
        var f = new LayoutFixture(width: 300);
        f.Document.Styles.TabsRelativeToIndent = false;
        f.Document.Styles.DefaultTabInterval = 50;
        f.Add("a\tb", p => p with { MarginLeft = 20 });

        Assert.Equal(50, After(f, 1).X, 3);
    }

    [Fact]
    public void LaDistanzaPredefinitaDelleTabulazioniVieneDalDocumento()
    {
        var f = new LayoutFixture(width: 300);
        f.Document.Styles.DefaultTabInterval = 50;
        f.Add("a\tb\tc");

        Assert.Equal(50, After(f, 1).X, 3);
        Assert.Equal(100, After(f, 2).X, 3);
    }

    [Fact]
    public void UnEtichettaPiuLargaDelSuoSpazioSaltaAllaTabulazionePredefinitaSuccessiva()
    {
        var f = new LayoutFixture(width: 300);
        f.Document.Styles.DefaultTabInterval = 40;
        f.Document.Styles.List.Add(new Styles.ListStyle("L", [new Styles.ListLevel(Styles.NumberFormat.Arabic) { Prefix = ">>", Suffix = "<<" }]));
        f.Add("x", p => p with { ListStyleName = "L" });

        var line = f.Lines(f.Layout())[0].Line;
        Assert.Equal(GlyphRunKind.Label, line.Runs[0].Kind);
        Assert.Equal(18 + 40, line.Runs.First(run => run.Kind != GlyphRunKind.Label).X, 3);
    }
}
