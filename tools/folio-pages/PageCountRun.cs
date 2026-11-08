using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents;
using PulseOffice.Documents.Layout;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Typography.OpenType;
using PulseOffice.Typography.OpenType.Metrics;
using PulseOffice.UI.Controls.Fonts;

namespace FolioPages;

public sealed class PageCountComposition
{
    public IServiceProvider Build(IReadOnlyList<string> extraFontDirectories)
    {
        var directories = new PulseOffice.Platform.SystemFontDirectories().Directories.Concat(extraFontDirectories).ToList();
        var installed = new IndexedFontLocator(directories, new OpenTypeReader(), new FontMetricsReader());
        var located = new SubstitutingLocator(installed, new MetricCompatibleSubstitutes());
        return new ServiceCollection()
            .AddDocumentsModel()
            .AddSingleton<PulseOffice.Documents.Layout.Measurement.ITextMeasurer>(new PulseOffice.Documents.Layout.Measurement.OpenType.OpenTypeMeasurer(
                located,
                new PulseOffice.Documents.Layout.Measurement.OpenType.HorizontalHeaderMetrics(),
                new PulseOffice.Documents.Layout.Measurement.OpenType.ExactAdvances(),
                new PulseOffice.Documents.Layout.Measurement.FixedPitchMeasurer()))
            .AddDocumentsLayout()
            .BuildServiceProvider();
    }
}

public sealed class SubstitutingLocator(IFontFileLocator inner, IFontSubstitutes substitutes) : IFontFileLocator
{
    public FontMetricsFace? Find(string family, bool bold, bool italic)
    {
        if (inner.Find(family, bold, italic) is { } exact && exact.Family.Equals(family, StringComparison.OrdinalIgnoreCase)) return exact;
        foreach (var substitute in substitutes.For(family))
            if (inner.Find(substitute, bold, italic) is { } found && found.Family.Equals(substitute, StringComparison.OrdinalIgnoreCase)) return found;
        return inner.Find(family, bold, italic);
    }
}

public sealed class PageCountRun(IServiceProvider services)
{
    private readonly TimeSpan _limit = TimeSpan.FromSeconds(90);

    public int Run(string listPath, string outputPath)
    {
        var done = File.Exists(outputPath) ? File.ReadLines(outputPath).Select(line => line.Split('\t')[0]).ToHashSet() : [];
        using var output = new StreamWriter(outputPath, append: true) { AutoFlush = true };
        foreach (var path in File.ReadLines(listPath).Where(path => path.Length > 0 && !done.Contains(path)))
        {
            var work = Task.Run(() => Measure(path));
            if (!work.Wait(_limit))
            {
                output.WriteLine($"{path}\tTIMEOUT");
                return 3;
            }
            output.WriteLine($"{path}\t{work.Result}");
        }
        return 0;
    }

    private string Measure(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var read = Path.GetExtension(path).Equals(".docx", StringComparison.OrdinalIgnoreCase)
                ? new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory()).Reader(DocxFlavour.Document).Read(stream)
                : new OdtReader().Read(stream);
            if (!read.Succeeded || read.Value is null) return "UNREADABLE";
            var layout = services.GetRequiredService<IDocumentLayoutEngine>().Layout(read.Value);
            return layout.Pages.Count + "\t" + string.Join(" | ", layout.Pages.Select(FirstLine));
        }
        catch (Exception error)
        {
            return "ERROR\t" + error.GetType().Name + ": " + error.Message.ReplaceLineEndings(" ");
        }
    }

    private string FirstLine(PageBox page)
    {
        var line = page.AllLines.FirstOrDefault(candidate => candidate.Line.Runs.Any(run => run.Text.Trim().Length > 0));
        if (line is null) return string.Empty;
        var text = string.Concat(line.Line.Runs.Select(run => run.Text)).Replace('\t', ' ').Trim();
        return text.Length > 40 ? text[..40] : text;
    }
}
