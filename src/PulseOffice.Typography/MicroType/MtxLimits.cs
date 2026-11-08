namespace PulseOffice.Typography.MicroType;

public sealed record MtxLimits
{
    public static MtxLimits Default { get; } = new();

    public int MaxInputBytes { get; init; } = 32 * 1024 * 1024;

    public int MaxBlockBytes { get; init; } = 64 * 1024 * 1024;

    public int MaxFontBytes { get; init; } = 128 * 1024 * 1024;

    public int MaxTables { get; init; } = 512;

    public int MaxGlyphs { get; init; } = 65535;

    public int MaxPointsPerGlyph { get; init; } = 65535;

    public int MaxComponentsPerGlyph { get; init; } = 4096;
}
