using Microsoft.Extensions.Logging;

namespace PulseOffice.Text.Spelling.Hunspell;

public sealed class UserDictionaryFile : IUserDictionary
{
    private readonly string _path;
    private readonly ILogger<UserDictionaryFile> _logger;
    private readonly HashSet<string> _words = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly Lock _gate = new();
    private readonly Lock _saving = new();

    public const int LongestWord = 256;

    public UserDictionaryFile(ILogger<UserDictionaryFile> logger, string path)
    {
        _logger = logger;
        _path = path;

        Load();
    }

    public IReadOnlyCollection<string> Words
    {
        get { lock (_gate) return [.. _words.Order(StringComparer.CurrentCulture)]; }
    }

    public event EventHandler? Changed;

    public bool Knows(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;

        lock (_gate) return _words.Contains(word.Trim());
    }

    public void Add(string word)
    {
        if (Acceptable(word) is not { } accepted) return;

        lock (_gate)
        {
            if (!_words.Add(accepted)) return;
        }

        Save();
    }

    public bool Remove(string word)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(word) || !_words.Remove(word.Trim())) return false;
        }

        Save();
        return true;
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;

        try
        {
            foreach (var line in File.ReadLines(_path))
                if (Acceptable(line) is { } word) _words.Add(word);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Unreadable user dictionary: starting empty");
        }
    }

    private void Save()
    {
        lock (_saving)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

                string[] snapshot;
                lock (_gate) snapshot = [.. _words.Order(StringComparer.Ordinal)];

                var temporary = _path + ".tmp";
                File.WriteAllLines(temporary, snapshot);
                File.Move(temporary, _path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not save the user dictionary");
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private string? Acceptable(string? word)
    {
        var trimmed = word?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > LongestWord) return null;

        return trimmed.Any(character => char.IsControl(character)
            || char.GetUnicodeCategory(character) is System.Globalization.UnicodeCategory.LineSeparator
                or System.Globalization.UnicodeCategory.ParagraphSeparator)
            ? null
            : trimmed;
    }
}
