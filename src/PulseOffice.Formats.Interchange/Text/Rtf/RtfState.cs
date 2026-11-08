using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Interchange.Text.Rtf;

public enum RtfDestination
{
    Body,
    Skip,
    FontTable,
    ColorTable,
    StyleSheet,
    InfoText,
    Picture,
    FieldInstruction,
    FieldResult,
    ListTable,
    ListOverrideTable,
    BookmarkStart,
    BookmarkEnd,
    ObjectWrapper,
}

public sealed class RtfFieldContext
{
    public System.Text.StringBuilder Instruction { get; } = new();
}

public sealed class RtfCharacterState
{
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public bool? Underline { get; set; }
    public bool? DoubleUnderline { get; set; }
    public bool? Strikethrough { get; set; }
    public bool? SmallCaps { get; set; }
    public bool? Shadow { get; set; }
    public bool? Outline { get; set; }
    public TextBaseline? Baseline { get; set; }
    public int? FontSize { get; set; }
    public int? Font { get; set; }
    public int? Color { get; set; }
    public int? Highlight { get; set; }
    public int? Language { get; set; }
    public int? CharacterStyle { get; set; }
    public bool Hidden { get; set; }
    public TextCase? Case { get; set; }
    public FontRelief? Relief { get; set; }
    public int? Expand { get; set; }
    public int? Scale { get; set; }
    public bool? Kerning { get; set; }
    public bool? RightToLeft { get; set; }

    public RtfCharacterState Copy() => (RtfCharacterState)MemberwiseClone();
}

public sealed class RtfParagraphState
{
    public TextAlign? Align { get; set; }
    public int? Left { get; set; }
    public int? Right { get; set; }
    public int? First { get; set; }
    public int? Before { get; set; }
    public int? After { get; set; }
    public bool? KeepWithNext { get; set; }
    public bool? KeepTogether { get; set; }
    public bool? PageBreakBefore { get; set; }
    public int? Style { get; set; }
    public int? ListOverride { get; set; }
    public int ListLevel { get; set; }
    public bool? LegacyBullet { get; set; }
    public bool InTable { get; set; }
    public int? OutlineLevel { get; set; }
    public int? Shading { get; set; }
    public PulseOffice.Text.Rich.Bidi.TextDirection? Direction { get; set; }
    public RtfParagraphBorders? Borders { get; set; }

    public RtfParagraphState Copy() => (RtfParagraphState)MemberwiseClone();
}

public sealed class RtfGroupState
{
    public RtfDestination Destination { get; set; } = RtfDestination.Body;
    public RtfCharacterState Character { get; set; } = new();
    public RtfParagraphState Paragraph { get; set; } = new();
    public int UnicodeSkip { get; set; } = 1;
    public RtfFieldContext? Field { get; set; }
    public string? InfoKey { get; set; }
    public bool Footnote { get; set; }

    public string? RegionStyle { get; set; }

    public RtfGroupState Copy() => new()
    {
        Destination = Destination,
        Character = Character.Copy(),
        Paragraph = Paragraph.Copy(),
        UnicodeSkip = UnicodeSkip,
        Field = Field,
        InfoKey = InfoKey,
    };
}

public sealed record RtfFont(string Name, int CodePage);
