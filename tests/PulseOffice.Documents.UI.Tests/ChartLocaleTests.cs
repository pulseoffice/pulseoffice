using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Charts;
using PulseOffice.Data.UI;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Controls.Charts.Plots;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Text.Rich;
using PulseOffice.UI.Embedding;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class ChartLocaleTests
{
    private sealed class CapturingPlot : ChartPlotBase
    {
        public CultureInfo? Culture { get; private set; }

        public override bool Handles(ChartType type) => true;

        public override void Draw(ChartFrame frame) => Culture = frame.Formatting;
    }

    private sealed class CapturingTables : IDataTableRenderer
    {
        public CultureInfo? Culture { get; private set; }

        public void Render(DrawingContext context, DataTableObject table, Rect bounds, CultureInfo culture) => Culture = culture;

        public IReadOnlyList<double> ColumnWidths(DataSnapshot snapshot, double available, double fontSize) => [];
    }

    private sealed class CapturingSignatures : ISignatureLineRenderer
    {
        public CultureInfo? Culture { get; private set; }

        public void Render(DrawingContext context, SignatureLineObject line, Rect bounds, FormControlPalette palette, CultureInfo culture) => Culture = culture;
    }

    private static void WithSystem(string system, Action body)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(system);
        try
        {
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    private static void Paint(IContentPainter painter, EmbeddedObject content, string system, string language) =>
        WithSystem(system, () =>
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize(40, 40), new Vector(96, 96));
            using var context = bitmap.CreateDrawingContext();
            painter.Paint(context, content, new Rect(0, 0, 40, 40), 1, new ShapeSurroundings() { Language = language });
        });

    [Theory]
    [InlineData("it-IT", "en-US", "1,235", "0.5")]
    [InlineData("en-US", "it-IT", "1.235", "0,5")]
    public void ChartNumbersFollowTheDocumentLanguage(string system, string language, string thousands, string fraction)
    {
        var text = new ChartText(CultureInfo.GetCultureInfo(language));
        WithSystem(system, () =>
        {
            Assert.Equal(thousands, text.Number(1234.5));
            Assert.Equal(fraction, text.Number(0.5));
        });
    }

    [Theory]
    [InlineData("it-IT", "en-US")]
    [InlineData("en-US", "it-IT")]
    public Task ChartsAreDrawnInTheLanguageOfTheirSurroundings(string system, string language) => FolioScene.RunAsync(() =>
    {
        var plot = new CapturingPlot();
        var painter = new ChartContentPainter(new ChartRenderer([plot]), new TextCultures(CultureInfo.InvariantCulture));
        var chart = new ChartDefinition { Id = "c", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty, InlineData = new ChartData(["A"], [new ChartSeries("S", [1.5])]) };
        Paint(painter, chart, system, language);
        Assert.Equal(language, plot.Culture?.Name);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData("it-IT", "en-US")]
    [InlineData("en-US", "it-IT")]
    public Task DataTablesAreDrawnInTheLanguageOfTheirSurroundings(string system, string language) => FolioScene.RunAsync(() =>
    {
        var tables = new CapturingTables();
        Paint(new TableContentPainter(tables, new TextCultures(CultureInfo.InvariantCulture)), new DataTableObject { Id = "t", Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty } }, system, language);
        Assert.Equal(language, tables.Culture?.Name);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData("it-IT", "en-US")]
    [InlineData("en-US", "it-IT")]
    public Task SignatureLinesAreDrawnInTheLanguageOfTheirSurroundings(string system, string language) => FolioScene.RunAsync(() =>
    {
        var signatures = new CapturingSignatures();
        Paint(new SignatureLineContentPainter(signatures, new TextCultures(CultureInfo.InvariantCulture)), new SignatureLineObject { Id = "s" }, system, language);
        Assert.Equal(language, signatures.Culture?.Name);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData("en-US", "tr-TR", "İ")]
    [InlineData("tr-TR", "en-US", "I")]
    public void SmallCapsUpperCaseInTheRunLanguage(string system, string language, string expected)
    {
        var run = new TextRun("i") { SmallCaps = true, Language = language };
        string shown = string.Empty;
        WithSystem(system, () => shown = new RunText().Shown(run, new TextFields(1, DateTime.Now)));
        Assert.Equal(expected, shown);
    }
}
