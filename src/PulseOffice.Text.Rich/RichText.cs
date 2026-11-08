namespace PulseOffice.Text.Rich;

public enum TextAlign
{
    Left,
    Center,
    Right,
    Justify,
}

public enum TextBaseline
{
    Normal,
    Superscript,
    Subscript,
}

public enum TextCase
{
    None,
    Upper,
    Lower,
    Capitalize,
}

public enum FontRelief
{
    None,
    Embossed,
    Engraved,
}

public enum PlaceholderRole
{
    Title,
    Subtitle,
    Body,
    Text,
    Picture,
    Free,
}

public enum TextFieldKind
{
    None,
    SlideNumber,
    Date,
    Time,
    SlideCount,
    SlideTitle,
    Author,
    FileName,
    PageNumber,
    PageCount,
    Title,
    Subject,
    WordCount,
    CharacterCount,
    ParagraphCount,
    Reference,
    Variable,
    Sequence,
    Chapter,
    Bookmark,
    Description,
    Keywords,
    ModifiedDate,
    CreatedDate,
    VariableSet,
    ConditionalText,
    HiddenText,
    NoteReference,
    Citation,
    TableCount,
    ImageCount,
    ObjectCount,
    CustomProperty,
    Database,
    HiddenParagraph,
    Sender,
    Preserved,
}

public sealed record FieldDetail
{
    public string? Format { get; init; }

    public string? WhenTrue { get; init; }

    public string? WhenFalse { get; init; }

    public bool Fixed { get; init; }

    public string? Value { get; init; }

    public string? NativeOdf { get; init; }
}

public sealed record TextRun(string Text)
{
    public TextFieldKind Field { get; init; }

    public string? Font { get; init; }

    public double? FontSize { get; init; }

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public bool Strikethrough { get; init; }

    public bool DoubleUnderline { get; init; }

    public bool Overline { get; init; }

    public bool Shadow { get; init; }

    public bool Outline { get; init; }

    public bool SmallCaps { get; init; }

    public TextBaseline Baseline { get; init; }

    public uint? Color { get; init; }

    public ColorRole? ColorRole { get; init; }

    public uint? Highlight { get; init; }

    public bool MajorFont { get; init; }

    public string? StyleName { get; init; }

    public string? Hyperlink { get; init; }

    public string? Language { get; init; }

    public string? AsianFont { get; init; }

    public string? AsianLanguage { get; init; }

    public string? FieldArgument { get; init; }

    public FieldDetail? FieldDetail { get; init; }

    public double? LetterSpacing { get; init; }

    public double? ScaleWidth { get; init; }

    public bool? Kerning { get; init; }

    public TextCase? Case { get; init; }

    public FontRelief? Relief { get; init; }

    public bool? Hidden { get; init; }

    public bool? RightToLeft { get; init; }

    public RunOverrides Explicit { get; init; }

    public string? Ruby { get; init; }
}

public sealed record BulletSymbol(string Character)
{
    public string? Font { get; init; }

    public uint? Color { get; init; }

    public double? RelativeSize { get; init; }

    public bool SymbolCharset { get; init; }

    public ListBulletPicture? Picture { get; init; }
}

public sealed record ListBulletPicture(ReadOnlyMemory<byte> Data, string MediaType, double Width, double Height)
{
    public bool ScalesWithFont { get; init; }
}

public sealed record TextParagraph(string Text)
{
    public int BulletLevel { get; init; }

    public bool Bullet { get; init; }

    public BulletSymbol? BulletSymbol { get; init; }

    public bool Numbered { get; init; }

    public ListNumberFormat? NumberFormat { get; init; }

    public bool IsListItem => Bullet || Numbered;

    public double FontSize { get; init; } = 18;

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public bool Strikethrough { get; init; }

    public TextBaseline Baseline { get; init; }

    public TextAlign Align { get; init; } = TextAlign.Left;

    public uint? Color { get; init; }

    public ColorRole? ColorRole { get; init; }

    public bool MajorFont { get; init; }

    public bool Vertical { get; init; }

    public double LineSpacing
    {
        get;
        init => field = LineSpacingFactor.Bounded(value);
    } = 1.0;

    public double SpaceBefore { get; init; }

    public double SpaceAfter { get; init; }

    public double? MarginLeft { get; init; }

    public double? Indent { get; init; }

    public IReadOnlyList<TextRun> Runs { get; init; } = [];

    public string? StyleName { get; init; }

    public string? ListStyleName { get; init; }

    public int? OutlineLevel { get; init; }

    public int? ListRestart { get; init; }

    public string? ListId { get; init; }

    public bool ListHeader { get; init; }

    public double? MarginRight { get; init; }

    public bool? KeepWithNext { get; init; }

    public bool? KeepTogether { get; init; }

    public int? Widows { get; init; }

    public int? Orphans { get; init; }

    public bool? PageBreakBefore { get; init; }

    public bool? ColumnBreakBefore { get; init; }

    public string? PageStyleName { get; init; }

    public int? PageNumberRestart { get; init; }

    public string? NextPageStyleName { get; init; }

    public IReadOnlyList<TabStop>? TabStops { get; init; }

    public ParagraphLineHeight? LineHeight { get; init; }

    public ParagraphHyphenation? Hyphenation { get; init; }

    public bool? ContextualSpacing { get; init; }

    public PulseOffice.Text.Rich.Bidi.TextDirection? Direction { get; init; }

    public uint? Background { get; init; }

    public BoxBorders? Borders { get; init; }

    public DropCap? DropCap { get; init; }

    public ParagraphOverrides Explicit { get; init; }

    public IReadOnlyList<TextRun> EffectiveRuns() =>
        Runs.Count > 0 ? Runs : [SingleRun()];

    public TextRun SingleRun() => new(Text)
    {
        FontSize = FontSize,
        Bold = Bold,
        Italic = Italic,
        Underline = Underline,
        Strikethrough = Strikethrough,
        Baseline = Baseline,
        Color = Color,
        ColorRole = ColorRole,
        MajorFont = MajorFont,
    };

    public TextParagraph WithRuns(IReadOnlyList<TextRun> runs) => this with
    {
        Runs = runs,
        Text = string.Concat(runs.Select(r => r.Text)),
    };
}

public sealed record RichText(PlaceholderRole Role, IReadOnlyList<TextParagraph> Paragraphs)
{
    public TextFrame Frame { get; init; } = TextFrame.Default;

    public static RichText Empty(PlaceholderRole role) => new(role, []);

    public string PlainText => string.Join("\n", Paragraphs.Select(p => p.Text));

    public bool IsEmpty => Paragraphs.Count == 0 || Paragraphs.All(p => p.Text.Length == 0);

    public RichText WithPlainText(string text)
    {
        var lines = new ParagraphBreaks().Normalize(text).Split('\n');
        var template = Paragraphs.Count > 0 ? Paragraphs[0] : new TextParagraph(string.Empty);

        return this with
        {
            Paragraphs = [.. lines.Select((line, index) =>
                Retyped(index < Paragraphs.Count ? Paragraphs[index] : template, line))],
        };
    }

    private TextParagraph Retyped(TextParagraph paragraph, string line)
    {
        var first = paragraph.Runs.Count > 0 ? paragraph.Runs[0] with { Text = string.Empty } : null;
        var uniform = first is not null && first.Field == default
            && paragraph.Runs.All(run => (run with { Text = string.Empty }) == first);
        return paragraph with { Text = line, Runs = uniform ? [first! with { Text = line }] : [] };
    }
}
