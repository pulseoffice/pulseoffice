using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Inline;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Flow;

public sealed record EntryRequest(ParagraphBlock Block, TextParagraph Content, ListPlacement? List)
{
    public StoryKind Story { get; init; } = StoryKind.Body;

    public string? StoryId { get; init; }

    public IReadOnlyList<InlineInsertion> Inlines { get; init; } = [];

    public IReadOnlyList<string> Objects { get; init; } = [];

    public IReadOnlyList<object> Parts { get; init; } = [];

    public string Variant { get; init; } = string.Empty;
}

public interface IParagraphEntries
{
    TextParagraph Effective(StyleSheet sheet, ParagraphBlock block, TextParagraph content);

    ParagraphEntry Entry(TextDocument document, EntryRequest request);

    void Clear();
}

public sealed class ParagraphEntries(
    IStyleResolver resolver,
    IParagraphShaper shaper,
    IInlineInserter inliner,
    IParagraphComposition composition,
    IParagraphFlowSettings settings,
    LayoutOptions options) : IParagraphEntries
{
    private readonly Dictionary<(string Id, string Variant), (TextParagraph Content, TextParagraph Effective, string Font)> _effective = new();
    private readonly Dictionary<(string Id, StoryKind Story, string Variant), ParagraphEntry> _entries = new();

    public void Clear()
    {
        _effective.Clear();
        _entries.Clear();
    }

    public TextParagraph Effective(StyleSheet sheet, ParagraphBlock block, TextParagraph content) => Resolve(sheet, block, content, string.Empty).Effective;

    public ParagraphEntry Entry(TextDocument document, EntryRequest request)
    {
        var block = request.Block;
        var label = request.List?.Key ?? string.Empty;
        IReadOnlyList<object> parts = request.Parts.Count == 0 && request.Inlines.Count == 0 ? [] : [.. request.Parts, .. request.Inlines];
        if (_entries.TryGetValue((block.Id, request.Story, request.Variant), out var cached)
            && ReferenceEquals(cached.Content, request.Content) && cached.Label == label
            && cached.ExtraParts.Count == parts.Count && (parts.Count == 0 || cached.Key.Matches(new FlowKey(block.Id, [request.Content, .. parts], label))))
        {
            return cached;
        }
        var (_, effective, font) = Resolve(document.Styles, block, request.Content, request.Variant);
        var list = request.List;
        var language = effective.Runs.Select(run => run.Language).FirstOrDefault(value => value is not null) ?? options.DefaultLanguage ?? document.Language;
        var placement = list is null || (list.Label.Length == 0 && list.Picture is null) ? null : new ListLabelPlacement(list.Label, list.LabelPosition) { FollowedByTab = list.FollowedByTab, Picture = list.Picture };
        var shaped = inliner.Insert(shaper.Shape(new ShapingRequest(effective, font, placement, settings.Hyphenation(block, effective), language)), request.Inlines);
        var decoration = Decoration(effective);
        var mirrored = effective.Direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft;
        var frame = new ParagraphFrame(
            Finite(effective.MarginLeft) + (list?.ExtraMargin ?? 0) + (decoration?.Inset(mirrored ? BorderSide.Right : BorderSide.Left) ?? 0),
            Finite(effective.MarginRight) + (decoration?.Inset(mirrored ? BorderSide.Left : BorderSide.Right) ?? 0),
            list is null ? Finite(effective.Indent) : list.LabelPosition,
            effective.Align,
            [.. (effective.TabStops ?? []).Where(tab => double.IsFinite(tab.Position)).OrderBy(tab => tab.Position)],
            document.Styles.DefaultTabInterval) { RightToLeft = mirrored };
        if (!document.Styles.TabsRelativeToIndent) frame = frame with { TabOrigin = -frame.MarginLeft };
        var flow = new ParagraphFlow(
            Math.Max(0, Finite(effective.SpaceBefore)) + (decoration?.Inset(BorderSide.Top) ?? 0),
            Math.Max(0, Finite(effective.SpaceAfter)) + (decoration?.Inset(BorderSide.Bottom) ?? 0),
            effective.KeepWithNext ?? false,
            effective.KeepTogether ?? false,
            Math.Max(0, effective.Widows ?? 2),
            Math.Max(0, effective.Orphans ?? 2),
            effective.PageBreakBefore ?? false,
            settings.ContextualSpacing(block, effective),
            settings.PageStyleName(block, effective),
            effective.StyleName)
        {
            PageNumberRestart = effective.PageNumberRestart,
            ColumnBreakBefore = effective.ColumnBreakBefore ?? false,
            NextPageStyleName = effective.NextPageStyleName,
        };
        var entry = new ParagraphEntry(block, request.Content, label, shaped, frame, settings.LineSpacing(block, effective), flow, composition)
        {
            Story = request.Story,
            StoryId = request.StoryId,
            Decoration = decoration,
            AnchoredObjects = request.Objects,
            ExtraParts = parts,
        };
        _entries[(block.Id, request.Story, request.Variant)] = entry;
        return entry;
    }

    private ParagraphDecoration? Decoration(TextParagraph effective)
    {
        var borders = effective.Borders ?? new BoxBorders();
        if (effective.Background is null && !borders.HasLines) return null;
        return new ParagraphDecoration(effective.Background, borders);
    }

    private (TextParagraph Content, TextParagraph Effective, string Font) Resolve(StyleSheet sheet, ParagraphBlock block, TextParagraph content, string variant)
    {
        if (_effective.TryGetValue((block.Id, variant), out var cached) && ReferenceEquals(cached.Content, content)) return cached;
        var style = resolver.ResolveParagraphStyle(sheet, content.StyleName);
        cached = (content, resolver.EffectiveParagraph(sheet, content), style.Character.Font ?? options.DefaultFont);
        _effective[(block.Id, variant)] = cached;
        return cached;
    }

    private double Finite(double? value) => value is { } number && double.IsFinite(number) ? number : 0;
}
