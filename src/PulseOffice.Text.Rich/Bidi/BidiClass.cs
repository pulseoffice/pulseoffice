namespace PulseOffice.Text.Rich.Bidi;

public enum BidiClass : byte
{
    L,
    R,
    AL,
    EN,
    ES,
    ET,
    AN,
    CS,
    NSM,
    BN,
    B,
    S,
    WS,
    ON,
    LRE,
    LRO,
    RLE,
    RLO,
    PDF,
    LRI,
    RLI,
    FSI,
    PDI,
}

public enum TextDirection
{
    LeftToRight,
    RightToLeft,
}

public sealed record BidiEmbedding(int Start, int End, TextDirection Direction);

public sealed record BidiParagraph(byte BaseLevel, byte[] Levels, BidiClass[] Classes)
{
    public bool RightToLeft => (BaseLevel & 1) == 1;

    public bool IsUniform => Array.TrueForAll(Levels, level => level == BaseLevel) && !RightToLeft;
}
