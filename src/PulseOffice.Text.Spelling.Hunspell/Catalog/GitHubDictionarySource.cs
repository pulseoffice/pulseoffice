using System.Text;
using System.Text.Json;
using PulseOffice.Text.Spelling.Dictionaries;

namespace PulseOffice.Text.Spelling.Hunspell.Catalog;

public sealed record DictionaryCandidate(string Raw, string Code, string Version, IReadOnlyDictionary<string, string> Extras);

public abstract class GitHubDictionarySource(IDictionaryHttp http, IDictionaryCodes codes, IDictionaryLicenseClassifier licenses) : IDictionarySource
{
    private const int Parallel = 6;

    protected IDictionaryHttp Http => http;

    protected IDictionaryCodes Codes => codes;

    protected IDictionaryLicenseClassifier Licenses => licenses;

    public abstract string Id { get; }

    public abstract DictionaryPart Part { get; }

    public virtual int Priority => 10;

    protected abstract DictionaryEndpoint Endpoint { get; }

    public async Task<IReadOnlyList<DictionaryPackage>> ListAsync(CancellationToken ct = default)
    {
        if (!Endpoint.Enabled) return [];

        using var document = await http.JsonAsync(Endpoint.Listing, ct).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("The dictionary listing is not an array.");

        var candidates = Candidates(document.RootElement)
            .GroupBy(candidate => candidate.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        using var gate = new SemaphoreSlim(Parallel);
        var tasks = candidates.Select(async candidate =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await DescribeAsync(candidate, ct).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        });

        var packages = await Task.WhenAll(tasks).ConfigureAwait(false);
        return [.. packages.OrderBy(package => package.Code, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<IReadOnlyList<DictionaryFileContent>> FetchAsync(
        DictionaryPackage package, IProgress<DictionaryProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var downloaded = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long bytes = 0;
        progress?.Report(new DictionaryProgress(0, package.Files.Count, 0));

        foreach (var file in package.Files)
        {
            var data = await http.BytesAsync(file.Location, ct).ConfigureAwait(false);
            bytes += data.Length;
            downloaded[file.Name] = data;
            progress?.Report(new DictionaryProgress(downloaded.Count, package.Files.Count, bytes));
        }

        return Transform(package, downloaded);
    }

    protected abstract IEnumerable<DictionaryCandidate> Candidates(JsonElement listing);

    protected abstract Task<DictionaryPackage> DescribeAsync(DictionaryCandidate candidate, CancellationToken ct);

    protected abstract IReadOnlyList<DictionaryFileContent> Transform(DictionaryPackage package, IReadOnlyDictionary<string, byte[]> files);

    protected async Task<string?> TryTextAsync(Uri uri, CancellationToken ct)
    {
        try
        {
            return await http.TryTextAsync(uri, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException
            || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }
    }

    protected string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    protected byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}

public sealed class WooormSpellingSource(
    IDictionaryHttp http, IDictionaryCodes codes, IDictionaryLicenseClassifier licenses, DictionaryServiceOptions options)
    : GitHubDictionarySource(http, codes, licenses)
{
    public const string SourceId = "wooorm";

    public override string Id => SourceId;

    public override DictionaryPart Part => DictionaryPart.Spelling;

    protected override DictionaryEndpoint Endpoint => options.Spelling;

    protected override IEnumerable<DictionaryCandidate> Candidates(JsonElement listing)
    {
        foreach (var entry in listing.EnumerateArray())
        {
            if (String(entry, "type") != "dir" || String(entry, "name") is not { } name) continue;
            if (Codes.Normalize(name) is not { } code) continue;

            yield return new DictionaryCandidate(name, code, String(entry, "sha") ?? string.Empty, new Dictionary<string, string>());
        }
    }

    protected override async Task<DictionaryPackage> DescribeAsync(DictionaryCandidate candidate, CancellationToken ct)
    {
        var folder = new Uri(Endpoint.Files, $"dictionaries/{Uri.EscapeDataString(candidate.Raw)}/");
        var manifest = await TryTextAsync(new Uri(folder, "package.json"), ct).ConfigureAwait(false);
        string? spdx = null;
        string? homepage = null;

        if (manifest is not null)
        {
            try
            {
                using var document = JsonDocument.Parse(manifest);
                spdx = String(document.RootElement, "license");
                homepage = String(document.RootElement, "repository");
            }
            catch (JsonException)
            {
            }
        }

        return new DictionaryPackage(
            Id,
            candidate.Code,
            Part,
            candidate.Version,
            Licenses.FromSpdx(spdx, new Uri(folder, "license").ToString()),
            [
                new DictionaryRemoteFile("aff", new Uri(folder, "index.aff")),
                new DictionaryRemoteFile("dic", new Uri(folder, "index.dic")),
                new DictionaryRemoteFile("license", new Uri(folder, "license")),
            ],
            "wooorm/dictionaries",
            homepage);
    }

    protected override IReadOnlyList<DictionaryFileContent> Transform(DictionaryPackage package, IReadOnlyDictionary<string, byte[]> files)
    {
        if (!files.TryGetValue("aff", out var affix) || !files.TryGetValue("dic", out var words) || affix.Length == 0 || words.Length == 0)
            throw new InvalidDataException("The dictionary download is incomplete.");

        var stem = Codes.FileStem(package.Code);
        var contents = new List<DictionaryFileContent> { new($"{stem}.aff", affix), new($"{stem}.dic", words) };
        if (files.TryGetValue("license", out var license)) contents.Add(new DictionaryFileContent($"{stem}.license.txt", license));
        return contents;
    }
}

public sealed class TexHyphenationSource(
    IDictionaryHttp http,
    IDictionaryCodes codes,
    IDictionaryLicenseClassifier licenses,
    DictionaryServiceOptions options,
    ITexHyphenationConverter converter,
    ITexLicenseReader licenseReader)
    : GitHubDictionarySource(http, codes, licenses)
{
    public const string SourceId = "hyph-utf8";

    private const string Prefix = "hyph-";
    private const string PatternSuffix = ".pat.txt";
    private const string ExceptionSuffix = ".hyp.txt";

    public override string Id => SourceId;

    public override DictionaryPart Part => DictionaryPart.Hyphenation;

    protected override DictionaryEndpoint Endpoint => options.Hyphenation;

    protected override IEnumerable<DictionaryCandidate> Candidates(JsonElement listing)
    {
        var entries = listing.EnumerateArray()
            .Select(entry => (Name: String(entry, "name"), Sha: String(entry, "sha") ?? string.Empty))
            .Where(entry => entry.Name is not null && entry.Name.StartsWith(Prefix, StringComparison.Ordinal))
            .ToDictionary(entry => entry.Name!, entry => entry.Sha, StringComparer.Ordinal);

        foreach (var (name, sha) in entries)
        {
            if (!name.EndsWith(PatternSuffix, StringComparison.Ordinal)) continue;

            var raw = name[Prefix.Length..^PatternSuffix.Length];
            if (Codes.Normalize(raw) is not { } code) continue;

            var extras = new Dictionary<string, string>(StringComparer.Ordinal);
            var version = sha;
            if (entries.TryGetValue(Prefix + raw + ExceptionSuffix, out var exceptionSha))
            {
                extras["hyp"] = Prefix + raw + ExceptionSuffix;
                version = $"{sha}+{exceptionSha}";
            }

            yield return new DictionaryCandidate(raw, code, version, extras);
        }
    }

    protected override async Task<DictionaryPackage> DescribeAsync(DictionaryCandidate candidate, CancellationToken ct)
    {
        var header = await TryTextAsync(new Uri(Endpoint.Files, $"tex/{Prefix}{candidate.Raw}.tex"), ct).ConfigureAwait(false);
        var spdx = header is null ? null : licenseReader.Read(header);

        var files = new List<DictionaryRemoteFile> { new("pat", new Uri(Endpoint.Files, $"txt/{Prefix}{candidate.Raw}{PatternSuffix}")) };
        if (candidate.Extras.TryGetValue("hyp", out var exceptions)) files.Add(new DictionaryRemoteFile("hyp", new Uri(Endpoint.Files, $"txt/{exceptions}")));

        return new DictionaryPackage(
            Id,
            candidate.Code,
            Part,
            candidate.Version,
            Licenses.FromSpdx(spdx, new Uri(Endpoint.Files, $"tex/{Prefix}{candidate.Raw}.tex").ToString()),
            files,
            "hyphenation/tex-hyphen (hyph-utf8)",
            "https://www.hyphenation.org/tex");
    }

    protected override IReadOnlyList<DictionaryFileContent> Transform(DictionaryPackage package, IReadOnlyDictionary<string, byte[]> files)
    {
        if (!files.TryGetValue("pat", out var patterns) || patterns.Length == 0)
            throw new InvalidDataException("The hyphenation download is incomplete.");

        var exceptions = files.TryGetValue("hyp", out var hyp) ? Encoding.UTF8.GetString(hyp) : null;
        var converted = converter.Convert(Encoding.UTF8.GetString(patterns), exceptions);
        var stem = Codes.FileStem(package.Code);

        return
        [
            new DictionaryFileContent($"hyph_{stem}.dic", Utf8(converted)),
            new DictionaryFileContent($"hyph_{stem}.license.txt", Utf8($"{package.License.Name}\n{package.License.Url}\n{package.Homepage}\n")),
        ];
    }
}
