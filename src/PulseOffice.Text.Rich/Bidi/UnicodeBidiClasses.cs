using System.Globalization;
using System.Text;

namespace PulseOffice.Text.Rich.Bidi;

public interface IBidiClasses
{
    BidiClass Of(int codePoint);
}

public sealed class UnicodeBidiClasses : IBidiClasses
{
    public BidiClass Of(int codePoint) => Control(codePoint) ?? Number(codePoint) ?? Separator(codePoint) ?? Strong(codePoint) ?? General(codePoint);

    private BidiClass? Control(int codePoint) => codePoint switch
    {
        0x0009 or 0x000B or 0x001F => BidiClass.S,
        0x000A or 0x000D or (>= 0x001C and <= 0x001E) or 0x0085 or 0x2029 => BidiClass.B,
        0x000C or 0x0020 or 0x2028 => BidiClass.WS,
        (>= 0x0000 and <= 0x0008) or (>= 0x000E and <= 0x001B) or (>= 0x007F and <= 0x0084) or (>= 0x0086 and <= 0x009F) => BidiClass.BN,
        0x00AD or 0x180E or (>= 0x200B and <= 0x200D) or (>= 0x2060 and <= 0x2064) or 0xFEFF => BidiClass.BN,
        0x200E => BidiClass.L,
        0x200F => BidiClass.R,
        0x061C => BidiClass.AL,
        0x202A => BidiClass.LRE,
        0x202B => BidiClass.RLE,
        0x202C => BidiClass.PDF,
        0x202D => BidiClass.LRO,
        0x202E => BidiClass.RLO,
        0x2066 => BidiClass.LRI,
        0x2067 => BidiClass.RLI,
        0x2068 => BidiClass.FSI,
        0x2069 => BidiClass.PDI,
        _ => null,
    };

    private BidiClass? Number(int codePoint) => codePoint switch
    {
        (>= 0x0030 and <= 0x0039) or 0x00B2 or 0x00B3 or 0x00B9 or 0x2070 or (>= 0x2074 and <= 0x2079) or (>= 0x2080 and <= 0x2089) => BidiClass.EN,
        (>= 0x06F0 and <= 0x06F9) or (>= 0x2488 and <= 0x249B) or (>= 0xFF10 and <= 0xFF19) or (>= 0x1D7CE and <= 0x1D7FF) or (>= 0x1F100 and <= 0x1F10A) => BidiClass.EN,
        (>= 0x0600 and <= 0x0605) or (>= 0x0660 and <= 0x0669) or 0x066B or 0x066C or 0x06DD or 0x08E2 or (>= 0x10E60 and <= 0x10E7E) => BidiClass.AN,
        _ => null,
    };

    private BidiClass? Separator(int codePoint) => codePoint switch
    {
        0x002B or 0x002D or 0x207A or 0x207B or 0x208A or 0x208B or 0x2212 or 0xFB29 or 0xFE62 or 0xFE63 or 0xFF0B or 0xFF0D => BidiClass.ES,
        0x002C or 0x002E or 0x002F or 0x003A or 0x00A0 or 0x060C or 0x202F or 0x2044 or 0xFE50 or 0xFE52 or 0xFE55 or 0xFF0C or 0xFF0E or 0xFF0F or 0xFF1A => BidiClass.CS,
        0x0023 or 0x0024 or 0x0025 or (>= 0x00A2 and <= 0x00A5) or 0x00B0 or 0x00B1 or 0x058F or 0x0609 or 0x060A or 0x066A => BidiClass.ET,
        0x09F2 or 0x09F3 or 0x0AF1 or 0x0BF9 or 0x0E3F or 0x17DB or (>= 0x2030 and <= 0x2034) or (>= 0x20A0 and <= 0x20CF) or 0x212E or 0x2213 => BidiClass.ET,
        0xFE5F or 0xFE69 or 0xFE6A or (>= 0xFF03 and <= 0xFF05) or 0xFFE0 or 0xFFE1 or 0xFFE5 or 0xFFE6 => BidiClass.ET,
        _ => null,
    };

    private BidiClass? Strong(int codePoint)
    {
        if (!Rune.IsValid(codePoint)) return BidiClass.L;
        var category = Rune.GetUnicodeCategory(new Rune(codePoint));
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark) return BidiClass.NSM;
        return codePoint switch
        {
            (>= 0x0590 and <= 0x05FF) or (>= 0x07C0 and <= 0x085F) or (>= 0xFB1D and <= 0xFB4F) => BidiClass.R,
            (>= 0x0600 and <= 0x07BF) or (>= 0x0860 and <= 0x08FF) or (>= 0xFB50 and <= 0xFDCF) or (>= 0xFDF0 and <= 0xFDFF) or (>= 0xFE70 and <= 0xFEFE) => BidiClass.AL,
            (>= 0x10D00 and <= 0x10D3F) or (>= 0x10F30 and <= 0x10F6F) or (>= 0x1EC70 and <= 0x1ECBF) or (>= 0x1ED00 and <= 0x1ED4F) or (>= 0x1EE00 and <= 0x1EEFF) => BidiClass.AL,
            (>= 0x10800 and <= 0x10FFF) or (>= 0x1E800 and <= 0x1EFFF) => BidiClass.R,
            _ => null,
        };
    }

    private BidiClass General(int codePoint)
    {
        if (!Rune.IsValid(codePoint)) return BidiClass.L;
        return Rune.GetUnicodeCategory(new Rune(codePoint)) switch
        {
            UnicodeCategory.SpaceSeparator => BidiClass.WS,
            UnicodeCategory.LineSeparator => BidiClass.WS,
            UnicodeCategory.ParagraphSeparator => BidiClass.B,
            UnicodeCategory.Format or UnicodeCategory.Control => BidiClass.BN,
            UnicodeCategory.CurrencySymbol => BidiClass.ET,
            UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation or UnicodeCategory.OpenPunctuation
                or UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation
                or UnicodeCategory.OtherPunctuation or UnicodeCategory.MathSymbol or UnicodeCategory.ModifierSymbol
                or UnicodeCategory.OtherSymbol => BidiClass.ON,
            _ => BidiClass.L,
        };
    }
}
