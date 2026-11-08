using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Formats.Pdf;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Text.Rich;
using PulseOffice.Typography.OpenType;
using PulseOffice.Typography.OpenType.Metrics;
using PulseOffice.Typography.Subsetting;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioPdfFontTests
{
    [Fact]
    public Task IlPdfDiFolioIncorporaICaratteriConCollegamentiESegnalibri() => FolioScene.RunAsync(() =>
    {
        var installed = new IndexedFontLocator(new PulseOffice.Platform.SystemFontDirectories(), new OpenTypeReader(), new FontMetricsReader());
        var located = new Substituting(installed, new PulseOffice.UI.Controls.Fonts.MetricCompatibleSubstitutes());
        var measurer = new PulseOffice.Documents.Layout.Measurement.OpenType.OpenTypeMeasurer(located, new PulseOffice.Documents.Layout.Measurement.OpenType.HorizontalHeaderMetrics(), new PulseOffice.Documents.Layout.Measurement.OpenType.ExactAdvances(), new PulseOffice.Documents.Layout.Measurement.FixedPitchMeasurer());
        var scene = new FolioScene(measurer: measurer, prepare: document =>
        {
            document.Body.Replace(
            [
                new ParagraphBlock(document.NextId("p"), "Capitolo primo", DefaultStyleNames.HeadingLevel(1)),
                new ParagraphBlock(document.NextId("p"), new TextParagraph("Perché così è già più «bello» — 10 € https://example.org") { Runs = [new TextRun("Perché così è già più «bello» — 10 € "), new TextRun("https://example.org") { Hyperlink = "https://example.org" }] }),
                new ParagraphBlock(document.NextId("p"), "日本語のテキスト Ελληνικά Русский"),
            ]);
        });
        var printable = scene.Services.GetRequiredService<IFolioPrintables>().From(scene.Editor.Layout, scene.Document)!;
        var exporter = new PdfExporter(new PdfFontSetFactory(new PdfFaceLocator(located, installed), new PdfFallbackFonts(), new TrueTypeSubsetter(), new ToUnicodeWriter(), new PdfFontMetrics(), new PulseOffice.Typography.Shaping.HarfBuzzTextShaper(), new PulseOffice.Typography.Shaping.ComplexScriptSegmenter()));
        using var stream = new MemoryStream();
        var result = exporter.Write(printable, stream, new PdfExportOptions("Prova", Compress: false));
        if (Environment.GetEnvironmentVariable("FOLIO_PDF_OUT") is { Length: > 0 } output) File.WriteAllBytes(output, stream.ToArray());
        var text = System.Text.Encoding.Latin1.GetString(stream.ToArray());

        Assert.True(result.Succeeded, string.Join(";", result.Diagnostics.Select(d => d.Code + d.Message)));
        Assert.Contains("/FontFile2", text, StringComparison.Ordinal);
        Assert.Contains("/ToUnicode", text, StringComparison.Ordinal);
        Assert.Contains("/URI (https://example.org)", text, StringComparison.Ordinal);
        Assert.Contains("/Title (Capitolo primo)", text, StringComparison.Ordinal);
        return Task.CompletedTask;
    });

    private sealed class Substituting(IFontFileLocator inner, PulseOffice.UI.Controls.Fonts.IFontSubstitutes substitutes) : IFontFileLocator
    {
        public FontMetricsFace? Find(string family, bool bold, bool italic)
        {
            if (inner.Find(family, bold, italic) is { } exact && exact.Family.Equals(family, StringComparison.OrdinalIgnoreCase)) return exact;
            foreach (var substitute in substitutes.For(family))
                if (inner.Find(substitute, bold, italic) is { } found && found.Family.Equals(substitute, StringComparison.OrdinalIgnoreCase)) return found;
            return inner.Find(family, bold, italic);
        }
    }
}
