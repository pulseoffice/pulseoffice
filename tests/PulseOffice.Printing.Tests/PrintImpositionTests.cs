using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class PrintImpositionTests
{
    private sealed class Numbered(int pages) : IPrintableDocument
    {
        public List<int> Painted { get; } = [];

        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => pages;

        public void PaintPage(int index, IPageCanvas canvas)
        {
            Painted.Add(index);
            canvas.DrawText((index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(50), Twip.FromPoints(20)), PrintFont.Default, PrintColor.Black);
        }
    }

    private sealed class Recorder : IPageCanvas
    {
        public DocumentSize PageSize { get; } = new(Twip.FromPoints(800), Twip.FromPoints(800));

        public List<string> Texts { get; } = [];

        public List<PrintColor> Colors { get; } = [];

        public int Images { get; private set; }

        public Twip MeasureText(string text, PrintFont font) => Twip.Zero;

        public void FillRectangle(DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle)
        {
            Texts.Add(text);
            Colors.Add(color);
        }

        public void DrawPath(PrintPath path)
        {
        }

        public void DrawImage(PageImage image, DocumentRect bounds) => Images++;
    }

    private readonly PrintImposition _imposition = new(new PageRangeParser());

    private List<string> Sheets(IPrintableDocument document) =>
        [.. Enumerable.Range(0, document.PageCount).Select(index =>
        {
            var recorder = new Recorder();
            document.PaintPage(index, recorder);
            return string.Join(",", recorder.Texts);
        })];

    [Theory]
    [InlineData("1-3,5", 6, "0,1,2,4")]
    [InlineData("4-", 6, "3,4,5")]
    [InlineData("-2", 6, "0,1")]
    [InlineData("5-3", 6, "4,3,2")]
    [InlineData("", 3, "0,1,2")]
    public void LIntervalloSiLeggeComeInWriter(string range, int count, string expected)
    {
        Assert.Equal(expected, string.Join(",", new PageRangeParser().Parse(range, count)!));
    }

    [Fact]
    public void UnIntervalloSbagliatoNonSiLegge()
    {
        Assert.Null(new PageRangeParser().Parse("1-x", 5));
    }

    [Fact]
    public void PariDispariEOrdineInverso()
    {
        var odd = _imposition.Impose(new Numbered(6), new PrintJobOptions { Subset = PageSubset.Odd, Reverse = true }, nativeCopies: true);

        Assert.Equal(["5", "3", "1"], Sheets(odd));
    }

    [Fact]
    public void Copies_beyond_the_dialog_limit_are_capped_instead_of_exhausting_memory()
    {
        var imposed = _imposition.Impose(new Numbered(2), new PrintJobOptions { Copies = int.MaxValue }, nativeCopies: false);

        Assert.Equal(2 * 999, imposed.PageCount);
    }

    [Fact]
    public void QuattroPagineSuUnFoglio()
    {
        var sheets = Sheets(_imposition.Impose(new Numbered(6), new PrintJobOptions { PagesPerSheet = 4 }, nativeCopies: true));

        Assert.Equal(["1,2,3,4", "5,6"], sheets);
    }

    [Fact]
    public void DuePaginePerFoglioGiranoIlFoglioInOrizzontale()
    {
        var imposed = _imposition.Impose(new Numbered(2), new PrintJobOptions { PagesPerSheet = 2 }, nativeCopies: true);

        Assert.True(imposed.Setup.PageWidth.Value > imposed.Setup.PageHeight.Value);
        Assert.Single(Sheets(imposed));
    }

    [Fact]
    public void LOpuscoloPiegaLePagineNellOrdineGiusto()
    {
        var sheets = Sheets(_imposition.Impose(new Numbered(8), new PrintJobOptions { Brochure = true }, nativeCopies: true));

        Assert.Equal(["8,1", "2,7", "6,3", "4,5"], sheets);
    }

    [Fact]
    public void LOpuscoloCompletaConPagineBianche()
    {
        var sheets = Sheets(_imposition.Impose(new Numbered(5), new PrintJobOptions { Brochure = true }, nativeCopies: true));

        Assert.Equal(["1", "2", "3", "4,5"], sheets);
    }

    [Fact]
    public void LeCopieSenzaSupportoNativoSiRipetonoFascicolateONo()
    {
        var collated = Sheets(_imposition.Impose(new Numbered(2), new PrintJobOptions { Copies = 2, Collate = true }, nativeCopies: false));
        var grouped = Sheets(_imposition.Impose(new Numbered(2), new PrintJobOptions { Copies = 2, Collate = false }, nativeCopies: false));
        var native = Sheets(_imposition.Impose(new Numbered(2), new PrintJobOptions { Copies = 2 }, nativeCopies: true));

        Assert.Equal(["1", "2", "1", "2"], collated);
        Assert.Equal(["1", "1", "2", "2"], grouped);
        Assert.Equal(["1", "2"], native);
    }

    [Fact]
    public void SenzaGraficaNienteImmaginiETestoInNero()
    {
        var recorder = new Recorder();
        var canvas = new FilteredPageCanvas(recorder, graphics: false, blackText: true);

        canvas.DrawImage(new PageImage(1, 1, [1, 2, 3]), new DocumentRect(Twip.Zero, Twip.Zero, new Twip(20), new Twip(20)));
        canvas.DrawText("x", new DocumentRect(Twip.Zero, Twip.Zero, new Twip(20), new Twip(20)), PrintFont.Default, new PrintColor(200, 0, 0));

        Assert.Equal(0, recorder.Images);
        Assert.Equal(PrintColor.Black, recorder.Colors.Single());
    }
}
