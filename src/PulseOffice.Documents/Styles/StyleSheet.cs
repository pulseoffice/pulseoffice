namespace PulseOffice.Documents.Styles;

public sealed class StyleStamp
{
    public long Value { get; private set; }

    public void Advance() => Value++;
}

public sealed class StyleCollection<TStyle>(StyleStamp stamp) where TStyle : DocumentStyle
{
    private readonly List<TStyle> _styles = [];
    private readonly Dictionary<string, TStyle> _byName = new(StringComparer.Ordinal);

    public StyleCollection() : this(new StyleStamp())
    {
    }

    public IReadOnlyList<TStyle> All => _styles;

    public TStyle? Find(string? name) =>
        name is not null && _byName.TryGetValue(name, out var style) ? style : null;

    public bool Contains(string name) => Find(name) is not null;

    public void Add(TStyle style)
    {
        if (Contains(style.Name)) throw new InvalidOperationException($"Style '{style.Name}' already exists.");
        _styles.Add(style);
        _byName[style.Name] = style;
        Adopt(style);
    }

    public void Replace(TStyle style)
    {
        var index = _styles.FindIndex(existing => existing.Name == style.Name);
        if (index < 0) _styles.Add(style);
        else _styles[index] = style;
        _byName[style.Name] = style;
        Adopt(style);
    }

    public void ReplaceAll(IEnumerable<TStyle> styles)
    {
        var copy = styles.ToList();
        _styles.Clear();
        _styles.AddRange(copy);
        Reindex();
        foreach (var style in copy) style.Attach(stamp);
        stamp.Advance();
    }

    public bool Remove(string name)
    {
        var removed = _byName.Remove(name);
        if (removed)
        {
            _styles.RemoveAll(style => style.Name == name);
            stamp.Advance();
        }
        return removed;
    }

    public void CopyFrom(StyleCollection<TStyle> other)
    {
        _styles.Clear();
        _styles.AddRange(other._styles.Select(style => (TStyle)style.Clone()));
        Reindex();
        foreach (var style in _styles) style.Attach(stamp);
        stamp.Advance();
    }

    private void Reindex()
    {
        _byName.Clear();
        foreach (var style in _styles) _byName.TryAdd(style.Name, style);
    }

    private void Adopt(TStyle style)
    {
        style.Attach(stamp);
        stamp.Advance();
    }
}

public sealed class StyleSheet
{
    private readonly StyleStamp _stamp;

    public StyleSheet() : this(new StyleStamp())
    {
    }

    private StyleSheet(StyleStamp stamp)
    {
        _stamp = stamp;
        Paragraph = new StyleCollection<ParagraphStyle>(stamp);
        Character = new StyleCollection<CharacterStyle>(stamp);
        List = new StyleCollection<ListStyle>(stamp);
        Page = new StyleCollection<PageStyle>(stamp);
        Frame = new StyleCollection<FrameStyle>(stamp);
        Table = new StyleCollection<TableStyle>(stamp);
        Outline.Attach(stamp);
    }

    public long Version => _stamp.Value;

    public CharacterProperties DefaultCharacter { get; set { field = value; _stamp.Advance(); } } = new()
    {
        Font = "Liberation Serif",
        FontSize = 12,
        Bold = false,
        Italic = false,
        Underline = false,
        DoubleUnderline = false,
        Strikethrough = false,
        Overline = false,
        Shadow = false,
        Outline = false,
        SmallCaps = false,
        Baseline = Text.Rich.TextBaseline.Normal,
    };

    public ParagraphProperties DefaultParagraph { get; set { field = value; _stamp.Advance(); } } = new()
    {
        Align = Text.Rich.TextAlign.Left,
        LineSpacing = 1.0,
        SpaceBefore = 0,
        SpaceAfter = 0,
        MarginLeft = 0,
        MarginRight = 0,
        Indent = 0,
        KeepWithNext = false,
        KeepTogether = false,
        Widows = 2,
        Orphans = 2,
        PageBreakBefore = false,
        TabStops = [],
    };

    public double DefaultTabInterval { get; set { field = value; _stamp.Advance(); } } = StandardTabInterval;

    public bool TabsRelativeToIndent { get; set { field = value; _stamp.Advance(); } } = true;

    public const double StandardTabInterval = 1.25 * 72 / 2.54;

    public StyleCollection<ParagraphStyle> Paragraph { get; }

    public StyleCollection<CharacterStyle> Character { get; }

    public StyleCollection<ListStyle> List { get; }

    public StyleCollection<PageStyle> Page { get; }

    public StyleCollection<FrameStyle> Frame { get; }

    public StyleCollection<TableStyle> Table { get; }

    public ListStyle Outline
    {
        get;
        set
        {
            field = value;
            value.Attach(_stamp);
            _stamp.Advance();
        }
    } = new(OutlineStyleName, [.. Enumerable.Range(0, ListStyle.LevelCount).Select(_ => new ListLevel(NumberFormat.None) { Suffix = string.Empty, Hanging = 0 })]);

    public const string OutlineStyleName = "Outline";

    public StyleSheet Clone()
    {
        var copy = new StyleSheet { DefaultCharacter = DefaultCharacter, DefaultParagraph = DefaultParagraph, DefaultTabInterval = DefaultTabInterval, TabsRelativeToIndent = TabsRelativeToIndent, Outline = (ListStyle)Outline.Clone() };
        copy.Paragraph.CopyFrom(Paragraph);
        copy.Character.CopyFrom(Character);
        copy.List.CopyFrom(List);
        copy.Page.CopyFrom(Page);
        copy.Frame.CopyFrom(Frame);
        copy.Table.CopyFrom(Table);
        return copy;
    }
}
