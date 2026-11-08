using PulseOffice.Typography.Licensing;
using PulseOffice.Typography.OpenType;

namespace PulseOffice.Typography.Catalog;

public enum FontOrigin
{
    Document,
    System,
    Downloaded,
    Online,
}

public enum FontCategory
{
    Unknown,
    Serif,
    SansSerif,
    Monospace,
    Display,
    Handwriting,
    Symbol,
}

public enum FontScript
{
    Latin,
    LatinExtended,
    Greek,
    Cyrillic,
    Vietnamese,
    Armenian,
    Hebrew,
    Arabic,
    Devanagari,
    Bengali,
    Tamil,
    Thai,
    Georgian,
    Ethiopic,
    Korean,
    Japanese,
    Chinese,
    Math,
    Symbols,
}

public sealed record FontFaceRef(int Weight, bool Italic, string Location, int CollectionIndex = 0)
{
    public bool IsBold => Weight >= 600;
}

public sealed record FontFamilyInfo
{
    public required string Family { get; init; }

    public required string SourceId { get; init; }

    public required FontOrigin Origin { get; init; }

    public FontCategory Category { get; init; }

    public IReadOnlyList<FontFaceRef> Faces { get; init; } = [];

    public IReadOnlyList<FontScript> Scripts { get; init; } = [];

    public FontLicense License { get; init; } = FontLicense.Unknown;

    public EmbeddingPermission Embedding { get; init; } = EmbeddingPermission.Unknown;

    public string? Designer { get; init; }

    public string? Homepage { get; init; }

    public string? OnlineId { get; init; }

    public bool InDocument { get; init; }

    public bool IsAvailable => Faces.Count > 0 || Origin == FontOrigin.System;

    public bool IsDownloaded => Origin == FontOrigin.Downloaded;

    public bool NeedsDownload => Origin == FontOrigin.Online;

    public IReadOnlyList<string> StyleNames =>
        [.. Faces.Select(face => $"{face.Weight}{(face.Italic ? "i" : string.Empty)}").Distinct(StringComparer.Ordinal)];

    public bool Matches(string query) =>
        string.IsNullOrWhiteSpace(query) || Family.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed record FontProgress(int CompletedFiles, int TotalFiles, long Bytes)
{
    public double Fraction => TotalFiles == 0 ? 0 : (double)CompletedFiles / TotalFiles;
}
