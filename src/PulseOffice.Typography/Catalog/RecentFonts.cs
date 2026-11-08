using System.Text.Json;

namespace PulseOffice.Typography.Catalog;

public interface IRecentFonts
{
    IReadOnlyList<string> All { get; }

    void Remember(string family);
}

public sealed class FileRecentFonts(string path, int capacity = 8) : IRecentFonts
{
    private List<string>? _families;

    public IReadOnlyList<string> All => Families();

    public void Remember(string family)
    {
        if (string.IsNullOrWhiteSpace(family)) return;
        var families = Families();
        families.RemoveAll(entry => string.Equals(entry, family, StringComparison.OrdinalIgnoreCase));
        families.Insert(0, family.Trim());
        if (families.Count > capacity) families.RemoveRange(capacity, families.Count - capacity);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(families));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private List<string> Families()
    {
        if (_families is not null) return _families;
        try
        {
            _families = File.Exists(path)
                ? (JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? []).Where(entry => !string.IsNullOrWhiteSpace(entry)).ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _families = [];
        }

        return _families;
    }
}
