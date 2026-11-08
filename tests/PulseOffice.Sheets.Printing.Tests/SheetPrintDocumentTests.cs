using PulseOffice.Foundation.Pages;
using System.Globalization;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Printing;
using PulseOffice.Sheets.Values;
using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Sheets.Printing.Tests;

public class SheetPrintDocumentTests
{
    private sealed class Recorder : IPageCanvas
    {
        public List<string> Texts { get; } = [];

        public List<DocumentRect> Fills { get; } = [];

        public List<PrintPath> Paths { get; } = [];

        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints((text?.Length ?? 0) * 5.0);

        public void FillRectangle(DocumentRect rect, PrintColor color) => Fills.Add(rect);

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) { }

        public void DrawText(
            string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) =>
            Texts.Add(text);

        public void DrawPath(PrintPath path) => Paths.Add(path);

        public void DrawImage(PageImage image, DocumentRect bounds) { }
    }

    private static readonly IPaperCatalog Papers = new PaperCatalog();

    private static ICellPresenter Presenter()
    {
        var formatter = new NumberFormatter(
            CultureInfo.InvariantCulture,
            serial => new DateOnly(1899, 12, 30).ToDateTime(TimeOnly.MinValue).AddDays(serial));

        return new CellPresenter(formatter);
    }

    private static Worksheet SheetWith(int rows, int columns)
    {
        var sheet = new Worksheet("Foglio1");

        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
                sheet.SetValue(new CellAddress(row, column), CellValue.Number(row * 100 + column));

        return sheet;
    }

    private static SheetPrintDocument Build(Worksheet sheet, PageSetup setup, SheetPrintOptions? options = null) =>
        new(sheet, new Styling.StyleCatalog(), Presenter(), new HeaderFooterFields(CultureInfo.InvariantCulture),
            options ?? new SheetPrintOptions { Setup = setup });

    private static PageSetup Standard() => new() { Paper = Papers.Default };

    private static Worksheet SheetWithShape(ShapeObject shape)
    {
        var sheet = SheetWith(5, 3);

        sheet.Objects.Add(new PlacedObject
        {
            Id = shape.Id,
            Content = shape,
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
        });

        return sheet;
    }

    [Fact]
    public void UnaFormaVieneDisegnataNellaPagina()
    {
        var document = Build(
            SheetWithShape(new ShapeObject { Id = "f1", Shape = ShapeKind.Ellipse }), Standard());

        var canvas = new Recorder();
        document.PaintPage(0, canvas);

        var path = Assert.Single(canvas.Paths);

        Assert.NotNull(path.Fill);
        Assert.Equal(3, path.Bounds.Width.ToCentimeters(), 2);
    }

    [Fact]
    public void UnaFormaSenzaRiempimentoNonLoDichiara()
    {
        var document = Build(SheetWithShape(new ShapeObject
        {
            Id = "f1",
            Shape = ShapeKind.Line,
            Style = ShapeStyle.Default with { Fill = ShapeFill.None },
        }), Standard());

        var canvas = new Recorder();
        document.PaintPage(0, canvas);

        Assert.Null(Assert.Single(canvas.Paths).Fill);
    }

    [Fact]
    public void LaRotazioneArrivaAlDisegno()
    {
        var document = Build(SheetWithShape(new ShapeObject
        {
            Id = "f1",
            Shape = ShapeKind.Rectangle,
            RotationDegrees = 30,
        }), Standard());

        var canvas = new Recorder();
        document.PaintPage(0, canvas);

        Assert.Equal(30, Assert.Single(canvas.Paths).RotationDegrees, 3);
    }

    [Fact]
    public void UnOggettoAncoratoFuoriDallaPaginaNonSiDisegna()
    {
        var sheet = SheetWith(200, 3);

        sheet.Objects.Add(new PlacedObject
        {
            Id = "f1",
            Content = new ShapeObject { Id = "f1", Shape = ShapeKind.Rectangle },
            Anchor = new CellAddress(190, 1),
            Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
        });

        var document = Build(sheet, Standard());

        var canvas = new Recorder();
        document.PaintPage(0, canvas);

        Assert.Empty(canvas.Paths);
    }

    [Fact]
    public void UnFoglioVuotoNonHaPagine()
    {
        Assert.Equal(0, Build(new Worksheet("Foglio1"), Standard()).PageCount);
    }

    [Fact]
    public void UnFoglioPiccoloStaInUnaPagina()
    {
        Assert.Equal(1, Build(SheetWith(10, 3), Standard()).PageCount);
    }

    [Fact]
    public void UnFoglioLungoSiSpezzaInPiuPagine()
    {
        var document = Build(SheetWith(200, 3), Standard());

        Assert.True(document.PageCount > 1);
        Assert.All(document.Pages, page => Assert.Equal(0, page.Columns.First));
    }

    [Fact]
    public void UnFoglioLargoSiSpezzaAnchePerColonne()
    {
        var document = Build(SheetWith(5, 40), Standard());

        Assert.True(document.Pages.Select(page => page.Columns).Distinct().Count() > 1);
    }

    [Fact]
    public void LOrizzontaleFaEntrarePiuColonne()
    {
        var portrait = Build(SheetWith(5, 40), Standard());
        var landscape = Build(SheetWith(5, 40), Standard() with { Orientation = PageOrientation.Landscape });

        Assert.True(landscape.Pages[0].Columns.Count > portrait.Pages[0].Columns.Count);
    }

    [Fact]
    public void LOrdineDellePagineSegueLImpostazione()
    {
        var sheet = SheetWith(200, 40);

        var down = Build(sheet, Standard());
        var across = Build(sheet, Standard() with { Order = PageOrder.AcrossThenDown });

        Assert.Equal(down.Pages[0].Columns, down.Pages[1].Columns);
        Assert.True(down.Pages[1].Rows.First > down.Pages[0].Rows.First);

        Assert.Equal(across.Pages[0].Rows, across.Pages[1].Rows);
        Assert.True(across.Pages[1].Columns.First > across.Pages[0].Columns.First);
    }

    [Fact]
    public void LeRigheDaRipetereTornanoSuOgniPagina()
    {
        var sheet = SheetWith(200, 3);
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Intestazione"));

        var document = Build(sheet, Standard(), new SheetPrintOptions
        {
            Setup = Standard(),
            RepeatedRows = 1,
        });

        var canvas = new Recorder();
        document.PaintPage(1, canvas);

        Assert.Contains("Intestazione", canvas.Texts);
    }

    [Fact]
    public void IlPiedeSostituisceINumeriDiPagina()
    {
        var setup = Standard() with { Footer = new HeaderFooter(Center: "Pagina &P di &N") };
        var document = Build(SheetWith(200, 3), setup);

        var canvas = new Recorder();
        document.PaintPage(0, canvas);

        Assert.Contains($"Pagina 1 di {document.PageCount}", canvas.Texts);
    }

    [Fact]
    public void LeIntestazioniDiRigaEColonnaSiStampanoSoloSeRichieste()
    {
        var without = new Recorder();
        Build(SheetWith(3, 3), Standard()).PaintPage(0, without);

        var with = new Recorder();
        Build(SheetWith(3, 3), Standard() with { PrintHeadings = true }).PaintPage(0, with);

        Assert.DoesNotContain("A", without.Texts);
        Assert.Contains("A", with.Texts);
        Assert.Contains("1", with.Texts);
    }

    [Fact]
    public void UnAreaDiStampaLimitaCioCheEsce()
    {
        var sheet = SheetWith(50, 5);

        var document = Build(sheet, Standard(), new SheetPrintOptions
        {
            Setup = Standard(),
            Area = new CellRange(new CellAddress(0, 0), new CellAddress(1, 0)),
        });

        var canvas = new Recorder();
        document.PaintPage(0, canvas);

        Assert.Equal(2, canvas.Texts.Count);
    }

    [Fact]
    public void IlTestoLungoInvadeLeCelleVuoteAccanto()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(CellAddress.First, CellValue.Text(new string('x', 200)));

        var canvas = new Recorder();
        Build(sheet, Standard()).PaintPage(0, canvas);

        Assert.Single(canvas.Texts);
        Assert.Equal(200, canvas.Texts[0].Length);
    }

    [Fact]
    public void LeRigheNascosteNonSiStampano()
    {
        var sheet = SheetWith(5, 1);
        sheet.Layout.SetRowHeight(2, Twip.Zero);

        var canvas = new Recorder();
        Build(sheet, Standard()).PaintPage(0, canvas);

        Assert.Equal(4, canvas.Texts.Count);
    }

    [Fact]
    public void UnaRegioneUnitaConLaPrimaRigaNascostaSiStampaLoStesso()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Titolo"));
        sheet.Merges.Add(new CellRange(new CellAddress(0, 0), new CellAddress(2, 1)));
        sheet.Layout.HideRow(0);

        var recorder = new Recorder();
        Build(sheet, Standard(), new SheetPrintOptions
        {
            Setup = Standard(),
            Area = new CellRange(new CellAddress(0, 0), new CellAddress(2, 1)),
        }).PaintPage(0, recorder);

        Assert.Single(recorder.Texts, text => text == "Titolo");
    }

    [Fact]
    public void LaScalaDelloStileDiPaginaRiduceIlNumeroDiPagine()
    {
        var sheet = SheetWith(200, 2);

        var full = Build(sheet, Standard());
        var half = Build(sheet, Standard() with { Scale = 0.5 });

        Assert.True(half.PageCount < full.PageCount, $"{half.PageCount} vs {full.PageCount}");
    }

    [Fact]
    public void AdattaAUnaPaginaInAltezzaStaInUnaPagina()
    {
        var document = Build(SheetWith(200, 2), Standard() with { FitToHeightPages = 1, FitToWidthPages = 1 });

        Assert.Equal(1, document.PageCount);
    }

    [Fact]
    public void UnaRegioneUnitaDivisaFraPagineSiDisegnaUnaVoltaSulRettangoloInteroComeCalc()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Titolo"));
        sheet.Merges.Add(new CellRange(new CellAddress(0, 0), new CellAddress(149, 0)));

        var document = Build(sheet, Standard(), new SheetPrintOptions
        {
            Setup = Standard(),
            Area = new CellRange(new CellAddress(0, 0), new CellAddress(149, 0)),
        });

        Assert.True(document.PageCount > 1);

        var pages = Enumerable.Range(0, document.PageCount)
            .Where(page =>
            {
                var recorder = new Recorder();
                document.PaintPage(page, recorder);
                return recorder.Texts.Contains("Titolo");
            })
            .ToList();

        Assert.Equal([document.PageCount - 1], pages);
    }

    [Fact]
    public void UnTitoloUnitoSuTuttaLaRigaSiStampaSullaPaginaDelSuoCentro()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Bilancio annuale 2026"));
        sheet.Merges.Add(new CellRange(new CellAddress(0, 0), new CellAddress(0, 17)));
        for (var column = 0; column < 18; column++) sheet.SetValue(new CellAddress(3, column), CellValue.Number(column));

        var document = Build(sheet, Standard());
        Assert.True(document.PageCount > 1);

        var printed = Enumerable.Range(0, document.PageCount).Count(page =>
        {
            var recorder = new Recorder();
            document.PaintPage(page, recorder);
            return recorder.Texts.Contains("Bilancio annuale 2026");
        });

        Assert.True(printed >= 1);
    }

    [Fact]
    public void WholeColumnPrintAreaPrintsOnlyTheUsedRows()
    {
        var sheet = new Worksheet("S");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("a"));
        sheet.SetValue(new CellAddress(5, 1), CellValue.Number(1));
        var whole = new CellRange(CellAddress.First, new CellAddress(CellAddress.MaxRows - 1, 1));
        var document = Build(sheet, Standard(), new SheetPrintOptions { Setup = Standard(), Area = whole });
        Assert.Equal(1, document.PageCount);
    }

    [Fact]
    public void WholeColumnPrintAreaOnAnEmptySheetHasNoPages()
    {
        var whole = new CellRange(CellAddress.First, new CellAddress(CellAddress.MaxRows - 1, 1));
        Assert.Equal(0, Build(new Worksheet("S"), Standard(), new SheetPrintOptions { Setup = Standard(), Area = whole }).PageCount);
    }
}
