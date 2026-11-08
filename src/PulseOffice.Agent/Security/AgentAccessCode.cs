using System.Security.Cryptography;
using System.Text;
using PulseOffice.Platform;

namespace PulseOffice.Agent.Security;

public interface IAgentAccessCode
{
    string Current { get; }

    string Regenerate();

    bool Matches(string? candidate);

    event EventHandler? Changed;
}

public sealed class FileAgentAccessCode(string path, IPrivateFiles files) : IAgentAccessCode
{
    private const int Bytes = 24;
    private readonly Lock _gate = new();
    private string? _code;

    public event EventHandler? Changed;

    public string Current
    {
        get
        {
            lock (_gate) return _code ??= Load() ?? Store(Generate());
        }
    }

    public string Regenerate()
    {
        string code;
        lock (_gate) code = _code = Store(Generate());
        Changed?.Invoke(this, EventArgs.Empty);
        return code;
    }

    public bool Matches(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate)) return false;
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(Current));
        var given = SHA256.HashData(Encoding.UTF8.GetBytes(candidate));
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }

    private string? Load()
    {
        if (!File.Exists(path)) return null;
        files.Restrict(path);
        var text = File.ReadAllText(path).Trim();
        return text.Length >= 16 ? text : null;
    }

    private string Store(string code)
    {
        files.CreateFolder(Path.GetDirectoryName(path)!);
        using (var stream = files.Open(path, FileMode.Create))
        using (var writer = new StreamWriter(stream))
            writer.Write(code);
        return code;
    }

    private string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(Bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
