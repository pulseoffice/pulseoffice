using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Lines;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Flow;

public sealed record LayoutOptions
{
    public string DefaultFont { get; init; } = "Liberation Serif";

    public double SuperscriptScale { get; init; } = 0.58;

    public double SuperscriptRaise { get; init; } = 0.33;

    public double SubscriptLower { get; init; } = 0.33;

    public double ListIndentPerLevel { get; init; } = 18;

    public double SmallCapsScale { get; init; } = 0.8;

    public double CapHeight { get; init; } = 0.7;

    public string? DefaultLanguage { get; init; }
}

public interface IParagraphFlowSettings
{
    LineSpacingRule LineSpacing(ParagraphBlock block, TextParagraph effective);

    ParagraphHyphenation? Hyphenation(ParagraphBlock block, TextParagraph effective);

    bool ContextualSpacing(ParagraphBlock block, TextParagraph effective);

    string? PageStyleName(ParagraphBlock block, TextParagraph effective);
}

public interface ILineSpacingRules
{
    LineSpacingRule Of(TextParagraph effective);
}

public sealed class LineSpacingRules : ILineSpacingRules
{
    public LineSpacingRule Of(TextParagraph effective) => effective.LineHeight switch
    {
        null => effective.LineSpacing == 1.0 ? LineSpacingRule.Single : new ProportionalSpacing(effective.LineSpacing),
        { Mode: LineHeightMode.Proportional, Value: 1.0 } => LineSpacingRule.Single,
        { Mode: LineHeightMode.Proportional } height => new ProportionalSpacing(height.Value),
        { Mode: LineHeightMode.AtLeast } height => new AtLeastSpacing(height.Value),
        { Mode: LineHeightMode.Exact } height => new ExactSpacing(height.Value),
        { Mode: LineHeightMode.Leading } height => new LeadingSpacing(height.Value),
        _ => LineSpacingRule.Single,
    };
}

public sealed class ModelFlowSettings(ILineSpacingRules spacing) : IParagraphFlowSettings
{
    public ModelFlowSettings() : this(new LineSpacingRules())
    {
    }

    public LineSpacingRule LineSpacing(ParagraphBlock block, TextParagraph effective) => spacing.Of(effective);

    public ParagraphHyphenation? Hyphenation(ParagraphBlock block, TextParagraph effective) =>
        effective.Hyphenation is { Enabled: true } hyphenation ? hyphenation : null;

    public bool ContextualSpacing(ParagraphBlock block, TextParagraph effective) => effective.ContextualSpacing ?? false;

    public string? PageStyleName(ParagraphBlock block, TextParagraph effective) => effective.PageStyleName;
}

public sealed class ConfiguredFlowSettings(IParagraphFlowSettings model) : IParagraphFlowSettings
{
    public ConfiguredFlowSettings() : this(new ModelFlowSettings())
    {
    }

    public Dictionary<string, LineSpacingRule> Spacing { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Hyphenated { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Contextual { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> PageStyles { get; } = new(StringComparer.Ordinal);

    public LineSpacingRule LineSpacing(ParagraphBlock block, TextParagraph effective) =>
        Spacing.TryGetValue(block.Id, out var rule) ? rule : model.LineSpacing(block, effective);

    public ParagraphHyphenation? Hyphenation(ParagraphBlock block, TextParagraph effective) =>
        Hyphenated.Contains(block.Id) ? ParagraphHyphenation.On with { MinimumWordLength = 0, CharactersBefore = 0, CharactersAfter = 0 } : model.Hyphenation(block, effective);

    public bool ContextualSpacing(ParagraphBlock block, TextParagraph effective) => Contextual.Contains(block.Id) || model.ContextualSpacing(block, effective);

    public string? PageStyleName(ParagraphBlock block, TextParagraph effective) =>
        PageStyles.TryGetValue(block.Id, out var name) ? name : model.PageStyleName(block, effective);
}
