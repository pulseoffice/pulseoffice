namespace PulseOffice.Typography.OpenType;

public sealed record OpenTypeFace
{
    public int CollectionIndex { get; init; }

    public required string Family { get; init; }

    public string Subfamily { get; init; } = "Regular";

    public string? FullName { get; init; }

    public string? PostScriptName { get; init; }

    public string? Version { get; init; }

    public string? Designer { get; init; }

    public string? Copyright { get; init; }

    public string? LicenseDescription { get; init; }

    public string? LicenseUrl { get; init; }

    public int Weight { get; init; } = 400;

    public int Width { get; init; } = 5;

    public bool Italic { get; init; }

    public ushort FsType { get; init; }

    public byte[] Panose { get; init; } = new byte[10];

    public short FamilyClass { get; init; }

    public uint[] UnicodeRanges { get; init; } = new uint[4];

    public uint[] CodePageRanges { get; init; } = new uint[2];

    public bool IsFixedPitch { get; init; }

    public uint CheckSumAdjustment { get; init; }

    public bool HasOs2 { get; init; }

    public EmbeddingPermission Embedding => HasOs2 ? EmbeddingPermission.FromFsType(FsType) : EmbeddingPermission.Unknown;
}

public enum EmbeddingLevel
{
    Unknown,
    Installable,
    Editable,
    PreviewAndPrint,
    Restricted,
}

public sealed record EmbeddingPermission(EmbeddingLevel Level, bool NoSubsetting, bool BitmapOnly)
{
    private const ushort RestrictedBit = 0x0002;
    private const ushort PreviewAndPrintBit = 0x0004;
    private const ushort EditableBit = 0x0008;
    private const ushort NoSubsettingBit = 0x0100;
    private const ushort BitmapOnlyBit = 0x0200;

    public static readonly EmbeddingPermission Unknown = new(EmbeddingLevel.Unknown, false, false);

    public static readonly EmbeddingPermission Installable = new(EmbeddingLevel.Installable, false, false);

    public static EmbeddingPermission FromFsType(ushort fsType)
    {
        var level = (fsType & 0x000F) switch
        {
            0 => EmbeddingLevel.Installable,
            var bits when (bits & EditableBit) != 0 => EmbeddingLevel.Editable,
            var bits when (bits & PreviewAndPrintBit) != 0 => EmbeddingLevel.PreviewAndPrint,
            var bits when (bits & RestrictedBit) != 0 => EmbeddingLevel.Restricted,
            _ => EmbeddingLevel.Installable,
        };

        return new EmbeddingPermission(level, (fsType & NoSubsettingBit) != 0, (fsType & BitmapOnlyBit) != 0);
    }

    public bool AllowsEmbedding => Level is not EmbeddingLevel.Restricted && !BitmapOnly;

    public bool AllowsEditing => Level is EmbeddingLevel.Installable or EmbeddingLevel.Editable or EmbeddingLevel.Unknown;
}
