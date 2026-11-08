using System.IO.Compression;
using System.Xml;

namespace PulseOffice.Text.Spelling;

public interface IAutoCorrectListReader
{
    AutoCorrectLists? Read(string path);
}

public sealed class AcorListReader(AutoCorrectEntryNormalizer normalizer) : IAutoCorrectListReader
{
    private const string BlockNamespace = "http://openoffice.org/2001/block-list";
    private const long MaximumFileBytes = 16L * 1024 * 1024;
    private const long MaximumPartBytes = 32L * 1024 * 1024;
    private const int MaximumEntries = 100_000;

    public AutoCorrectLists? Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaximumFileBytes) return null;

            using var archive = ZipFile.OpenRead(path);

            return normalizer.Lists(
                [.. Blocks(archive, "DocumentList.xml")],
                [.. Blocks(archive, "SentenceExceptList.xml").Select(block => block.Shortcut)],
                [.. Blocks(archive, "WordExceptList.xml").Select(block => block.Shortcut)]);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or XmlException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private IEnumerable<(string? Shortcut, string? Value)> Blocks(ZipArchive archive, string part)
    {
        var entry = archive.GetEntry(part);
        if (entry is null || entry.Length > MaximumPartBytes) yield break;

        using var stream = entry.Open();
        using var xml = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumPartBytes,
            IgnoreComments = true,
        });

        var count = 0;
        while (xml.Read())
        {
            if (xml.NodeType != XmlNodeType.Element || xml.LocalName != "block" || xml.NamespaceURI != BlockNamespace) continue;
            if (++count > MaximumEntries) yield break;

            yield return (xml.GetAttribute("abbreviated-name", BlockNamespace), xml.GetAttribute("name", BlockNamespace));
        }
    }
}
