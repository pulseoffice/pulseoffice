namespace PulseOffice.Shell;

public sealed class SafeFileName
{
    private const int MaxLength = 120;

    private readonly HashSet<char> _forbidden = [.. Path.GetInvalidFileNameChars(), '/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public string Of(string name, string fallback)
    {
        ArgumentException.ThrowIfNullOrEmpty(fallback);
        var cleaned = string.Concat((name ?? string.Empty).Select(character => _forbidden.Contains(character) || char.IsControl(character) ? '_' : character)).Trim();
        if (cleaned.Length > MaxLength) cleaned = cleaned[..MaxLength];
        return cleaned.Trim('.', ' ').Length == 0 ? fallback : cleaned;
    }
}
