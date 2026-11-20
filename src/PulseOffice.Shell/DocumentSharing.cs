using System.Globalization;
using System.Text.Json;

namespace PulseOffice.Shell;

public sealed record ShareClaim(string User, string Machine, DateTimeOffset Taken, RecoveryOwner? Process = null)
{
    public bool IsMine { get; init; }

    public bool IsStale { get; init; }

    public bool SameAccount => string.Equals(User, Environment.UserName, StringComparison.Ordinal)
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

    private const string ProcessPrefix = "pulseoffice-process:";

    public string Write(ShareClaim claim) =>
        $",{Escape(claim.User)},{Escape(claim.Machine)},{claim.Taken.ToString(Moment, CultureInfo.InvariantCulture)},{ProcessField(claim.Process)};";

    private string ProcessField(RecoveryOwner? process) =>
        process is null ? string.Empty
        : string.Create(CultureInfo.InvariantCulture, $"{ProcessPrefix}{process.ProcessId}:{process.Started.ToUnixTimeMilliseconds()}");

    private RecoveryOwner? ProcessOf(IReadOnlyList<string> fields)
    {
        if (fields.Count < 5 || !fields[4].StartsWith(ProcessPrefix, StringComparison.Ordinal)) return null;
        var parts = fields[4][ProcessPrefix.Length..].Split(':');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var started)
            || started < -62135596800000L || started > 253402300799999L) return null;
        return new RecoveryOwner(id, DateTimeOffset.FromUnixTimeMilliseconds(started));
    }

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
        return new ShareClaim(user, fields[2], taken, ProcessOf(fields));
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

public sealed class FileDocumentSharing(IRecoveryOwnership? ownership = null) : IDocumentSharing
{
    private static readonly TimeSpan Abandoned = TimeSpan.FromMinutes(1);

    private readonly LockFileFormat _format = new();

    private readonly IRecoveryOwnership _ownership = ownership ?? new ProcessRecoveryOwnership();

    private bool Owned(ShareClaim claim)
    {
        if (!claim.SameAccount || claim.Process is not { } process) return false;
        var current = _ownership.Current;
        return process.ProcessId == current.ProcessId
            ? (process.Started - current.Started).Duration() <= TimeSpan.FromSeconds(2)
            : !_ownership.IsAlive(process);
    }

    private bool HeldByThisProcess(ShareClaim claim) =>
        claim.Process is { } process
        && process.ProcessId == _ownership.Current.ProcessId
        && (process.Started - _ownership.Current.Started).Duration() <= TimeSpan.FromSeconds(2);

    public ShareClaim? Holder(string documentPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentPath);

        var path = LockPathOf(documentPath);

        if (!File.Exists(path)) return null;

        try
        {
            return _format.Read(ReadHead(path)) is { } claim ? claim with { IsMine = Owned(claim), IsStale = Owned(claim) && !HeldByThisProcess(claim) } : null;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public bool Claim(string documentPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentPath);

        var held = Holder(documentPath);
        if (held is { IsMine: false }) return false;
        if (held is not null && HeldByThisProcess(held)) return true;

        var lockPath = LockPathOf(documentPath);
        try
        {
            if (held is not null
                || (File.Exists(lockPath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(lockPath) > Abandoned))
                File.Delete(lockPath);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        var claim = new ShareClaim(Environment.UserName, Environment.MachineName, DateTimeOffset.Now, _ownership.Current);

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
