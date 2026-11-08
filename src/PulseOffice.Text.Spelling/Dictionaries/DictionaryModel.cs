using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Text.Spelling.Dictionaries;

public enum DictionaryPart
{
    Spelling,
    Hyphenation,
    Thesaurus,
}

public enum DictionaryFreedom
{
    Free,
    Copyleft,
    Unknown,
}

public enum DictionaryStatus
{
    Unavailable,
    Available,
    OnSystem,
    Installed,
    UpdateAvailable,
}

public sealed record DictionaryLicense(string Name, string? Spdx, DictionaryFreedom Freedom, string? Url = null)
{
    public static readonly DictionaryLicense Unknown = new("Unknown", null, DictionaryFreedom.Unknown);
}

public sealed record DictionaryRemoteFile(string Name, Uri Location);

public sealed record DictionaryPackage(
    string SourceId,
    string Code,
    DictionaryPart Part,
    string Version,
    DictionaryLicense License,
    IReadOnlyList<DictionaryRemoteFile> Files,
    string Origin,
    string? Homepage = null);

public sealed record DictionaryFileContent(string Name, byte[] Data);

public sealed record DictionaryProgress(int Completed, int Total, long Bytes);

public sealed record InstalledDictionary(
    string Code,
    DictionaryPart Part,
    string SourceId,
    string Version,
    DictionaryLicense License,
    string Origin,
    IReadOnlyList<string> Files);

public sealed record DictionaryPartState(
    DictionaryPart Part,
    DictionaryStatus Status,
    DictionaryPackage? Package,
    InstalledDictionary? Installed)
{
    public IReadOnlyList<DictionaryPackage> Alternatives { get; init; } = Package is null ? [] : [Package];

    public bool Inherited { get; init; }
}

public sealed record DictionaryLanguage(string Code, LanguageTag Language, IReadOnlyList<DictionaryPartState> Parts)
{
    public string Base { get; init; } = Code;

    public DictionaryPartState this[DictionaryPart part] => Parts.First(state => state.Part == part);

    public bool HasUpdate => Parts.Any(state => state.Status == DictionaryStatus.UpdateAvailable);

    public bool HasInstalled => Parts.Any(state => state.Status is DictionaryStatus.Installed or DictionaryStatus.UpdateAvailable);
}

public sealed record DictionaryCatalog(IReadOnlyList<DictionaryLanguage> Languages, bool Offline);

public interface IDictionarySource
{
    string Id { get; }

    DictionaryPart Part { get; }

    int Priority { get; }

    Task<IReadOnlyList<DictionaryPackage>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<DictionaryFileContent>> FetchAsync(
        DictionaryPackage package, IProgress<DictionaryProgress>? progress = null, CancellationToken ct = default);
}

public interface IDictionaryChanges
{
    event EventHandler? Changed;

    void Announce();
}

public sealed class DictionaryChanges : IDictionaryChanges
{
    public event EventHandler? Changed;

    public void Announce() => Changed?.Invoke(this, EventArgs.Empty);
}

public interface IDictionaryManager
{
    IReadOnlyList<DictionaryPart> SupportedParts { get; }

    Task<DictionaryCatalog> CatalogAsync(bool refresh = false, CancellationToken ct = default);

    Task<OperationResult<bool>> InstallAsync(
        DictionaryPackage package, IProgress<DictionaryProgress>? progress = null, CancellationToken ct = default);

    OperationResult<bool> Remove(string code, DictionaryPart part);
}
