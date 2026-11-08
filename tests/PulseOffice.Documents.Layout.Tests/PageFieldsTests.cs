using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class PageFieldsTests
{
    private ParagraphBlock Paragraph(LayoutFixture f, string text, params TextRun[] runs) =>
        new(f.Document.NextId("h"), runs.Length == 0 ? new TextParagraph(text) { Runs = [new TextRun(text)] } : new TextParagraph(string.Empty).WithRuns(runs));

    private string Text(LineBox line) => string.Concat(line.Line.Runs.Where(run => run.Kind == GlyphRunKind.Text).Select(run => run.Text)).TrimEnd();

    private PageStyle Style(LayoutFixture f) => f.Document.Styles.Page.Find(LayoutFixture.Page)!;

    private string Shape(DocumentLayout layout) => string.Join('|', layout.Pages.Select(page =>
        $"{page.Number}:{page.Start}:" + string.Join(',', page.AllLines.Select(line => $"{Text(line)}@{line.Left:F2},{line.Top:F3}"))));

    [Fact]
    public void ModificareLIntestazioneReimpaginaDaSola()
    {
        var f = new LayoutFixture(width: 200, height: 120);
        var style = Style(f);
        style.Header.Enabled = true;
        style.Header.Spacing = Twip.Zero;
        var header = Paragraph(f, "breve");
        style.Header.Default.Add(header);
        for (var i = 0; i < 20; i++) f.Add("riga");
        var previous = f.Layout();

        header.Content = new TextParagraph(new string('a', 60)) { Runs = [new TextRun(new string('a', 60))] };
        var relaid = f.Engine.Relayout(f.Document, previous);

        Assert.Equal(2, relaid.Pages[0].Header!.Lines.Count());
        Assert.Equal(24, relaid.Pages[0].Lines[0].Top, 3);
        Assert.Equal(Shape(f.Layout()), Shape(relaid));
    }

    [Theory]
    [InlineData(NumberFormat.UpperRoman, "I,II,III,IV", "IV")]
    [InlineData(NumberFormat.LowerRoman, "i,ii,iii,iv", "iv")]
    [InlineData(NumberFormat.UpperLetter, "A,B,C,D", "D")]
    [InlineData(NumberFormat.LowerLetter, "a,b,c,d", "d")]
    [InlineData(NumberFormat.Arabic, "1,2,3,4", "4")]
    public void IlNumeroDiPaginaSegueIlFormatoDelloStileDiPagina(NumberFormat format, string numbers, string count)
    {
        var f = new LayoutFixture(width: 200, height: 48);
        var style = Style(f);
        style.NumberFormat = format;
        style.Footer.Enabled = true;
        style.Footer.Spacing = Twip.Zero;
        style.Footer.Default.Add(Paragraph(f, string.Empty, new TextRun("#") { Field = TextFieldKind.PageNumber }, new TextRun("/"), new TextRun("#") { Field = TextFieldKind.PageCount }));
        for (var page = 0; page < 4; page++)
        {
            f.Document.Body.Add(Paragraph(f, string.Empty, new TextRun("p "), new TextRun("#") { Field = TextFieldKind.PageNumber }));
            f.Add("riga");
            f.Add("riga");
        }

        var layout = f.Layout();
        var expected = numbers.Split(',');

        Assert.Equal(4, layout.Pages.Count);
        Assert.Equal(expected.Select(n => "p " + n), layout.Pages.Select(page => Text(page.Lines[0])));
        Assert.Equal(expected.Select(n => n + "/" + count), layout.Pages.Select(page => Text(page.Footer!.Lines.Single())));
    }

    [Fact]
    public void NumeroEConteggioDellePagineNelCorpoSiRisolvonoPaginaPerPagina()
    {
        var f = new LayoutFixture(width: 200, height: 48);
        for (var page = 0; page < 12; page++)
        {
            f.Document.Body.Add(Paragraph(f, string.Empty, new TextRun("pagina "), new TextRun("#") { Field = TextFieldKind.PageNumber }, new TextRun(" di "), new TextRun("#") { Field = TextFieldKind.PageCount }));
            for (var i = 0; i < 3; i++) f.Add("riga");
        }

        var layout = f.Layout();

        Assert.Equal(12, layout.Pages.Count);
        Assert.Equal(Enumerable.Range(1, 12).Select(n => $"pagina {n} di 12"), layout.Pages.Select(page => Text(page.Lines[0])));
        var edited = f.Document.Body.Blocks.OfType<ParagraphBlock>().Last();
        edited.Content = edited.Content.WithRuns([new TextRun("ultima")]);
        Assert.Equal(Shape(f.Layout()), Shape(f.Engine.Relayout(f.Document, layout)));
    }

    [Fact]
    public void LaRipartenzaDellaNumerazioneCambiaINumeriDiPagina()
    {
        var f = new LayoutFixture(width: 200, height: 48);
        var style = Style(f);
        style.Header.Enabled = true;
        style.Header.Spacing = Twip.Zero;
        style.Header.Default.Add(Paragraph(f, string.Empty, new TextRun("#") { Field = TextFieldKind.PageNumber }));
        for (var i = 0; i < 5; i++) f.Add("riga");
        f.Add("capitolo", p => p with { PageStyleName = LayoutFixture.Page, PageNumberRestart = 7 });
        for (var i = 0; i < 5; i++) f.Add("riga");

        var layout = f.Layout();

        Assert.Equal(["1", "2", "7", "8"], layout.Pages.Select(page => Text(page.Header!.Lines.First())));
        Assert.Equal([1, 2, 7, 8], layout.Pages.Select(page => page.Number));
    }

    [Fact]
    public void LAltezzaDellIntestazioneUsaIlConteggioFinaleDellePagine()
    {
        var f = new LayoutFixture(width: 60, height: 60);
        var style = Style(f);
        style.Header.Enabled = true;
        style.Header.Default.Add(Paragraph(f, string.Empty, new TextRun("aaaaaaaa "), new TextRun("#") { Field = TextFieldKind.PageCount }));
        for (var i = 0; i < 30; i++) f.Add("riga");

        var layout = f.Layout();

        Assert.True(layout.Pages.Count >= 10);
        Assert.All(layout.Pages, page => Assert.Equal(2, page.Header!.Lines.Count()));
        Assert.All(layout.Pages, page => Assert.True(page.Lines[0].Top >= page.Header!.Bounds.Bottom - 0.001));
        Assert.Equal(layout.Pages.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), Text(layout.Pages[0].Header!.Lines.ElementAt(1)));
    }
}
