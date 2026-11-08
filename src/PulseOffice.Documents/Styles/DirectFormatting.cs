using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Styles;

public interface IDirectFormatting
{
    CharacterProperties Run(TextRun run);

    ParagraphProperties Paragraph(TextParagraph paragraph);

    CharacterProperties ParagraphCharacter(TextParagraph paragraph);

    TextRun Apply(TextRun run, CharacterProperties properties);

    TextRun Set(TextRun run, CharacterProperties properties);

    TextParagraph Set(TextParagraph paragraph, ParagraphProperties properties);

    TextRun Clear(TextRun run);

    TextParagraph Clear(TextParagraph paragraph);
}

public sealed class DirectFormatting : IDirectFormatting
{
    public CharacterProperties Run(TextRun run) => new()
    {
        Font = run.Font,
        FontSize = run.FontSize,
        Bold = Flag(run, RunOverrides.Bold, run.Bold),
        Italic = Flag(run, RunOverrides.Italic, run.Italic),
        Underline = Flag(run, RunOverrides.Underline, run.Underline),
        DoubleUnderline = Flag(run, RunOverrides.DoubleUnderline, run.DoubleUnderline),
        Strikethrough = Flag(run, RunOverrides.Strikethrough, run.Strikethrough),
        Overline = Flag(run, RunOverrides.Overline, run.Overline),
        Shadow = Flag(run, RunOverrides.Shadow, run.Shadow),
        Outline = Flag(run, RunOverrides.Outline, run.Outline),
        SmallCaps = Flag(run, RunOverrides.SmallCaps, run.SmallCaps),
        Baseline = run.Explicit.HasFlag(RunOverrides.Baseline) || run.Baseline != TextBaseline.Normal ? run.Baseline : null,
        Color = run.Color,
        Highlight = run.Highlight,
        Language = run.Language,
        LetterSpacing = run.LetterSpacing,
        ScaleWidth = run.ScaleWidth,
        Kerning = run.Kerning,
        Case = run.Case,
        Relief = run.Relief,
        Hidden = run.Hidden,
        RightToLeft = run.RightToLeft,
    };

    public ParagraphProperties Paragraph(TextParagraph paragraph) => new()
    {
        Align = paragraph.Explicit.HasFlag(ParagraphOverrides.Align) ? paragraph.Align : null,
        LineSpacing = paragraph.Explicit.HasFlag(ParagraphOverrides.LineSpacing) ? paragraph.LineSpacing : null,
        SpaceBefore = paragraph.Explicit.HasFlag(ParagraphOverrides.SpaceBefore) ? paragraph.SpaceBefore : null,
        SpaceAfter = paragraph.Explicit.HasFlag(ParagraphOverrides.SpaceAfter) ? paragraph.SpaceAfter : null,
        MarginLeft = paragraph.MarginLeft,
        MarginRight = paragraph.MarginRight,
        Indent = paragraph.Indent,
        KeepWithNext = paragraph.KeepWithNext,
        KeepTogether = paragraph.KeepTogether,
        Widows = paragraph.Widows,
        Orphans = paragraph.Orphans,
        PageBreakBefore = paragraph.PageBreakBefore,
        ColumnBreakBefore = paragraph.ColumnBreakBefore,
        PageStyleName = paragraph.PageStyleName,
        PageNumberRestart = paragraph.PageNumberRestart,
        TabStops = paragraph.TabStops,
        LineHeight = paragraph.LineHeight,
        Hyphenation = paragraph.Hyphenation,
        ContextualSpacing = paragraph.ContextualSpacing,
        Direction = paragraph.Direction,
        Background = paragraph.Background,
        Borders = paragraph.Borders,
        DropCap = paragraph.DropCap,
    };

    public CharacterProperties ParagraphCharacter(TextParagraph paragraph) => new()
    {
        Bold = paragraph.Bold ? true : null,
        Italic = paragraph.Italic ? true : null,
        Underline = paragraph.Underline ? true : null,
        Strikethrough = paragraph.Strikethrough ? true : null,
        Baseline = paragraph.Baseline != TextBaseline.Normal ? paragraph.Baseline : null,
        Color = paragraph.Color,
    };

    public TextRun Apply(TextRun run, CharacterProperties properties) => run with
    {
        Font = properties.Font,
        FontSize = properties.FontSize,
        Bold = properties.Bold ?? false,
        Italic = properties.Italic ?? false,
        Underline = properties.Underline ?? false,
        DoubleUnderline = properties.DoubleUnderline ?? false,
        Strikethrough = properties.Strikethrough ?? false,
        Overline = properties.Overline ?? false,
        Shadow = properties.Shadow ?? false,
        Outline = properties.Outline ?? false,
        SmallCaps = properties.SmallCaps ?? false,
        Baseline = properties.Baseline ?? TextBaseline.Normal,
        Color = properties.Color,
        Highlight = properties.Highlight,
        Language = properties.Language,
        LetterSpacing = properties.LetterSpacing,
        ScaleWidth = properties.ScaleWidth,
        Kerning = properties.Kerning,
        Case = properties.Case,
        Relief = properties.Relief,
        Hidden = properties.Hidden,
        RightToLeft = properties.RightToLeft,
    };

    public TextRun Set(TextRun run, CharacterProperties properties)
    {
        var merged = properties.Over(Run(run));
        var flags = run.Explicit
            | Mark(properties.Bold, RunOverrides.Bold)
            | Mark(properties.Italic, RunOverrides.Italic)
            | Mark(properties.Underline, RunOverrides.Underline)
            | Mark(properties.DoubleUnderline, RunOverrides.DoubleUnderline)
            | Mark(properties.Strikethrough, RunOverrides.Strikethrough)
            | Mark(properties.Overline, RunOverrides.Overline)
            | Mark(properties.Shadow, RunOverrides.Shadow)
            | Mark(properties.Outline, RunOverrides.Outline)
            | Mark(properties.SmallCaps, RunOverrides.SmallCaps)
            | Mark(properties.Baseline, RunOverrides.Baseline);
        return Apply(run, merged) with { Explicit = flags };
    }

    public TextParagraph Set(TextParagraph paragraph, ParagraphProperties properties)
    {
        var merged = properties.Over(Paragraph(paragraph));
        var flags = paragraph.Explicit
            | (properties.Align is null ? ParagraphOverrides.None : ParagraphOverrides.Align)
            | (properties.LineSpacing is null ? ParagraphOverrides.None : ParagraphOverrides.LineSpacing)
            | (properties.SpaceBefore is null ? ParagraphOverrides.None : ParagraphOverrides.SpaceBefore)
            | (properties.SpaceAfter is null ? ParagraphOverrides.None : ParagraphOverrides.SpaceAfter);
        return paragraph with
        {
            Align = merged.Align ?? paragraph.Align,
            LineSpacing = merged.LineSpacing ?? paragraph.LineSpacing,
            SpaceBefore = merged.SpaceBefore ?? paragraph.SpaceBefore,
            SpaceAfter = merged.SpaceAfter ?? paragraph.SpaceAfter,
            MarginLeft = merged.MarginLeft,
            MarginRight = merged.MarginRight,
            Indent = merged.Indent,
            KeepWithNext = merged.KeepWithNext,
            KeepTogether = merged.KeepTogether,
            Widows = merged.Widows,
            Orphans = merged.Orphans,
            PageBreakBefore = merged.PageBreakBefore,
            ColumnBreakBefore = merged.ColumnBreakBefore,
            PageStyleName = merged.PageStyleName,
            PageNumberRestart = merged.PageNumberRestart,
            TabStops = merged.TabStops,
            LineHeight = merged.LineHeight,
            Hyphenation = merged.Hyphenation,
            ContextualSpacing = merged.ContextualSpacing,
            Direction = merged.Direction,
            Background = merged.Background,
            Borders = merged.Borders,
            DropCap = merged.DropCap,
            Explicit = flags,
        };
    }

    public TextRun Clear(TextRun run) => new(run.Text)
    {
        Field = run.Field,
        FieldArgument = run.FieldArgument,
        StyleName = run.StyleName,
        Hyperlink = run.Hyperlink,
    };

    public TextParagraph Clear(TextParagraph paragraph) => new TextParagraph(paragraph.Text)
    {
        StyleName = paragraph.StyleName,
        ListStyleName = paragraph.ListStyleName,
        OutlineLevel = paragraph.OutlineLevel,
        BulletLevel = paragraph.BulletLevel,
        Bullet = paragraph.Bullet,
        Numbered = paragraph.Numbered,
        ListRestart = paragraph.ListRestart,
        ListId = paragraph.ListId,
        ListHeader = paragraph.ListHeader,
        PageStyleName = paragraph.PageStyleName,
        PageNumberRestart = paragraph.PageNumberRestart,
        NextPageStyleName = paragraph.NextPageStyleName,
    }.WithRuns([.. paragraph.Runs.Select(Clear)]);

    private bool? Flag(TextRun run, RunOverrides attribute, bool value) =>
        run.Explicit.HasFlag(attribute) || value ? value : null;

    private RunOverrides Mark<T>(T? value, RunOverrides attribute) where T : struct =>
        value is null ? RunOverrides.None : attribute;
}
