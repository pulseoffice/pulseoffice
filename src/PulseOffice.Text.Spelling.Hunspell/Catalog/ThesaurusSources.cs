using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PulseOffice.Text.Spelling.Dictionaries;

namespace PulseOffice.Text.Spelling.Hunspell.Catalog;

public abstract class ThesaurusSource(IDictionaryHttp http, IDictionaryCodes codes) : IDictionarySource
{
    protected IDictionaryHttp Http => http;

    public abstract string Id { get; }

    public DictionaryPart Part => DictionaryPart.Thesaurus;

    public virtual int Priority => 10;

    protected abstract DictionaryEndpoint Endpoint { get; }

    public async Task<IReadOnlyList<DictionaryPackage>> ListAsync(CancellationToken ct = default) =>
        Endpoint.Enabled ? await DescribeAsync(ct).ConfigureAwait(false) : [];

    public async Task<IReadOnlyList<DictionaryFileContent>> FetchAsync(
        DictionaryPackage package, IProgress<DictionaryProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var file = package.Files.FirstOrDefault() ?? throw new InvalidDataException("The thesaurus has no file to download.");
        progress?.Report(new DictionaryProgress(0, 2, 0));
        var data = await http.BytesAsync(file.Location, ct).ConfigureAwait(false);
        progress?.Report(new DictionaryProgress(1, 2, data.Length));

        var files = await Task.Run(() => Convert(data), ct).ConfigureAwait(false);
        progress?.Report(new DictionaryProgress(2, 2, data.Length));

        var stem = codes.FileStem(package.Code);
        return
        [
            new DictionaryFileContent($"th_{stem}_v2.dat", files.Data),
            new DictionaryFileContent($"th_{stem}_v2.idx", files.Index),
            new DictionaryFileContent($"th_{stem}.license.txt", License(package, files)),
        ];
    }

    protected abstract Task<IReadOnlyList<DictionaryPackage>> DescribeAsync(CancellationToken ct);

    protected abstract ThesaurusDownload Convert(byte[] data);

    protected bool Inside(Uri candidate) =>
        candidate.Scheme == Uri.UriSchemeHttps && candidate.ToString().StartsWith(Endpoint.Files.ToString(), StringComparison.Ordinal);

    private byte[] License(DictionaryPackage package, ThesaurusDownload files) =>
        files.Notice ?? Encoding.UTF8.GetBytes($"{package.License.Name}\n{package.License.Url}\n{package.Homepage}\n");
}

public sealed record ThesaurusDownload(byte[] Data, byte[] Index, byte[]? Notice);

public sealed class WordNetThesaurusSource(
    IDictionaryHttp http,
    IDictionaryCodes codes,
    IDictionaryLicenseClassifier licenses,
    IWordNetThesaurusConverter converter,
    IMyThesWriter writer,
    DictionaryServiceOptions options) : ThesaurusSource(http, codes)
{
    public const string SourceId = "oewn";

    public const long ExpandedMaximum = 512L * 1024 * 1024;

    private readonly Regex _assetName = new(@"^english-wordnet-\d{4}\.xml\.gz$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public override string Id => SourceId;

    protected override DictionaryEndpoint Endpoint => options.WordNet;

    protected override async Task<IReadOnlyList<DictionaryPackage>> DescribeAsync(CancellationToken ct)
    {
        using var document = await Http.JsonAsync(Endpoint.Listing, ct).ConfigureAwait(false);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The WordNet release has no assets.");

        var tag = root.TryGetProperty("tag_name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() ?? string.Empty : string.Empty;

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object
                || !asset.TryGetProperty("name", out var fileName) || fileName.ValueKind != JsonValueKind.String
                || !_assetName.IsMatch(fileName.GetString() ?? string.Empty)
                || !asset.TryGetProperty("browser_download_url", out var address) || address.ValueKind != JsonValueKind.String
                || !Uri.TryCreate(address.GetString(), UriKind.Absolute, out var location) || !Inside(location))
                continue;

            return
            [
                new DictionaryPackage(
                    Id,
                    "en",
                    Part,
                    tag,
                    licenses.FromSpdx("CC-BY-4.0", "https://creativecommons.org/licenses/by/4.0/"),
                    [new DictionaryRemoteFile("wordnet", location)],
                    $"Open English WordNet {tag}".Trim(),
                    "https://en-word.net"),
            ];
        }

        return [];
    }

    protected override ThesaurusDownload Convert(byte[] data)
    {
        using var expanded = new BoundedReadStream(new GZipStream(new MemoryStream(data, writable: false), CompressionMode.Decompress), ExpandedMaximum);
        var files = writer.Write(converter.Convert(expanded));
        return new ThesaurusDownload(files.Data, files.Index, null);
    }

}

public sealed record OpenThesaurusEdition(string Code, string File);

public sealed class OpenThesaurusSource(
    IDictionaryHttp http,
    IDictionaryCodes codes,
    IDictionaryLicenseClassifier licenses,
    IDictionaryArchive archive,
    DictionaryServiceOptions options) : ThesaurusSource(http, codes)
{
    public const string SourceId = "openthesaurus";

    private static readonly OpenThesaurusEdition[] Editions =
    [
        new("de", "Deutscher-Thesaurus.oxt"),
        new("de-CH", "Schweizer-Thesaurus.oxt"),
    ];

    public override string Id => SourceId;

    protected override DictionaryEndpoint Endpoint => options.OpenThesaurus;

    protected override async Task<IReadOnlyList<DictionaryPackage>> DescribeAsync(CancellationToken ct)
    {
        var packages = new List<DictionaryPackage>();

        foreach (var edition in Editions)
        {
            var location = new Uri(Endpoint.Files, edition.File);
            if (!Inside(location)) continue;
            if (await Http.TryVersionAsync(location, ct).ConfigureAwait(false) is not { } version) continue;

            packages.Add(new DictionaryPackage(
                Id,
                edition.Code,
                Part,
                version,
                licenses.FromSpdx("CC-BY-SA-4.0 OR LGPL-2.1-or-later", "https://www.openthesaurus.de/about/download"),
                [new DictionaryRemoteFile("oxt", location)],
                "OpenThesaurus",
                "https://www.openthesaurus.de"));
        }

        return packages;
    }

    protected override ThesaurusDownload Convert(byte[] data)
    {
        var entries = archive.Extract(data, name =>
            name.Equals("README.txt", StringComparison.OrdinalIgnoreCase)
            || (name.StartsWith("th_", StringComparison.OrdinalIgnoreCase)
                && (name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".idx", StringComparison.OrdinalIgnoreCase))));

        var thesaurus = entries.Keys.FirstOrDefault(name => name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase));
        if (thesaurus is null || entries[thesaurus].Length == 0) throw new InvalidDataException("The OpenThesaurus package has no thesaurus.");

        var index = entries.TryGetValue(Path.ChangeExtension(thesaurus, ".idx"), out var idx) ? idx : [];
        entries.TryGetValue("README.txt", out var notice);
        return new ThesaurusDownload(entries[thesaurus], index, notice);
    }
}
