namespace PulseOffice.Formats.Intake;

public sealed record IncomingDocumentLimits
{
    public long MaximumFileBytes { get; init; } = 1L << 30;

    public int MaximumElementDepth { get; init; } = 256;

    public long MaximumExpandedBytes { get; init; } = 1L << 30;

    public long MaximumEntryBytes { get; init; } = 512L << 20;

    public long CompressionRatioCheckThreshold { get; init; } = 64L << 20;

    public int MaximumCompressionRatio { get; init; } = 300;

    public int MaximumEntries { get; init; } = 100_000;

    public long MaximumCharactersFromEntities { get; init; } = 4096;
}
