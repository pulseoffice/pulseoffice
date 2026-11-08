using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class CharacterEffectsTests
{
    private IReadOnlyList<GlyphRun> Runs(LayoutFixture fixture, string paragraphId) =>
        [.. fixture.Lines(fixture.Layout()).Where(line => line.ParagraphId == paragraphId).SelectMany(line => line.Line.Runs).Where(run => run.Kind == GlyphRunKind.Text)];

    [Fact]
    public void LaSpaziaturaSiAggiungeDopoOgniCarattere()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.AddRuns(new TextRun("abc") { LetterSpacing = 2, FontSize = 10 });

        var run = Assert.Single(Runs(f, block.Id));

        Assert.Equal([7, 7, 7], run.Advances);
    }

    [Fact]
    public void LaScalaDellaLarghezzaRestringeGliAvanzamenti()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.AddRuns(new TextRun("ab") { ScaleWidth = 50, FontSize = 10 });

        Assert.Equal([2.5, 2.5], Assert.Single(Runs(f, block.Id)).Advances);
    }

    [Fact]
    public void LeMaiuscoleCambianoIlTestoDisegnatoMaNonLaLunghezza()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var upper = f.AddRuns(new TextRun("ciao mondo") { Case = TextCase.Upper });
        var title = f.AddRuns(new TextRun("ciao l'amico") { Case = TextCase.Capitalize });
        var lower = f.AddRuns(new TextRun("CIAO") { Case = TextCase.Lower });

        Assert.Equal(["CIAO MONDO"], f.LineTexts(f.Layout(), upper.Id));
        Assert.Equal(["Ciao L'amico"], f.LineTexts(f.Layout(), title.Id));
        Assert.Equal(["ciao"], f.LineTexts(f.Layout(), lower.Id));
    }

    [Fact]
    public void IMaiuscolettiRimpicciolisconoSoloLeMinuscole()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.AddRuns(new TextRun("Ab") { SmallCaps = true, FontSize = 10 });

        var runs = Runs(f, block.Id);

        Assert.Equal(["A", "B"], runs.Select(run => run.Text));
        Assert.Equal(10, runs[0].Font.Size, 3);
        Assert.Equal(8, runs[1].Font.Size, 3);
    }

    [Fact]
    public void IlTestoCjkUsaIlFontAsiaticoDelRun()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.AddRuns(new TextRun("ab 漢字 cd") { Font = "Inter", AsianFont = "Noto Sans CJK JP", FontSize = 10 });

        var runs = Runs(f, block.Id);

        Assert.Equal(["ab ", "漢字 ", "cd"], runs.Select(run => run.Text));
        Assert.Equal(["Inter", "Noto Sans CJK JP", "Inter"], runs.Select(run => run.Font.Family));
    }

    [Fact]
    public void IlTestoNascostoNonOccupaSpazio()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.AddRuns(new TextRun("vis"), new TextRun("nascosto") { Hidden = true }, new TextRun("ibile"));

        var runs = Runs(f, block.Id);

        Assert.Equal(0, runs.Single(run => run.Text == "nascosto").Width);
        Assert.Equal(runs[0].Width, runs[2].X - runs[0].X, 3);
    }

    [Fact]
    public void LaCrenaturaSpentaArrivaAlMisuratore()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.AddRuns(new TextRun("AV") { Kerning = false });

        Assert.False(Assert.Single(Runs(f, block.Id)).Font.Kerning);
    }
}

public sealed class ParagraphDecorationTests
{
    [Fact]
    public void IlCapoletteraScendeSulleRigheERientraLeRigheAccanto()
    {
        var f = new LayoutFixture(width: 120, height: 400);
        var block = f.Add("Cane gatto topo lupo orso volpe riccio talpa", p => p with { DropCap = new DropCap(2, 1) { Distance = 2 } });

        var lines = f.Lines(f.Layout()).Where(line => line.ParagraphId == block.Id).ToList();
        var drop = lines[0].Line.Runs.First(run => run.Kind == Tree.GlyphRunKind.Text);
        var first = lines[0].Line.Runs.Where(run => run.Kind == Tree.GlyphRunKind.Text).ElementAt(1);
        var second = lines[1].Line.Runs.First(run => run.Kind == Tree.GlyphRunKind.Text);
        var third = lines[2].Line.Runs.First(run => run.Kind == Tree.GlyphRunKind.Text);

        Assert.Equal("C", drop.Text);
        Assert.True(drop.Font.Size > 12);
        Assert.True(drop.BaselineShift < 0);
        Assert.Equal(drop.Width + 2, first.X, 3);
        Assert.Equal(drop.Width + 2, second.X, 3);
        Assert.Equal(0, third.X, 3);
        Assert.StartsWith("ane", first.Text);
    }

    [Fact]
    public void UnParagrafoCortoConCapoletteraOccupaTutteLeRigheDelCapolettera()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("Breve", p => p with { DropCap = new DropCap(3, 1) });

        var line = Assert.Single(f.Lines(f.Layout()), l => l.ParagraphId == block.Id);

        Assert.True(line.Line.Height > 2.5 * 12);
        Assert.True(line.Line.CaretHeight < 1.5 * 12);
    }

    [Fact]
    public void IlCursoreSullUltimaRigaDiUnParagrafoConCapoletteraHaLAltezzaDellaRiga()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("Breve", p => p with { DropCap = new DropCap(3, 1) });
        var layout = f.Layout();
        var line = Assert.Single(f.Lines(layout), l => l.ParagraphId == block.Id);

        var caret = new Queries.LayoutQueries(new Queries.LineOffsets(new PulseOffice.Text.Rich.TextClusters())).Caret(layout, new PulseOffice.Documents.Editing.DocumentPosition(block.Id, 5))!.Value;

        Assert.Equal(line.Line.CaretHeight, caret.Height, 3);
        Assert.True(caret.Height < line.Line.Height);
    }

    [Fact]
    public void IBordiPerLatoSiDisegnanoSoloDoveCiSono()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        var block = f.Add("riga", p => p with { Borders = new BoxBorders { Bottom = new BorderLine(2, 0xFF000000) { Style = BorderLineStyle.Double }, PaddingBottom = 3, PaddingLeft = 5 } });

        var page = f.Layout().Pages[0];
        var border = Assert.Single(page.Decorations, box => box.Kind == Tree.DecorationKind.ParagraphBorder && box.Owner == block.Id);

        Assert.Equal(BorderLineStyle.Double, border.LineStyle);
        Assert.Equal(2, border.Bounds.Height, 3);
        Assert.Equal(5, page.Lines[0].Line.Runs[0].X, 3);
    }
}

public sealed class ListNumberingTests
{
    private string Label(LayoutFixture fixture, Blocks.ParagraphBlock block) =>
        fixture.Lines(fixture.Layout()).First(line => line.ParagraphId == block.Id).Line.Runs.FirstOrDefault(run => run.Kind == Tree.GlyphRunKind.Label)?.Text ?? string.Empty;

    [Fact]
    public void LaRipartenzaELeVociSenzaNumeroSeguonoWriter()
    {
        var f = new LayoutFixture(width: 300, height: 400);
        Blocks.ParagraphBlock Item(string text, int? restart = null, bool header = false) =>
            f.Add(text, p => p with { ListStyleName = Styles.DefaultStyleNames.Numbering123, Numbered = !header, ListHeader = header, ListRestart = restart });
        f.Document.Styles.List.Add(new Styles.ListStyle(Styles.DefaultStyleNames.Numbering123, [new Styles.ListLevel(Styles.NumberFormat.Arabic)]));
        var first = Item("uno");
        var second = Item("due");
        var restarted = Item("cinque", 5);
        var header = Item("nota", header: true);
        var next = Item("sei");

        Assert.Equal("1.", Label(f, first));
        Assert.Equal("2.", Label(f, second));
        Assert.Equal("5.", Label(f, restarted));
        Assert.Equal(string.Empty, Label(f, header));
        Assert.Equal("6.", Label(f, next));
    }

    [Fact]
    public void LaNumerazioneDeiCapitoliNumeraITitoli()
    {
        var f = new LayoutFixture(width: 300, height: 400);
        f.Document.Styles.Outline = new Styles.ListStyle(Styles.StyleSheet.OutlineStyleName,
            [.. Enumerable.Range(0, Styles.ListStyle.LevelCount).Select(level => new Styles.ListLevel(level < 2 ? Styles.NumberFormat.Arabic : Styles.NumberFormat.None) { Suffix = string.Empty, DisplayLevels = level + 1 })]);
        var one = f.Add("Uno", p => p with { OutlineLevel = 1 });
        var oneOne = f.Add("Uno uno", p => p with { OutlineLevel = 2 });
        var oneTwo = f.Add("Uno due", p => p with { OutlineLevel = 2 });
        var two = f.Add("Due", p => p with { OutlineLevel = 1 });
        var twoOne = f.Add("Due uno", p => p with { OutlineLevel = 2 });

        Assert.Equal(["1", "1.1", "1.2", "2", "2.1"], new[] { one, oneOne, oneTwo, two, twoOne }.Select(block => Label(f, block)));
    }
}

public sealed class PageDecorationTests
{
    [Fact]
    public void IBordiDellaPaginaRientranoIlTestoEIlFondoSiDisegna()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        var page = f.Document.Styles.Page.Find(LayoutFixture.Page)!;
        page.Margins = new Foundation.Units.Margins(Foundation.Units.Twip.FromPoints(20), Foundation.Units.Twip.FromPoints(20), Foundation.Units.Twip.FromPoints(20), Foundation.Units.Twip.FromPoints(20));
        page.Borders = BoxBorders.Uniform(new BorderLine(2, 0xFF000000), 3);
        page.Background = 0xFFEEEEEE;
        page.Watermark = new Styles.PageWatermark("BOZZA");
        f.Add("riga");

        var box = f.Layout().Pages[0];

        Assert.Equal(25, box.Lines[0].Top, 3);
        Assert.Equal(25, box.Lines[0].Left, 3);
        Assert.Contains(box.Backdrop, item => item is Tree.DecorationBox { Kind: Tree.DecorationKind.PageBackground });
        Assert.Equal(4, box.Backdrop.Count(item => item is Tree.DecorationBox { Kind: Tree.DecorationKind.PageBorder }));
        var watermark = Assert.Single(box.Backdrop.OfType<Tree.WatermarkBox>());
        Assert.Equal(45, watermark.Angle);
        Assert.Equal("BOZZA", watermark.Run.Text);
        Assert.True(watermark.Color != 0xFFC0C0C0);
    }
}
