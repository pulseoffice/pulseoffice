using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PulseOffice.Scripting;

public sealed record MacroIdentity(string Fingerprint);

public interface IMacroFingerprints
{
    MacroIdentity Of(ScriptLanguage language, string source);
}

public sealed class Sha256MacroFingerprints : IMacroFingerprints
{
    public MacroIdentity Of(ScriptLanguage language, string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var bytes = Encoding.UTF8.GetBytes(language + "\n" + normalized);
        return new MacroIdentity(Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
}

public interface IMacroWriteGrants
{
    bool IsGranted(MacroIdentity macro);

    bool Grant(MacroIdentity macro);

    bool Revoke(MacroIdentity macro);
}

public sealed class FileMacroWriteGrants(string path, ILogger<FileMacroWriteGrants> logger) : IMacroWriteGrants
{
    private const int MaximumGrants = 4096;

    private readonly Lock _gate = new();
    private HashSet<string>? _granted;

    public bool IsGranted(MacroIdentity macro)
    {
        ArgumentNullException.ThrowIfNull(macro);
        lock (_gate) return Granted().Contains(macro.Fingerprint);
    }

    public bool Grant(MacroIdentity macro)
    {
        ArgumentNullException.ThrowIfNull(macro);
        lock (_gate)
        {
            var granted = Granted();
            if (granted.Contains(macro.Fingerprint)) return true;
            if (granted.Count >= MaximumGrants)
            {
                logger.LogWarning("File write permission not granted to macro {Fingerprint}: too many macros hold it", macro.Fingerprint);
                return false;
            }

            if (!Replace(new HashSet<string>(granted, StringComparer.Ordinal) { macro.Fingerprint })) return false;
        }
        logger.LogInformation("File write permission granted to macro {Fingerprint}", macro.Fingerprint);
        return true;
    }

    public bool Revoke(MacroIdentity macro)
    {
        ArgumentNullException.ThrowIfNull(macro);
        lock (_gate)
        {
            var granted = Granted();
            if (!granted.Contains(macro.Fingerprint)) return true;

            var remaining = new HashSet<string>(granted, StringComparer.Ordinal);
            remaining.Remove(macro.Fingerprint);
            if (!Replace(remaining)) return false;
        }
        logger.LogInformation("File write permission revoked from macro {Fingerprint}", macro.Fingerprint);
        return true;
    }

    private bool Replace(HashSet<string> granted)
    {
        try
        {
            Save(granted);
            _granted = granted;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The macro permission file could not be written: the permissions stay as they were");
            return false;
        }
    }

    private HashSet<string> Granted()
    {
        if (_granted is not null) return _granted;
        _granted = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            if (!File.Exists(path)) return _granted;
            var info = new FileInfo(path);
            if (info.Length > 1024 * 1024) throw new InvalidDataException("The macro permission file is too large.");
            var stored = JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? [];
            foreach (var fingerprint in stored.Where(Wellformed).Take(MaximumGrants)) _granted.Add(fingerprint);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            logger.LogWarning(ex, "The macro permission file could not be read: no macro may write files");
            _granted.Clear();
        }

        return _granted;
    }

    private bool Wellformed(string fingerprint) =>
        fingerprint is { Length: 64 } && fingerprint.All(character => char.IsAsciiHexDigitLower(character));

    private void Save(HashSet<string> granted)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(granted.Order(StringComparer.Ordinal).ToArray()));
        File.Move(temporary, path, overwrite: true);
    }
}

public interface IMacroPermissionPolicy
{
    ScriptPermissions For(ScriptLanguage language, string source, ScriptPermissions baseline);
}

public sealed class MacroPermissionPolicy(IMacroFingerprints fingerprints, IMacroWriteGrants grants) : IMacroPermissionPolicy
{
    public ScriptPermissions For(ScriptLanguage language, string source, ScriptPermissions baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        return grants.IsGranted(fingerprints.Of(language, source ?? string.Empty))
            ? baseline with { MacroFolderWrite = true }
            : baseline;
    }
}
