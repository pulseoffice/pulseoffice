using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Styles;

public sealed record ResolvedParagraphStyle(
    ParagraphProperties Paragraph,
    CharacterProperties Character,
    string? ListStyleName,
    int? OutlineLevel)
{
    public int? ListLevel { get; init; }
}

public interface IStyleChain
{
    IReadOnlyList<TStyle> RootFirst<TStyle>(StyleCollection<TStyle> styles, string? name) where TStyle : DocumentStyle;
}

public sealed class StyleChain : IStyleChain
{
    public IReadOnlyList<TStyle> RootFirst<TStyle>(StyleCollection<TStyle> styles, string? name) where TStyle : DocumentStyle
    {
        var chain = new List<TStyle>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var style = styles.Find(name); style is not null && seen.Add(style.Name); style = styles.Find(style.ParentName))
        {
            chain.Add(style);
        }
        chain.Reverse();
        return chain;
    }
}

public interface IStyleResolver
{
    ResolvedParagraphStyle ResolveParagraphStyle(StyleSheet sheet, string? name);

    CharacterProperties ResolveCharacterStyle(StyleSheet sheet, string? name);

    FrameProperties ResolveFrameStyle(StyleSheet sheet, string? name);

    TableProperties ResolveTableStyle(StyleSheet sheet, string? name);

    TextParagraph EffectiveParagraph(StyleSheet sheet, TextParagraph paragraph);

    TextRun EffectiveRun(StyleSheet sheet, TextParagraph paragraph, TextRun run);
}

public sealed class StyleResolver(IStyleChain chain, IDirectFormatting direct) : IStyleResolver
{
    public StyleResolver() : this(new StyleChain(), new DirectFormatting())
    {
    }

    public ResolvedParagraphStyle ResolveParagraphStyle(StyleSheet sheet, string? name)
    {
        var paragraph = sheet.DefaultParagraph;
        var character = sheet.DefaultCharacter;
        string? list = null;
        int? outline = null;
        int? level = null;
        foreach (var style in chain.RootFirst(sheet.Paragraph, name ?? DefaultStyleNames.Standard))
        {
            paragraph = style.Paragraph.Over(paragraph);
            character = style.Character.Over(character);
            if (style.ListStyleName is not null) level = style.ListLevel;
            list = style.ListStyleName ?? list;
            outline = style.OutlineLevel ?? outline;
        }
        return new ResolvedParagraphStyle(paragraph, character, list, outline) { ListLevel = level };
    }

    public CharacterProperties ResolveCharacterStyle(StyleSheet sheet, string? name) =>
        chain.RootFirst(sheet.Character, name)
            .Aggregate(CharacterProperties.None, (under, style) => style.Character.Over(under));

    public FrameProperties ResolveFrameStyle(StyleSheet sheet, string? name) =>
        chain.RootFirst(sheet.Frame, name).Aggregate(FrameProperties.None, (under, style) => style.Frame.Over(under));

    public TableProperties ResolveTableStyle(StyleSheet sheet, string? name) =>
        chain.RootFirst(sheet.Table, name).Aggregate(TableProperties.None, (under, style) => style.Table.Over(under));

    public TextParagraph EffectiveParagraph(StyleSheet sheet, TextParagraph paragraph)
    {
        var style = ResolveParagraphStyle(sheet, paragraph.StyleName);
        var format = direct.Paragraph(paragraph).Over(style.Paragraph);
        var character = direct.ParagraphCharacter(paragraph).Over(style.Character);
        var resolved = paragraph with
        {
            Align = format.Align ?? TextAlign.Left,
            LineSpacing = format.LineSpacing ?? 1.0,
            SpaceBefore = format.SpaceBefore ?? 0,
            SpaceAfter = format.SpaceAfter ?? 0,
            MarginLeft = format.MarginLeft,
            MarginRight = format.MarginRight,
            Indent = format.Indent,
            KeepWithNext = format.KeepWithNext,
            KeepTogether = format.KeepTogether,
            Widows = format.Widows,
            Orphans = format.Orphans,
            PageBreakBefore = format.PageBreakBefore,
            ColumnBreakBefore = format.ColumnBreakBefore,
            PageStyleName = format.PageStyleName,
            PageNumberRestart = format.PageNumberRestart,
            TabStops = format.TabStops,
            LineHeight = format.LineHeight,
            Hyphenation = format.Hyphenation,
            ContextualSpacing = format.ContextualSpacing,
            Direction = format.Direction,
            Background = format.Background,
            Borders = format.Borders,
            DropCap = format.DropCap,
            ListStyleName = paragraph.ListStyleName ?? style.ListStyleName,
            BulletLevel = paragraph.ListStyleName is null && style.ListStyleName is not null && style.ListLevel is { } styled ? Math.Max(0, styled - 1) : paragraph.BulletLevel,
            OutlineLevel = paragraph.OutlineLevel ?? style.OutlineLevel,
            FontSize = character.FontSize ?? 12,
            Bold = character.Bold ?? false,
            Italic = character.Italic ?? false,
            Underline = character.Underline ?? false,
            Strikethrough = character.Strikethrough ?? false,
            Baseline = character.Baseline ?? TextBaseline.Normal,
            Color = character.Color,
        };
        return resolved with { Runs = [.. paragraph.Runs.Select(run => EffectiveRun(sheet, paragraph, run))] };
    }

    public TextRun EffectiveRun(StyleSheet sheet, TextParagraph paragraph, TextRun run)
    {
        var properties = direct.Run(run)
            .Over(ResolveCharacterStyle(sheet, CharacterStyleOf(run)))
            .Over(direct.ParagraphCharacter(paragraph))
            .Over(ResolveParagraphStyle(sheet, paragraph.StyleName).Character);
        return direct.Apply(run, properties);
    }

    private static string? CharacterStyleOf(TextRun run) =>
        run.StyleName ?? (run.Hyperlink is { Length: > 0 } && run.Field == TextFieldKind.None ? DefaultStyleNames.InternetLink : null);
}
