using System.IO.Compression;

namespace PulseOffice.Text.Spelling.Hunspell.Catalog;

public sealed record DictionaryArchiveLimits(int Entries, long EntryBytes, long TotalBytes)
{
    public static readonly DictionaryArchiveLimits Default = new(4096, 192L * 1024 * 1024, 384L * 1024 * 1024);
}

public interface IDictionaryArchive
{
    IReadOnlyDictionary<string, byte[]> Extract(byte[] archive, Func<string, bool> wanted);
}

public sealed class DictionaryArchive(DictionaryArchiveLimits limits) : IDictionaryArchive
{
    public DictionaryArchive()
        : this(DictionaryArchiveLimits.Default)
    {
    }

    public IReadOnlyDictionary<string, byte[]> Extract(byte[] archive, Func<string, bool> wanted)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(wanted);

        var extracted = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long total = 0;

        try
        {
            using var zip = new ZipArchive(new MemoryStream(archive, writable: false), ZipArchiveMode.Read);
            if (zip.Entries.Count > limits.Entries) throw new InvalidDataException("The dictionary archive has too many entries.");

            foreach (var entry in zip.Entries)
            {
                if (Normalized(entry.FullName) is not { } name || !wanted(name) || extracted.ContainsKey(name)) continue;

                using var stream = entry.Open();
                using var bounded = new BoundedReadStream(stream, Math.Min(limits.EntryBytes, limits.TotalBytes - total));
                using var buffer = new MemoryStream();
                bounded.CopyTo(buffer);
                total += buffer.Length;
                extracted[name] = buffer.ToArray();
            }
        }
        catch (Exception ex) when (ex is not InvalidDataException and (IOException or ArgumentException or NotSupportedException))
        {
            throw new InvalidDataException("The dictionary archive cannot be read.", ex);
        }

        return extracted;
    }

    private string? Normalized(string raw)
    {
        var name = raw.Replace('\\', '/');
        if (name.Length == 0 || name.EndsWith('/') || name.StartsWith('/') || name.Contains(':', StringComparison.Ordinal)) return null;

        var segments = name.Split('/');
        return segments.Any(segment => segment is "" or "." or "..") ? null : name;
    }
}

public sealed class BoundedReadStream(Stream inner, long limit) : Stream
{
    private long _read;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _read;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        _read += read;
        if (_read > limit) throw new InvalidDataException("The dictionary data is larger than allowed.");
        return read;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
