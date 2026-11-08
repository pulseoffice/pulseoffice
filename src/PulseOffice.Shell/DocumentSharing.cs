using System.Globalization;
using System.Text.Json;

namespace PulseOffice.Shell;

public sealed record ShareClaim(string User, string Machine, DateTimeOffset Taken)
{
    public bool IsMine => string.Equals(User, Environment.UserName, StringComparison.Ordinal)
        && string.Equals(Machine, Environment.MachineName, StringComparison.Ordinal);
}

public interface IDocumentSharing
{
    ShareClaim? Holder(string documentPath);

    bool Claim(string documentPath);

    bool Release(string documentPath);
}

public sealed class LockFileFormat
{
    public const int MaximumLength = 4096;

    private const string Moment = "dd.MM.yyyy HH:mm";

    public string Write(ShareClaim claim) =>
        $",{Escape(claim.User)},{Escape(claim.Machine)},{claim.Taken.ToString(Moment, CultureInfo.InvariantCulture)},;";

    public ShareClaim? Read(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (text.TrimStart().StartsWith('{'))
        {
            try
            {
                return JsonSerializer.Deserialize<ShareClaim>(text);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        var fields = Fields(text);
        if (fields.Count < 4) return null;

        var taken = DateTimeOffset.TryParseExact(fields[3], Moment, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal, out var parsed) ? parsed : DateTimeOffset.MinValue;
        var user = fields[1].Length > 0 ? fields[1] : fields[0];
        return new ShareClaim(user, fields[2], taken);
    }

    private List<string> Fields(string text)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '\\' && index + 1 < text.Length)
            {
                current.Append(text[++index]);
                continue;
            }

            if (character is ',' or ';')
            {
                fields.Add(current.ToString());
                current.Clear();
                if (character == ';') break;
                continue;
            }

            current.Append(character);
        }

        return fields;
    }

    private string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(",", "\\,", StringComparison.Ordinal).Replace(";", "\\;", StringComparison.Ordinal);
}

public sealed class FileDocumentSharing : IDocumentSharing
{
    private static readonly TimeSpan Abandoned = TimeSpan.FromMinutes(1);

    private readonly LockFileFormat _format = new();

    public ShareClaim? Holder(string documentPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentPath);

        var path = LockPathOf(documentPath);

        if (!File.Exists(path)) return null;

        try
        {
            return _format.Read(ReadHead(path));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public bool Claim(string documentPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentPath);

        if (Holder(documentPath) is { } held) return held.IsMine;

        var lockPath = LockPathOf(documentPath);
        try
        {
            if (File.Exists(lockPath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(lockPath) > Abandoned)
                File.Delete(lockPath);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        var claim = new ShareClaim(Environment.UserName, Environment.MachineName, DateTimeOffset.Now);

        try
        {
            using var stream = new FileStream(
                LockPathOf(documentPath), FileMode.CreateNew, FileAccess.Write, FileShare.None);

            using var writer = new StreamWriter(stream);

            writer.Write(_format.Write(claim));

            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool Release(string documentPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentPath);

        if (Holder(documentPath) is not { IsMine: true }) return false;

        try
        {
            File.Delete(LockPathOf(documentPath));
            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string ReadHead(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var buffer = new char[LockFileFormat.MaximumLength];
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        return new string(buffer, 0, read);
    }

    public static string LockPathOf(string documentPath) =>
        Path.Combine(
            Path.GetDirectoryName(documentPath) ?? string.Empty,
            string.Create(CultureInfo.InvariantCulture, $".~lock.{Path.GetFileName(documentPath)}#"));
}
