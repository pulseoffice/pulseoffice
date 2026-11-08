using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Inline;
using PulseOffice.Documents.Layout.Measurement;
using PulseOffice.Documents.Layout.Queries;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class FixedHyphenation : IHyphenationPoints
{
    public Dictionary<string, int[]> Words { get; } = new(StringComparer.Ordinal);

    public IReadOnlySet<int> Find(string text, string? language)
    {
        var points = new HashSet<int>();
        foreach (var (word, positions) in Words)
        {
            for (var at = text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = text.IndexOf(word, at + 1, StringComparison.Ordinal))
            {
                foreach (var position in positions) points.Add(at + position);
            }
        }
        return points;
    }
}

public sealed class LayoutFixture
{
    public const string Page = "Test";

    public LayoutFixture(double width = 60, double height = 120, ITextMeasurer? measurer = null, Action<IServiceCollection>? replace = null)
    {
        var collection = new ServiceCollection()
            .AddDocumentsModel()
            .AddSingleton<IParagraphFlowSettings>(Settings)
            .AddSingleton<IHyphenationPoints>(Hyphenation)
            .AddSingleton(measurer ?? new FixedPitchMeasurer())
            .AddDocumentsLayout();
        replace?.Invoke(collection);
        var services = collection.BuildServiceProvider();
        Engine = services.GetRequiredService<IDocumentLayoutEngine>();
        Queries = services.GetRequiredService<ILayoutQueries>();
        Document = new TextDocument { InitialPageStyle = Page };
        Document.Styles.Page.Add(PageOf(Page, width, height));
    }

    public ConfiguredFlowSettings Settings { get; } = new();

    public FixedHyphenation Hyphenation { get; } = new();

    public IDocumentLayoutEngine Engine { get; }

    public ILayoutQueries Queries { get; }

    public TextDocument Document { get; }

    public PageStyle PageOf(string name, double width, double height) => new(name)
    {
        Size = new DocumentSize(Twip.FromPoints(width), Twip.FromPoints(height)),
        Margins = new Margins(Twip.Zero, Twip.Zero, Twip.Zero, Twip.Zero),
    };

    public ParagraphBlock Add(string text, Func<TextParagraph, TextParagraph>? shape = null)
    {
        var id = Document.NextId("p");
        var paragraph = new TextParagraph(text) { Runs = text.Length == 0 ? [] : [new TextRun(text)] };
        var block = new ParagraphBlock(id, shape is null ? paragraph : shape(paragraph));
        Document.Body.Add(block);
        return block;
    }

    public ParagraphBlock AddRuns(params TextRun[] runs)
    {
        var block = new ParagraphBlock(Document.NextId("p"), new TextParagraph(string.Empty).WithRuns(runs));
        Document.Body.Add(block);
        return block;
    }

    public DocumentLayout Layout() => Engine.Layout(Document);

    public IReadOnlyList<string> LineTexts(DocumentLayout layout, string? paragraphId = null) =>
        [.. layout.Pages.SelectMany(page => page.Lines)
            .Where(line => paragraphId is null || line.ParagraphId == paragraphId)
            .Select(line => string.Concat(line.Line.Runs.Where(run => run.Kind is GlyphRunKind.Text or GlyphRunKind.Hyphen).Select(run => run.Text)).TrimEnd())];

    public IReadOnlyList<LineBox> Lines(DocumentLayout layout) => [.. layout.Pages.SelectMany(page => page.Lines)];
}
