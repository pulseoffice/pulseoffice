using System.Text.Json;
using Microsoft.Extensions.Logging;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Text.Spelling.Dictionaries;

namespace PulseOffice.Text.Spelling.Hunspell.Catalog;

public sealed class DictionaryManager(
    IEnumerable<IRefreshableDictionarySource> sources,
    IInstalledDictionaries installed,
    ISystemDictionaryInventory inventory,
    IDictionaryCodes codes,
    IDictionaryChanges changes,
    IDictionaryRanking ranking,
    ILogger<DictionaryManager> logger) : IDictionaryManager
{
    private static readonly DictionaryPart[] Parts = [DictionaryPart.Spelling, DictionaryPart.Hyphenation, DictionaryPart.Thesaurus];

    private readonly IReadOnlyList<IRefreshableDictionarySource> _sources = [.. sources];

    public IReadOnlyList<DictionaryPart> SupportedParts => [.. _sources.Select(source => source.Part).Distinct()];

    public async Task<DictionaryCatalog> CatalogAsync(bool refresh = false, CancellationToken ct = default)
    {
        var packages = new List<DictionaryPackage>();
        var offline = false;

        foreach (var source in _sources)
        {
            packages.AddRange(await source.ListAsync(refresh, ct).ConfigureAwait(false));
            offline |= source.LastListingFailed;
        }

        var mine = installed.All;
        var system = await Task.Run(inventory.Scan, ct).ConfigureAwait(false);

        var all = packages.Select(package => package.Code)
            .Concat(mine.Select(entry => entry.Code))
            .Concat(system.Select(entry => codes.Normalize(entry.Language.Canonical)).OfType<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase);

        var languages = all.Select(code => Language(code, packages, mine, system)).ToList();
        return new DictionaryCatalog(languages, offline);
    }

    public async Task<OperationResult<bool>> InstallAsync(
        DictionaryPackage package, IProgress<DictionaryProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var source = _sources.FirstOrDefault(candidate => candidate.Id == package.SourceId && candidate.Part == package.Part);
        if (source is null) return OperationResult<bool>.Fail(Diagnostic.Error("DICT001", $"Unknown dictionary source '{package.SourceId}'.").With(package.SourceId));

        try
        {
            var files = await source.FetchAsync(package, progress, ct).ConfigureAwait(false);
            installed.Install(package, files);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or JsonException
            || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Could not install the {Part} dictionary {Code}", package.Part, package.Code);
            return OperationResult<bool>.Fail(Diagnostic.Error("DICT002", ex.Message).With(package.Code, ex.Message));
        }

        logger.LogInformation("Installed the {Part} dictionary {Code} from {Source}", package.Part, package.Code, package.SourceId);
        changes.Announce();
        return OperationResult<bool>.Ok(true);
    }

    public OperationResult<bool> Remove(string code, DictionaryPart part)
    {
        try
        {
            if (!installed.Remove(code, part))
                return OperationResult<bool>.Fail(Diagnostic.Error("DICT003", $"The {part} dictionary '{code}' is not installed.").With(code));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not remove the {Part} dictionary {Code}", part, code);
            return OperationResult<bool>.Fail(Diagnostic.Error("DICT004", ex.Message).With(code, ex.Message));
        }

        logger.LogInformation("Removed the {Part} dictionary {Code}", part, code);
        changes.Announce();
        return OperationResult<bool>.Ok(true);
    }

    private DictionaryLanguage Language(
        string code, List<DictionaryPackage> packages, IReadOnlyList<InstalledDictionary> mine, IReadOnlyList<SystemDictionary> system)
    {
        var language = codes.Language(code);
        var root = codes.Base(code);
        string[] scope = root == code ? [code] : [code, root];

        var states = Parts.Select(part =>
        {
            var alternatives = scope
                .Select(target => ranking.Order(packages.Where(candidate => candidate.Part == part && Same(candidate.Code, target)), Priority))
                .FirstOrDefault(candidates => candidates.Count > 0) ?? [];
            var entry = scope
                .Select(target => mine.FirstOrDefault(candidate => candidate.Part == part && Same(candidate.Code, target)))
                .FirstOrDefault(candidate => candidate is not null);
            var package = entry is null
                ? alternatives.FirstOrDefault()
                : alternatives.FirstOrDefault(candidate => candidate.SourceId == entry.SourceId && Same(candidate.Code, entry.Code)) ?? alternatives.FirstOrDefault();
            var onSystem = system.Any(candidate => candidate.Part == part
                && codes.Normalize(candidate.Language.Canonical) is { } found && scope.Any(target => Same(found, target)));

            var status = entry is not null
                ? package is not null && package.SourceId == entry.SourceId && Same(package.Code, entry.Code) && package.Version != entry.Version
                    ? DictionaryStatus.UpdateAvailable
                    : DictionaryStatus.Installed
                : onSystem ? DictionaryStatus.OnSystem
                : package is not null ? DictionaryStatus.Available
                : DictionaryStatus.Unavailable;

            var inherited = entry is not null ? !Same(entry.Code, code) : package is not null && !Same(package.Code, code);

            return new DictionaryPartState(part, status, package, entry) { Alternatives = alternatives, Inherited = inherited };
        }).ToList();

        return new DictionaryLanguage(code, language, states) { Base = root };
    }

    private bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private int Priority(string sourceId) =>
        _sources.FirstOrDefault(source => source.Id == sourceId)?.Priority ?? int.MaxValue;
}
