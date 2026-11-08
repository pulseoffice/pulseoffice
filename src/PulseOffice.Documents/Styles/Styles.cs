using PulseOffice.Documents.Blocks;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Styles;

public enum StyleFamily
{
    Paragraph,
    Character,
    List,
    Page,
    Frame,
    Table,
}

public abstract class DocumentStyle(string name)
{
    public string Name { get; } = name;

    public string? ParentName { get; set { field = value; Touch(); } }

    public string? DisplayName { get; set { field = value; Touch(); } }

    public bool Hidden { get; set { field = value; Touch(); } }

    public abstract StyleFamily Family { get; }

    internal StyleStamp? Stamp { get; private set; }

    internal virtual void Attach(StyleStamp? stamp) => Stamp = stamp;

    protected void Touch() => Stamp?.Advance();

    public DocumentStyle Clone() => CloneAs(Name);

    public abstract DocumentStyle CloneAs(string name);

    protected T CopyCommon<T>(T copy) where T : DocumentStyle
    {
        copy.ParentName = ParentName;
        copy.DisplayName = DisplayName;
        copy.Hidden = Hidden;
        return copy;
    }
}

public enum ParagraphStyleCategory
{
    Text,
    Chapter,
    List,
    Index,
    Special,
}

public sealed class ParagraphStyle(string name) : DocumentStyle(name)
{
    public override StyleFamily Family => StyleFamily.Paragraph;

    public ParagraphProperties Paragraph { get; set { field = value; Touch(); } } = ParagraphProperties.None;

    public CharacterProperties Character { get; set { field = value; Touch(); } } = CharacterProperties.None;

    public string? NextStyleName { get; set { field = value; Touch(); } }

    public int? OutlineLevel { get; set { field = value; Touch(); } }

    public string? ListStyleName { get; set { field = value; Touch(); } }

    public int? ListLevel { get; set { field = value; Touch(); } }

    public ParagraphStyleCategory Category { get; set { field = value; Touch(); } }

    public override DocumentStyle CloneAs(string name) => CopyCommon(new ParagraphStyle(name)
    {
        Paragraph = Paragraph,
        Character = Character,
        NextStyleName = NextStyleName,
        OutlineLevel = OutlineLevel,
        ListStyleName = ListStyleName,
        ListLevel = ListLevel,
        Category = Category,
    });
}

public sealed class CharacterStyle(string name) : DocumentStyle(name)
{
    public override StyleFamily Family => StyleFamily.Character;

    public CharacterProperties Character { get; set { field = value; Touch(); } } = CharacterProperties.None;

    public override DocumentStyle CloneAs(string name) => CopyCommon(new CharacterStyle(name) { Character = Character });
}

public enum NumberFormat
{
    None,
    Bullet,
    Arabic,
    LowerLetter,
    UpperLetter,
    LowerRoman,
    UpperRoman,
}

public sealed record ListLevel(NumberFormat Format)
{
    public string Prefix { get; init; } = string.Empty;

    public string Suffix { get; init; } = ".";

    public string Bullet { get; init; } = "•";

    public int StartAt { get; init; } = 1;

    public int DisplayLevels { get; init; } = 1;

    public bool Legal { get; init; }

    public double Indent { get; init; }

    public double Hanging { get; init; } = 18;

    public string? CharacterStyleName { get; init; }

    public ListBulletPicture? Picture { get; init; }
}

public sealed class ListStyle(string name, IReadOnlyList<ListLevel> levels) : DocumentStyle(name)
{
    public const int LevelCount = 10;

    public override StyleFamily Family => StyleFamily.List;

    public IReadOnlyList<ListLevel> Levels { get; set { field = value; Touch(); } } = levels;

    public ListLevel Level(int level) => Levels.Count == 0 ? new ListLevel(NumberFormat.Arabic) : Levels[Math.Clamp(level, 0, Levels.Count - 1)];

    public override DocumentStyle CloneAs(string name) => CopyCommon(new ListStyle(name, Levels));
}

public sealed class HeaderFooterContent
{
    internal StyleStamp? Stamp { get; set; }

    private void Touch() => Stamp?.Advance();

    public bool Enabled { get; set { field = value; Touch(); } }

    public BlockContainer Default { get; init; } = new();

    public BlockContainer? First { get; set { field = value; Touch(); } }

    public BlockContainer? Even { get; set { field = value; Touch(); } }

    public Twip Spacing { get; set { field = value; Touch(); } } = Twip.FromCentimeters(0.5);

    public Twip MinimumHeight { get; set { field = value; Touch(); } } = Twip.Zero;

    public HeaderFooterContent Clone() => new()
    {
        Enabled = Enabled,
        Default = Default.Clone(),
        First = First?.Clone(),
        Even = Even?.Clone(),
        Spacing = Spacing,
        MinimumHeight = MinimumHeight,
    };
}

public sealed record PageWatermark(string Text)
{
    public string Font { get; init; } = "Liberation Sans";

    public uint Color { get; init; } = 0xFFC0C0C0;

    public double Transparency { get; init; } = 50;

    public double Angle { get; init; } = 45;
}

public enum PageUsage
{
    All,
    Left,
    Right,
    Mirrored,
}

public sealed class PageStyle(string name) : DocumentStyle(name)
{
    public override StyleFamily Family => StyleFamily.Page;

    public DocumentSize Size { get; set { field = value; Touch(); } } = new(Twip.FromMillimeters(210), Twip.FromMillimeters(297));

    public PageOrientation Orientation { get; set { field = value; Touch(); } } = PageOrientation.Portrait;

    public Margins Margins { get; set { field = value; Touch(); } } = Margins.Uniform(Twip.FromCentimeters(2));

    public SectionColumns Columns { get; set { field = value; Touch(); } } = SectionColumns.Single;

    public PageUsage Usage { get; set { field = value; Touch(); } } = PageUsage.All;

    public string? NextStyleName { get; set { field = value; Touch(); } }

    public uint? Background { get; set { field = value; Touch(); } }

    public PulseOffice.Text.Rich.BoxBorders? Borders { get; set { field = value; Touch(); } }

    public PageWatermark? Watermark { get; set { field = value; Touch(); } }

    public HeaderFooterContent Header { get; private set; } = new();

    public HeaderFooterContent Footer { get; private set; } = new();

    internal override void Attach(StyleStamp? stamp)
    {
        base.Attach(stamp);
        Header.Stamp = stamp;
        Footer.Stamp = stamp;
    }

    public bool FirstPageDifferent { get; set { field = value; Touch(); } }

    public bool EvenPagesDifferent { get; set { field = value; Touch(); } }

    public NumberFormat NumberFormat { get; set { field = value; Touch(); } } = NumberFormat.Arabic;

    public Twip Width => Orientation == PageOrientation.Portrait ? Size.Width : Size.Height;

    public Twip Height => Orientation == PageOrientation.Portrait ? Size.Height : Size.Width;

    public override DocumentStyle CloneAs(string name) => CopyCommon(new PageStyle(name)
    {
        Size = Size,
        Orientation = Orientation,
        Margins = Margins,
        Columns = Columns,
        Usage = Usage,
        NextStyleName = NextStyleName,
        Background = Background,
        Borders = Borders,
        Watermark = Watermark,
        Header = Header.Clone(),
        Footer = Footer.Clone(),
        FirstPageDifferent = FirstPageDifferent,
        EvenPagesDifferent = EvenPagesDifferent,
        NumberFormat = NumberFormat,
    });
}

public sealed class FrameStyle(string name) : DocumentStyle(name)
{
    public override StyleFamily Family => StyleFamily.Frame;

    public FrameProperties Frame { get; set { field = value; Touch(); } } = FrameProperties.None;

    public override DocumentStyle CloneAs(string name) => CopyCommon(new FrameStyle(name) { Frame = Frame });
}

public sealed class TableStyle(string name) : DocumentStyle(name)
{
    public override StyleFamily Family => StyleFamily.Table;

    public TableProperties Table { get; set { field = value; Touch(); } } = TableProperties.None;

    public override DocumentStyle CloneAs(string name) => CopyCommon(new TableStyle(name) { Table = Table });
}
