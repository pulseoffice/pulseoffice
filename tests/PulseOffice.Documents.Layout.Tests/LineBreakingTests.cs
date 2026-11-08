using PulseOffice.Documents.Layout.Lines;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class LineBreakingTests
{
    [Fact]
    public void SiVaACapoSugliSpazi()
    {
        var f = new LayoutFixture(width: 60);
        f.Add("aaaa bbbb cccc");

        Assert.Equal(["aaaa bbbb", "cccc"], f.LineTexts(f.Layout()));
    }

    [Fact]
    public void GliSpaziInFondoNonFannoAndareACapo()
    {
        var f = new LayoutFixture(width: 54);
        f.Add("aaaa bbbb     cccc");

        var lines = f.Lines(f.Layout());

        Assert.Equal(2, lines.Count);
        Assert.Equal(14, lines[1].Line.Start);
    }

    [Fact]
    public void UnaParolaTroppoLungaSiSpezzaPerCaratteriComeWriter()
    {
        var f = new LayoutFixture(width: 60);
        f.Add("abcdefghijklmnop");

        Assert.Equal(["abcdefghij", "klmnop"], f.LineTexts(f.Layout()));
    }

    [Fact]
    public void SiVaACapoDopoIlTrattino()
    {
        var f = new LayoutFixture(width: 48);
        f.Add("well-known");

        Assert.Equal(["well-", "known"], f.LineTexts(f.Layout()));
    }

    [Fact]
    public void IlTrattinoFacoltativoDiventaVisibileSoloAlTaglio()
    {
        var f = new LayoutFixture(width: 40);
        var block = f.Add("aaaa­bbbb");

        var lines = f.Lines(f.Layout());

        Assert.Equal(2, lines.Count);
        Assert.Equal(GlyphRunKind.Hyphen, lines[0].Line.Runs[^1].Kind);
        Assert.Equal(5, lines[1].Line.Start);
        Assert.Equal(block.Id, lines[1].ParagraphId);
    }

    [Fact]
    public void LaSillabazioneUsaIPuntiDelDizionario()
    {
        var f = new LayoutFixture(width: 50);
        f.Hyphenation.Words["meraviglioso"] = [2, 4, 6, 9];
        var block = f.Add("meraviglioso");
        f.Settings.Hyphenated.Add(block.Id);

        Assert.Equal(["meravi-", "glioso"], f.LineTexts(f.Layout()));
    }

    [Fact]
    public void SenzaSillabazioneLaParolaSiSpezzaPerCaratteri()
    {
        var f = new LayoutFixture(width: 50);
        f.Hyphenation.Words["meraviglioso"] = [2, 4, 6, 9];
        f.Add("meraviglioso");

        Assert.Equal(["meravigl", "ioso"], f.LineTexts(f.Layout()));
    }

    [Fact]
    public void IlRitornoACapoForzatoChiudeLaRiga()
    {
        var f = new LayoutFixture(width: 200);
        f.Add("uno\ndue\n");

        var lines = f.Lines(f.Layout());

        Assert.Equal(3, lines.Count);
        Assert.True(lines[0].Line.Forced);
        Assert.Equal(8, lines[2].Line.Start);
    }

    [Fact]
    public void UnParagrafoVuotoHaUnaRiga()
    {
        var f = new LayoutFixture();
        f.Add(string.Empty);

        var line = Assert.Single(f.Lines(f.Layout()));
        Assert.Equal(12, line.Line.Height, 3);
    }

    [Fact]
    public void IRientriRiduconoLaLarghezzaELaPrimaRigaRientra()
    {
        var f = new LayoutFixture(width: 100);
        f.Add("aaaa bbbb cccc dddd", p => p with { MarginLeft = 10, MarginRight = 10, Indent = 20 });

        var lines = f.Lines(f.Layout());

        Assert.Equal(["aaaa bbbb", "cccc dddd"], f.LineTexts(f.Layout()));
        Assert.Equal(30, lines[0].Line.Runs[0].X, 3);
        Assert.Equal(10, lines[1].Line.Runs[0].X, 3);
    }

    [Fact]
    public void CentratoEADestraSpostanoLaRiga()
    {
        var f = new LayoutFixture(width: 100);
        f.Add("abc", p => p with { Align = TextAlign.Center, Explicit = ParagraphOverrides.Align });
        f.Add("abc", p => p with { Align = TextAlign.Right, Explicit = ParagraphOverrides.Align });

        var lines = f.Lines(f.Layout());

        Assert.Equal(41, lines[0].Line.Runs[0].X, 3);
        Assert.Equal(82, lines[1].Line.Runs[0].X, 3);
    }

    [Fact]
    public void IlGiustificatoRiempieLeRigheTranneLUltima()
    {
        var f = new LayoutFixture(width: 60);
        f.Add("aa bb cc dd ee ff", p => p with { Align = TextAlign.Justify, Explicit = ParagraphOverrides.Align });

        var lines = f.Lines(f.Layout());
        var first = lines[0].Line;
        var content = first.Runs.Where(run => run.Kind == GlyphRunKind.Text).ToList();
        var right = content[^1].X + content[^1].Advances.Take(content[^1].Text.TrimEnd().Length).Sum();

        Assert.Equal(60, right, 3);
        var last = lines[^1].Line.Runs[0];
        Assert.Equal(last.Text.Length * 6 - (last.Text.Count(c => c == ' ') * 3), last.Advances.Sum(), 3);
    }

    [Fact]
    public void ICaratteriDiversiSullaStessaRigaPrendonoIlPiuAlto()
    {
        var f = new LayoutFixture(width: 200);
        f.AddRuns(new TextRun("piccolo ") { FontSize = 10 }, new TextRun("grande") { FontSize = 20 });

        var line = Assert.Single(f.Lines(f.Layout())).Line;

        Assert.Equal(16, line.Ascent, 3);
        Assert.Equal(20, line.Height, 3);
    }

    [Fact]
    public void ApiceRimpicciolisceEAlzaIlTesto()
    {
        var f = new LayoutFixture(width: 200);
        f.AddRuns(new TextRun("x"), new TextRun("2") { Baseline = TextBaseline.Superscript });

        var line = Assert.Single(f.Lines(f.Layout())).Line;
        var sup = line.Runs[1];

        Assert.Equal(12 * 0.58, sup.Font.Size, 3);
        Assert.Equal(12 * 0.33, sup.BaselineShift, 3);
        Assert.Equal(9.6, line.Ascent, 3);
    }

    [Theory]
    [InlineData("proportional", 18)]
    [InlineData("exact", 20)]
    [InlineData("least", 30)]
    [InlineData("leading", 16)]
    public void LInterlineaSegueLaRegola(string kind, double expected)
    {
        var f = new LayoutFixture(width: 200);
        var block = f.Add("riga");
        f.Settings.Spacing[block.Id] = kind switch
        {
            "proportional" => new ProportionalSpacing(1.5),
            "exact" => new ExactSpacing(20),
            "least" => new AtLeastSpacing(30),
            _ => new LeadingSpacing(4),
        };

        Assert.Equal(expected, Assert.Single(f.Lines(f.Layout())).Line.Height, 3);
    }

    [Fact]
    public void LInterlineaProporzionaleDelModelloSiApplica()
    {
        var f = new LayoutFixture(width: 200);
        f.Add("riga", p => p with { LineSpacing = 2, Explicit = ParagraphOverrides.LineSpacing });

        Assert.Equal(24, Assert.Single(f.Lines(f.Layout())).Line.Height, 3);
    }

    [Fact]
    public void LeEtichetteDegliElenchiNumeratiContano()
    {
        var f = new LayoutFixture(width: 200);
        f.Add("primo", p => p with { Numbered = true });
        f.Add("secondo", p => p with { Numbered = true });

        var lines = f.Lines(f.Layout());

        Assert.Equal("1.", lines[0].Line.Runs[0].Text);
        Assert.Equal(GlyphRunKind.Label, lines[1].Line.Runs[0].Kind);
        Assert.Equal("2.", lines[1].Line.Runs[0].Text);
        Assert.Equal(18, lines[1].Line.Runs[1].X, 3);
    }

    [Fact]
    public void GliStiliDiElencoNumeranoConIlLoroFormato()
    {
        var f = new LayoutFixture(width: 200);
        f.Document.Styles.List.Add(new Styles.ListStyle("L", [new Styles.ListLevel(Styles.NumberFormat.UpperRoman) { Suffix = ")" }]));
        f.Add("a", p => p with { ListStyleName = "L" });
        f.Add("b", p => p with { ListStyleName = "L" });

        var lines = f.Lines(f.Layout());

        Assert.Equal("II)", lines[1].Line.Runs[0].Text);
    }
}
