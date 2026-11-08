using DocumentFormat.OpenXml;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed record DocxParagraphExtras(string? StyleId, int? ListId, int ListLevel, int? OutlineLevel)
{
    public IReadOnlyList<double> ClearedTabs { get; init; } = [];
}

public sealed class DocxParagraphCodec(DocxValues values, DocxBorders boxes)
{
    private readonly IReadOnlyDictionary<TabLeader, W.TabStopLeaderCharValues> _leaders = new Dictionary<TabLeader, W.TabStopLeaderCharValues>
    {
        [TabLeader.None] = W.TabStopLeaderCharValues.None,
        [TabLeader.Dots] = W.TabStopLeaderCharValues.Dot,
        [TabLeader.Dashes] = W.TabStopLeaderCharValues.Hyphen,
        [TabLeader.Underline] = W.TabStopLeaderCharValues.Underscore,
    };

    private readonly IReadOnlyDictionary<TabAlignment, W.TabStopValues> _tabs = new Dictionary<TabAlignment, W.TabStopValues>
    {
        [TabAlignment.Left] = W.TabStopValues.Left,
        [TabAlignment.Center] = W.TabStopValues.Center,
        [TabAlignment.Right] = W.TabStopValues.Right,
        [TabAlignment.Decimal] = W.TabStopValues.Decimal,
    };

    private readonly IReadOnlyDictionary<TextAlign, W.JustificationValues> _aligns = new Dictionary<TextAlign, W.JustificationValues>
    {
        [TextAlign.Left] = W.JustificationValues.Left,
        [TextAlign.Center] = W.JustificationValues.Center,
        [TextAlign.Right] = W.JustificationValues.Right,
        [TextAlign.Justify] = W.JustificationValues.Both,
    };

    public IEnumerable<OpenXmlElement> Elements(ParagraphProperties properties, DocxParagraphExtras extras)
    {
        if (extras.StyleId is not null) yield return new W.ParagraphStyleId { Val = extras.StyleId };
        if (properties.KeepWithNext is { } keepNext) yield return new W.KeepNext { Val = keepNext };
        if (properties.KeepTogether is { } keepLines) yield return new W.KeepLines { Val = keepLines };
        if (properties.PageBreakBefore is { } pageBreak) yield return new W.PageBreakBefore { Val = pageBreak };
        if (properties.Widows is { } widows) yield return new W.WidowControl { Val = widows > 0 || (properties.Orphans ?? 0) > 0 };
        if (extras.ListId is { } listId)
            yield return new W.NumberingProperties(
                new W.NumberingLevelReference { Val = extras.ListLevel },
                new W.NumberingId { Val = listId });
        if (properties.Borders is { } borders) yield return boxes.Write(new W.ParagraphBorders(), borders);
        if (properties.Background is { } background)
            yield return new W.Shading { Val = W.ShadingPatternValues.Clear, Color = "auto", Fill = background >> 24 == 0 ? "auto" : values.Color(background) };
        if (properties.TabStops is { Count: > 0 } || extras.ClearedTabs.Count > 0)
            yield return new W.Tabs(extras.ClearedTabs.Select(position => new W.TabStop { Val = W.TabStopValues.Clear, Position = (int)Math.Round(position * 20) })
                .Concat((properties.TabStops ?? []).Select(stop => new W.TabStop
                {
                    Val = _tabs[stop.Alignment],
                    Leader = _leaders[stop.Leader],
                    Position = (int)Math.Round(stop.Position * 20),
                }))
                .OrderBy(stop => stop.Position!.Value));
        if (properties.Hyphenation is { } hyphenation) yield return new W.SuppressAutoHyphens { Val = !hyphenation.Enabled };
        if (properties.Direction is { } direction) yield return new W.BiDi { Val = direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft };
        if (Spacing(properties) is { } spacing) yield return spacing;
        if (Indentation(properties) is { } indentation) yield return indentation;
        if (properties.ContextualSpacing is { } contextual) yield return new W.ContextualSpacing { Val = contextual };
        if (properties.Align is { } align) yield return new W.Justification { Val = _aligns[align] };
        if (extras.OutlineLevel is { } outline && outline > 0) yield return new W.OutlineLevel { Val = outline - 1 };
    }

    public ParagraphProperties Read(OpenXmlElement? container)
    {
        if (container is null) return ParagraphProperties.None;
        var spacing = container.GetFirstChild<W.SpacingBetweenLines>();
        var indentation = container.GetFirstChild<W.Indentation>();
        var widow = values.Flag(container.GetFirstChild<W.WidowControl>());
        var justification = container.GetFirstChild<W.Justification>()?.Val?.Value;
        var suppress = values.Flag(container.GetFirstChild<W.SuppressAutoHyphens>());
        var hanging = values.Points(indentation?.Hanging?.Value);
        var first = values.Points(indentation?.FirstLine?.Value);
        return new ParagraphProperties
        {
            Align = justification is null ? null : Align(justification.Value),
            SpaceBefore = values.Points(spacing?.Before?.Value),
            SpaceAfter = values.Points(spacing?.After?.Value),
            LineHeight = LineHeight(spacing),
            MarginLeft = values.Points(indentation?.Left?.Value ?? indentation?.Start?.Value),
            MarginRight = values.Points(indentation?.Right?.Value ?? indentation?.End?.Value),
            Indent = hanging is { } h ? -h : first,
            KeepWithNext = values.Flag(container.GetFirstChild<W.KeepNext>()),
            KeepTogether = values.Flag(container.GetFirstChild<W.KeepLines>()),
            PageBreakBefore = values.Flag(container.GetFirstChild<W.PageBreakBefore>()),
            Widows = widow is null ? null : widow.Value ? 2 : 0,
            Orphans = widow is null ? null : widow.Value ? 2 : 0,
            ContextualSpacing = values.Flag(container.GetFirstChild<W.ContextualSpacing>()),
            Direction = values.Flag(container.GetFirstChild<W.BiDi>()) is { } bidi ? bidi ? PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft : PulseOffice.Text.Rich.Bidi.TextDirection.LeftToRight : null,
            Hyphenation = suppress is null ? null : suppress.Value ? ParagraphHyphenation.Off : ParagraphHyphenation.On,
            Background = values.Color(container.GetFirstChild<W.Shading>()?.Fill?.Value),
            TabStops = container.GetFirstChild<W.Tabs>() is { } tabs ? [.. tabs.Elements<W.TabStop>().Where(stop => stop.Val?.Value != W.TabStopValues.Clear).Select(Tab)] : null,
            Borders = container.GetFirstChild<W.ParagraphBorders>() is { } borders ? boxes.Read(borders) : null,
        };
    }

    public DocxParagraphExtras Extras(OpenXmlElement? container)
    {
        var numbering = container?.GetFirstChild<W.NumberingProperties>();
        var outline = container?.GetFirstChild<W.OutlineLevel>()?.Val?.Value;
        return new DocxParagraphExtras(
            container?.GetFirstChild<W.ParagraphStyleId>()?.Val?.Value,
            numbering?.NumberingId?.Val?.Value,
            Math.Clamp(numbering?.NumberingLevelReference?.Val?.Value ?? 0, 0, ListStyle.LevelCount - 1),
            outline is { } level and >= 0 and < 9 ? level + 1 : null);
    }

    private TextAlign Align(W.JustificationValues value) =>
        value == W.JustificationValues.Center ? TextAlign.Center
        : value == W.JustificationValues.Right || value == W.JustificationValues.End ? TextAlign.Right
        : value == W.JustificationValues.Both || value == W.JustificationValues.Distribute ? TextAlign.Justify
        : TextAlign.Left;

    private TabStop Tab(W.TabStop stop) => new((stop.Position?.Value ?? 0) / 20.0)
    {
        Alignment = _tabs.FirstOrDefault(pair => pair.Value == stop.Val?.Value, new(TabAlignment.Left, W.TabStopValues.Left)).Key,
        Leader = _leaders.FirstOrDefault(pair => pair.Value == stop.Leader?.Value, new(TabLeader.None, W.TabStopLeaderCharValues.None)).Key,
    };

    private W.SpacingBetweenLines? Spacing(ParagraphProperties properties)
    {
        var lineHeight = properties.LineHeight ?? (properties.LineSpacing is { } factor ? ParagraphLineHeight.Proportional(factor) : null);
        if (properties.SpaceBefore is null && properties.SpaceAfter is null && lineHeight is null) return null;
        var spacing = new W.SpacingBetweenLines();
        if (properties.SpaceBefore is { } before) spacing.Before = values.Twips(before);
        if (properties.SpaceAfter is { } after) spacing.After = values.Twips(after);
        switch (lineHeight)
        {
            case { Mode: LineHeightMode.Proportional } proportional:
                spacing.Line = values.Text((int)Math.Round(proportional.Value * 240));
                spacing.LineRule = W.LineSpacingRuleValues.Auto;
                break;
            case { Mode: LineHeightMode.AtLeast } atLeast:
                spacing.Line = values.Twips(atLeast.Value);
                spacing.LineRule = W.LineSpacingRuleValues.AtLeast;
                break;
            case { Mode: LineHeightMode.Exact } exact:
                spacing.Line = values.Twips(exact.Value);
                spacing.LineRule = W.LineSpacingRuleValues.Exact;
                break;
        }

        return spacing;
    }

    private ParagraphLineHeight? LineHeight(W.SpacingBetweenLines? spacing)
    {
        if (values.Integer(spacing?.Line?.Value) is not { } line) return null;
        var rule = spacing!.LineRule?.Value;
        if (rule == W.LineSpacingRuleValues.Exact) return ParagraphLineHeight.Exact(line / 20.0);
        if (rule == W.LineSpacingRuleValues.AtLeast) return ParagraphLineHeight.AtLeast(line / 20.0);
        return ParagraphLineHeight.Proportional(line / 240.0);
    }

    private W.Indentation? Indentation(ParagraphProperties properties)
    {
        if (properties.MarginLeft is null && properties.MarginRight is null && properties.Indent is null) return null;
        var indentation = new W.Indentation();
        if (properties.MarginLeft is { } left) indentation.Left = values.Twips(left);
        if (properties.MarginRight is { } right) indentation.Right = values.Twips(right);
        if (properties.Indent is { } indent)
        {
            if (indent < 0) indentation.Hanging = values.Twips(-indent);
            else indentation.FirstLine = values.Twips(indent);
        }

        return indentation;
    }
}
