using PulseOffice.Document.Versions;
using System.Globalization;
using System.Xml.Linq;

namespace PulseOffice.Formats.Odf;

public sealed class OdfVersionMapper
{
    private const string Folder = "Versions/";
    private const string ListPath = Folder + "VersionList.xml";

    private static readonly XNamespace Dc = OdfPackage.Namespaces.Dc;
    private static readonly XNamespace VersionList = OdfPackage.Namespaces.VersionList;

    public IReadOnlyList<(string Path, string MediaType)> Write(
        OdfPackage package, DocumentVersionCollection versions, string mimeType)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(versions);

        if (versions.Count == 0) return [];

        var list = new XElement(VersionList + "version-list",
            new XAttribute(XNamespace.Xmlns + "VL", VersionList),
            new XAttribute(XNamespace.Xmlns + "dc", Dc));

        var entries = new List<(string Path, string MediaType)> { (ListPath, "text/xml") };

        foreach (var version in versions.All)
        {
            if (!IsEntryName(version.Name)) continue;
            package.WriteBytes(Folder + version.Name, version.Content);
            entries.Add((Folder + version.Name, mimeType));

            list.Add(new XElement(VersionList + "version-entry",
                new XAttribute(VersionList + "title", version.Name),
                new XAttribute(VersionList + "comment", version.Comment),
                new XAttribute(VersionList + "creator", version.Author ?? string.Empty),
                new XAttribute(Dc + "date-time",
                    version.Created.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))));
        }

        package.WriteXml(ListPath, new XDocument(list));

        return entries;
    }

    public void Read(OdfPackage package, DocumentVersionCollection versions)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(versions);

        if (package.ReadXml(ListPath)?.Root is not { } list) return;

        foreach (var entry in list.Elements(VersionList + "version-entry"))
        {
            var title = (string?)entry.Attribute(VersionList + "title");
            if (title is null || !IsEntryName(title)) continue;

            if (package.ReadBytes(Folder + title) is not { } content) continue;

            var author = (string?)entry.Attribute(VersionList + "creator");

            versions.Adopt(new DocumentVersion
            {
                Name = title,
                Comment = (string?)entry.Attribute(VersionList + "comment") ?? string.Empty,
                Author = string.IsNullOrEmpty(author) ? null : author,
                Created = Moment(entry),
                Content = content,
            });
        }
    }

    private bool IsEntryName(string? name) =>
        name is { Length: > 0 } && name != "." && name != ".." && name.IndexOfAny(['/', '\\', ':']) < 0;

    private DateTimeOffset Moment(XElement entry) =>
        DateTimeOffset.TryParse((string?)entry.Attribute(Dc + "date-time"),
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment)
            ? moment
            : DateTimeOffset.Now;
}
