using System.IO.Compression;
using System.Xml;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.Intake;

public interface IIncomingDocumentInspector
{
    Diagnostic? Inspect(Stream stream);
}

public sealed class IncomingDocumentInspector(IncomingDocumentLimits limits) : IIncomingDocumentInspector
{
    public IncomingDocumentInspector() : this(new IncomingDocumentLimits())
    {
    }

    public Diagnostic? Inspect(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek) return null;

        var start = stream.Position;
        try
        {
            return IsZip(stream) ? InspectArchive(stream) : InspectXml(stream);
        }
        finally
        {
            stream.Position = start;
        }
    }

    private bool IsZip(Stream stream)
    {
        var start = stream.Position;
        Span<byte> header = stackalloc byte[4];
        var read = stream.ReadAtLeast(header, 4, throwOnEndOfStream: false);
        stream.Position = start;
        return read == 4 && header[0] == 'P' && header[1] == 'K' && header[2] == 3 && header[3] == 4;
    }

    private Diagnostic? InspectArchive(Stream stream)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return null;
        }

        using (archive)
        {
            IReadOnlyList<ZipArchiveEntry> entries;
            try
            {
                entries = archive.Entries;
            }
            catch (InvalidDataException)
            {
                return null;
            }

            if (entries.Count > limits.MaximumEntries) return Expansion();

            long total = 0;
            foreach (var entry in entries)
            {
                if (entry.FullName.EndsWith('/')) continue;

                long expanded;
                try
                {
                    expanded = Expand(entry, limits.MaximumExpandedBytes - total);
                }
                catch (InvalidDataException)
                {
                    continue;
                }

                if (expanded < 0) return Expansion();
                total += expanded;
                if (expanded > limits.MaximumEntryBytes) return Expansion();
                if (expanded > limits.CompressionRatioCheckThreshold && expanded > Math.Max(1, entry.CompressedLength) * limits.MaximumCompressionRatio)
                    return Expansion();

                if (!IsXmlEntry(entry.FullName)) continue;

                try
                {
                    using var content = entry.Open();
                    if (InspectXml(content) is { } problem) return problem;
                }
                catch (InvalidDataException)
                {
                }
            }
        }

        return null;
    }

    private long Expand(ZipArchiveEntry entry, long allowance)
    {
        using var content = entry.Open();
        var buffer = new byte[81920];
        long expanded = 0;
        int read;
        while ((read = content.Read(buffer)) > 0)
        {
            expanded += read;
            if (expanded > allowance || expanded > limits.MaximumEntryBytes) return -1;
        }

        return expanded;
    }

    private bool IsXmlEntry(string name) =>
        name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".vml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

    private Diagnostic? InspectXml(Stream stream)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
            MaxCharactersFromEntities = limits.MaximumCharactersFromEntities,
            IgnoreWhitespace = true,
            IgnoreComments = true,
            CloseInput = false,
        };

        try
        {
            using var reader = XmlReader.Create(stream, settings);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.DocumentType && DeclaresEntities(reader.Value))
                    return Diagnostic.Error("FMT013", "The file declares a document type or entities, which are not allowed in documents.");
                if (reader.Depth > limits.MaximumElementDepth)
                    return Diagnostic.Error("FMT011", $"The file nests elements more than {limits.MaximumElementDepth} levels deep.").With(limits.MaximumElementDepth);
            }
        }
        catch (XmlException)
        {
        }

        return null;
    }

    private bool DeclaresEntities(string internalSubset) =>
        internalSubset.Contains("<!ENTITY", StringComparison.OrdinalIgnoreCase);

    private Diagnostic Expansion() =>
        Diagnostic.Error("FMT012", "The compressed file expands beyond the safe size limits.");
}
