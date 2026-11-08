using PulseOffice.Document.Fonts;
using PulseOffice.Typography.Catalog;
using PulseOffice.Typography.Licensing;
using PulseOffice.Typography.OpenType;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Typography.Sources;

public interface IActiveFontDocument
{
    IFontDocument? Current { get; }

    IReadOnlyCollection<string> UsedFamilies { get; }
}

public sealed class DocumentFontSource(
    IActiveFontDocument active,
    IOpenTypeReader reader,
    IFontLicenseClassifier licenses,
    IFontCategoryClassifier categories,
    IFontScriptCoverage scripts) : IFontSource
{
    public const string SourceId = "document";

    public string Id => SourceId;

    public FontOrigin Origin => FontOrigin.Document;

    public bool IsVolatile => true;

    public Task<IReadOnlyList<FontFamilyInfo>> ListAsync(CancellationToken ct = default)
    {
        var fonts = active.Current?.Fonts ?? DocumentFonts.Empty;
        var families = new List<FontFamilyInfo>();

        foreach (var group in fonts.Files.GroupBy(file => file.Family, StringComparer.OrdinalIgnoreCase))
            families.Add(Describe(group.Key, [.. group]));

        var embedded = new HashSet<string>(families.Select(family => family.Family), StringComparer.OrdinalIgnoreCase);
        foreach (var used in active.UsedFamilies.Where(name => !string.IsNullOrWhiteSpace(name) && embedded.Add(name)))
            families.Add(new FontFamilyInfo { Family = used, SourceId = Id, Origin = Origin });

        return Task.FromResult<IReadOnlyList<FontFamilyInfo>>(families);
    }

    public Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> FetchAsync(
        FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(family);
        var files = (active.Current?.Fonts ?? DocumentFonts.Empty).FilesOf(family.Family).ToList();
        return Task.FromResult(files.Count > 0
            ? OperationResult<IReadOnlyList<EmbeddedFontFile>>.Ok(files)
            : OperationResult<IReadOnlyList<EmbeddedFontFile>>.Fail(Diagnostic.Error("FONT040", $"'{family.Family}' is not embedded in the document.").With(family.Family)));
    }

    private FontFamilyInfo Describe(string family, IReadOnlyList<EmbeddedFontFile> files)
    {
        OpenTypeFace? face = null;
        try
        {
            face = reader.Read(files[0].Data).FirstOrDefault();
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException)
        {
        }

        return new FontFamilyInfo
        {
            Family = family,
            SourceId = Id,
            Origin = Origin,
            Faces = [.. files.Select(file => new FontFaceRef(file.Weight, file.Italic, file.FileName))],
            Category = face is null ? FontCategory.Unknown : categories.Classify(face),
            Scripts = face is null ? [] : scripts.FromUnicodeRanges(face.UnicodeRanges),
            License = face is null ? FontLicense.Unknown : licenses.FromDescription(face.LicenseDescription, face.LicenseUrl, face.Copyright),
            Embedding = face?.Embedding ?? EmbeddingPermission.Unknown,
            Designer = face?.Designer,
        };
    }
}
