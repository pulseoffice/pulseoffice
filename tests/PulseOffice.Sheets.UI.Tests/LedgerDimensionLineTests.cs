using Avalonia;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Printing;
using PulseOffice.Drawing.Scenes;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Pdf;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Printing;
using PulseOffice.Sheets.UI.LedgerViewParts;
using PulseOffice.Sheets.Values;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Forms;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class LedgerHeadlessApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<LedgerHeadlessApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class LedgerDimensionLineTests
{
    private const string SceneMarkup =
        "<dr3d:scene xmlns:dr3d=\"urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" "
        + "draw:style-name=\"gr7\" dr3d:projection=\"perspective\"><dr3d:light dr3d:enabled=\"true\"/><dr3d:sphere draw:style-name=\"gr7\"/></dr3d:scene>";

    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    private sealed class Parts
    {
        public Parts()
        {
            var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
            Engine = provider.GetRequiredService<ICalculationEngineFactory>().Create(Workbook);
            var texts = new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);
            var status = new PulseOffice.UI.Controls.Services.StatusBarService(
                TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
                new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System));
            Presenter = new PulseOffice.Sheets.Formatting.CellPresenter(new PulseOffice.Sheets.Formatting.NumberFormatter(
                System.Globalization.CultureInfo.InvariantCulture, serial => new DateTime(1899, 12, 30).AddDays(serial)));
            Grid = new SheetGridView(Engine, Presenter, new ConditionalFormattingEvaluator(provider.GetRequiredService<Evaluator>()), new SheetSelection());
            var writer = new LedgerCellWriter(Workbook, Engine, History, status, texts, new PulseOffice.Shell.DocumentWorkspace(), Grid, null, null, null, null);
            Objects = new LedgerObjectEditing(Workbook, Grid, History, status, texts, new SheetConnectors(), new FormControlBinding(PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale.Invariant()), writer);
        }

        public Workbook Workbook { get; } = Workbook.CreateDefault();

        public UndoStack History { get; } = new();

        public CalculationEngine Engine { get; }

        public PulseOffice.Sheets.Formatting.CellPresenter Presenter { get; }

        public SheetGridView Grid { get; }

        public LedgerObjectEditing Objects { get; }

        public Worksheet Sheet => Workbook.Sheets[0];
    }

    private static ShapeObject Dimension() => new()
    {
        Id = "quota",
        Shape = ShapeKind.Line,
        Style = ShapeStyle.Default with { Fill = ShapeFill.None, Line = ShapeLine.Default with { ColorHex = "#FF0000", StartArrow = ArrowEnd.Arrow, EndArrow = ArrowEnd.Arrow } },
        Dimension = new DimensionLine { Start = new ShapePoint(0, 1), End = new ShapePoint(1, 1), Unit = "mm", ShowUnit = true },
    };

    private static PlacedObject Placed(PulseOffice.Document.Embedding.EmbeddedObject content, int row) => new()
    {
        Id = content.Id,
        Anchor = new CellAddress(row, 1),
        Size = new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(1)),
        Content = content,
    };

    [Fact]
    public Task A_dimension_line_moves_resizes_rotates_and_undoes_each_step() => Session.Dispatch(() =>
    {
        var parts = new Parts();
        var original = Placed(Dimension(), 1);
        parts.Sheet.Objects.Add(original);

        PlacedObject Current() => parts.Sheet.Objects.Single();
        void Change(CellAddress anchor, DocumentSize size, double? degrees) =>
            parts.Objects.ApplyObjectChanges([new SheetGridView.ObjectChange(Current(), anchor, Twip.FromPoints(4), Twip.FromPoints(2), size, degrees)]);

        Change(new CellAddress(5, 3), original.Size, null);
        var moved = Current();
        Assert.Equal(new CellAddress(5, 3), moved.Anchor);
        Assert.Equal(original.Content, moved.Content);

        var taller = new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(2.5));
        Change(moved.Anchor, taller, null);
        var resized = Current();
        Assert.Equal(taller, resized.Size);
        Assert.Equal(((ShapeObject)original.Content).Dimension, ((ShapeObject)resized.Content).Dimension);

        Change(resized.Anchor, taller, 30);
        Assert.Equal(30, ((ShapeObject)Current().Content).RotationDegrees);
        Assert.NotNull(((ShapeObject)Current().Content).Dimension);

        parts.History.Undo();
        Assert.Equal(resized, Current());
        parts.History.Undo();
        Assert.Equal(moved, Current());
        parts.History.Undo();
        Assert.Equal(original, Current());
        parts.History.Redo();
        parts.History.Redo();
        parts.History.Redo();
        Assert.Equal(30, ((ShapeObject)Current().Content).RotationDegrees);

        parts.Objects.DeleteObjects([Current()]);
        Assert.Empty(parts.Sheet.Objects);
        parts.History.Undo();
        Assert.Single(parts.Sheet.Objects);
    }, CancellationToken.None);

    [Fact]
    public Task Print_and_pdf_paint_the_dimension_line_with_its_label_and_the_scene() => Session.Dispatch(() =>
    {
        var parts = new Parts();
        parts.Sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Quote"));
        parts.Sheet.Objects.Add(Placed(Dimension() with { RotationDegrees = 30 }, 1));
        parts.Sheet.Objects.Add(Placed(new Scene3DObject { Id = "scena", Markup = SceneMarkup }, 6));
        var rasterizer = new SheetObjectRasterizer(parts.Workbook, null!, null!, new PulseOffice.UI.Controls.Printing.PageImageCapture(),
            new PulseOffice.UI.Controls.Shapes.DrawingObjectRenderer(), new ObjectPathBuilder(), System.Globalization.CultureInfo.InvariantCulture, new OriginalImageSources());
        var document = new SheetPrintDocument(parts.Sheet, parts.Workbook.Styles, parts.Presenter, new HeaderFooterFields(System.Globalization.CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = new PageSetup { Paper = new PaperSize("A4", new DocumentSize(Twip.FromMillimeters(210), Twip.FromMillimeters(297))) } },
            images: rasterizer);
        var canvas = new ImageCanvas();

        for (var page = 0; page < document.PageCount; page++) document.PaintPage(page, canvas);

        Assert.Equal(2, canvas.Images.Count);
        var dimension = canvas.Images[0];
        Assert.True(dimension.Area.Height.Value > Twip.FromCentimeters(1).Value);
        Assert.True(Inked(dimension.Image, (r, g, b) => r > 200 && g < 80 && b < 80));
        Assert.True(Inked(canvas.Images[1].Image, (r, g, b) => r < 200 || g < 200 || b < 200));

        using var stream = new MemoryStream();
        Assert.True(new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(document, stream, new PdfExportOptions("Ledger", Compress: false)).Succeeded);
        Assert.Contains("/Subtype /Image", System.Text.Encoding.Latin1.GetString(stream.ToArray()), StringComparison.Ordinal);
    }, CancellationToken.None);

    private static bool Inked(PageImage image, Func<byte, byte, byte, bool> ink)
    {
        for (var pixel = 0; pixel * 3 + 2 < image.Rgb.Length; pixel++)
            if ((image.Alpha is null || image.Alpha[pixel] > 0) && ink(image.Rgb[pixel * 3], image.Rgb[pixel * 3 + 1], image.Rgb[pixel * 3 + 2])) return true;
        return false;
    }

    private sealed class ImageCanvas : IPageCanvas
    {
        public List<SheetObjectImage> Images { get; } = [];

        public DocumentSize PageSize => new(Twip.FromMillimeters(210), Twip.FromMillimeters(297));

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints(text.Length * font.SizePoints / 2);

        public void DrawImage(PageImage image, DocumentRect bounds) => Images.Add(new SheetObjectImage(image, bounds));

        public void FillRectangle(DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, PulseOffice.Printing.TextAlignment alignment, PulseOffice.Printing.TextBaseline baseline)
        {
        }

        public void DrawPath(PrintPath path)
        {
        }
    }
}
