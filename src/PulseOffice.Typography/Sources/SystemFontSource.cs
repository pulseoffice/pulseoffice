using PulseOffice.Document.Fonts;
using PulseOffice.Typography.Catalog;
using PulseOffice.Typography.Licensing;
using PulseOffice.Typography.OpenType;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Platform;

namespace PulseOffice.Typography.Sources;

public sealed class SystemFontSource(
    ISystemFontDirectories directories,
    IInstalledFontNames installed,
    IOpenTypeReader reader,
    IFontFileExtractor extractor,
    IFontLicenseClassifier licenses,
    IFontCategoryClassifier categories,
    IFontScriptCoverage scripts,
    IClassicFaceSelector faces) : IFontSource
{
    public const string SourceId = "system";

    private static readonly string[] Extensions = [".ttf", ".otf", ".ttc", ".otc"];

    public string Id => SourceId;

    public FontOrigin Origin => FontOrigin.System;

    public Task<IReadOnlyList<FontFamilyInfo>> ListAsync(CancellationToken ct = default) =>
        Task.Run(() => Scan(ct), ct);

    public async Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> FetchAsync(
        FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(family);
        var selected = faces.Select(family.Faces);
        if (selected.Count == 0)
            return OperationResult<IReadOnlyList<EmbeddedFontFile>>.Fail(
                Diagnostic.Error("FONT010", $"No font file was found for '{family.Family}'.").With(family.Family));

        var files = new List<EmbeddedFontFile>();
        long bytes = 0;
        foreach (var face in selected)
        {
            ct.ThrowIfCancellationRequested();
            byte[] font;
            try
            {
                var data = await File.ReadAllBytesAsync(face.Location, ct).ConfigureAwait(false);
                font = extractor.Extract(data, face.CollectionIndex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            {
                return OperationResult<IReadOnlyList<EmbeddedFontFile>>.Fail(Diagnostic.Error("FONT011", ex.Message).With(ex.Message));
            }

            bytes += font.Length;
            files.Add(new EmbeddedFontFile(family.Family, face.Weight, face.Italic, FileName(family.Family, face), font));
            progress?.Report(new FontProgress(files.Count, selected.Count, bytes));
        }

        return OperationResult<IReadOnlyList<EmbeddedFontFile>>.Ok(files);
    }

    private string FileName(string family, FontFaceRef face) =>
        $"{new string([.. family.Where(char.IsLetterOrDigit)])}-{face.Weight}{(face.Italic ? "i" : string.Empty)}.ttf";

    private IReadOnlyList<FontFamilyInfo> Scan(CancellationToken ct)
    {
        var found = new Dictionary<string, List<(OpenTypeFace Face, string Path)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Files(ct))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess);
                foreach (var face in reader.Read(stream))
                {
                    if (!found.TryGetValue(face.Family, out var list)) found[face.Family] = list = [];
                    list.Add((face, file));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or EndOfStreamException)
            {
            }
        }

        var families = found.Where(pair => !Hidden(pair.Key)).Select(pair => Describe(pair.Key, pair.Value)).ToList();
        var known = new HashSet<string>(families.Select(family => family.Family), StringComparer.OrdinalIgnoreCase);

        foreach (var name in installed.Families.Where(name => !string.IsNullOrWhiteSpace(name) && !Hidden(name) && known.Add(name)))
            families.Add(new FontFamilyInfo { Family = name, SourceId = Id, Origin = Origin, Category = categories.Classify(new OpenTypeFace { Family = name }) });

        return families;
    }

    private bool Hidden(string family) => family.StartsWith('.') || family.StartsWith('#');

    private FontFamilyInfo Describe(string family, List<(OpenTypeFace Face, string Path)> members)
    {
        var regular = members
            .OrderBy(member => member.Face.Italic)
            .ThenBy(member => Math.Abs(member.Face.Weight - 400))
            .First().Face;

        return new FontFamilyInfo
        {
            Family = family,
            SourceId = Id,
            Origin = Origin,
            Category = categories.Classify(regular),
            Faces = [.. members.Select(member => new FontFaceRef(member.Face.Weight, member.Face.Italic, member.Path, member.Face.CollectionIndex))],
            Scripts = [.. members.SelectMany(member => scripts.FromUnicodeRanges(member.Face.UnicodeRanges)).Distinct().Order()],
            License = licenses.FromDescription(regular.LicenseDescription, regular.LicenseUrl, regular.Copyright),
            Embedding = regular.Embedding,
            Designer = regular.Designer,
        };
    }

    private IEnumerable<string> Files(CancellationToken ct)
    {
        foreach (var directory in directories.Directories)
        {
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFiles(directory, "*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = 0,
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                if (Extensions.Contains(Path.GetExtension(entry), StringComparer.OrdinalIgnoreCase)) yield return entry;
            }
        }
    }
}
