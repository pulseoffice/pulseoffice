using System.Globalization;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Printing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Printing.Tests;

public class ConditionalPrintTests
{
    private sealed class Recorder : IPageCanvas
    {
        public List<string> Texts { get; } = [];
        public List<PrintPath> Paths { get; } = [];
        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));
        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints((text?.Length ?? 0) * 5.0);
        public void FillRectangle(DocumentRect rect, PrintColor color) { }
        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) { }
        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) => Texts.Add(text);
        public void DrawPath(PrintPath path) => Paths.Add(path);
        public void DrawImage(PageImage image, DocumentRect bounds) { }
    }

    private sealed class Fixed(ConditionalAppearance appearance) : IConditionalAppearanceSource
    {
        public ConditionalAppearance At(Worksheet sheet, CellAddress address) => appearance;
    }

    private static Recorder Print(ConditionalAppearance appearance)
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(42));
        var document = new SheetPrintDocument(sheet, new Styling.StyleCatalog(),
            new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.MinValue)),
            new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = new PageSetup { Paper = new PaperCatalog().Default } },
            conditional: new Fixed(appearance));
        var recorder = new Recorder();
        document.PaintPage(0, recorder);
        return recorder;
    }

    [Fact]
    public void LaBarraEIlSimboloSiStampanoComeNellaGriglia()
    {
        var recorder = Print(new ConditionalAppearance
        {
            BarFraction = 0.5, BarColorHex = "#638EC6", IconSetName = "3Flags", IconIndex = 2,
            BarShape = new DataBarShape(0, 0.5, null, true, "#638EC6", "#112233", "#000000"),
        });

        Assert.Contains(recorder.Paths, path => path.Fill is { } fill && fill == PrintColor.FromHex("#638EC6")
            && path.Gradient is { AngleDegrees: 0 } && path.Stroke == PrintColor.FromHex("#112233"));
        Assert.Contains(recorder.Paths, path => path.Fill is { } fill && fill == PrintColor.FromHex("#A1DDAA"));
        Assert.Contains("42", recorder.Texts);
    }

    [Fact]
    public void LaBarraPienaConAsseStampaIlPezzoNegativoESenzaGradiente()
    {
        var recorder = Print(new ConditionalAppearance { BarShape = new DataBarShape(0.25, 0.5, 0.5, false, "#FF0000", null, "#000000") });

        Assert.Contains(recorder.Paths, path => path.Fill == PrintColor.FromHex("#FF0000") && path.Gradient is null && path.Stroke is null);
    }

    [Fact]
    public void UnInsiemeCheNascondeIlValoreNonStampaIlNumero() =>
        Assert.DoesNotContain("42", Print(new ConditionalAppearance { IconSetName = "3Arrows", IconIndex = 0, HideValue = true }).Texts);
}
