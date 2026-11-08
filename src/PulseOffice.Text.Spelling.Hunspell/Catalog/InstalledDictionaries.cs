using System.Text.Json;
using PulseOffice.Text.Spelling.Dictionaries;

namespace PulseOffice.Text.Spelling.Hunspell.Catalog;

public interface IInstalledDictionaries
{
    IReadOnlyList<InstalledDictionary> All { get; }

    void Install(DictionaryPackage package, IReadOnlyList<DictionaryFileContent> files);

    bool Remove(string code, DictionaryPart part);
}

public sealed class InstalledDictionaries(IDictionaryLocations locations) : IInstalledDictionaries
{
    private const string ManifestName = "installed.json";
    private readonly Lock _gate = new();

    public IReadOnlyList<InstalledDictionary> All
    {
        get { lock (_gate) return Load(); }
    }

    public void Install(DictionaryPackage package, IReadOnlyList<DictionaryFileContent> files)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(files);

        lock (_gate)
        {
            var entries = Load();
            var previous = entries.FirstOrDefault(entry => Same(entry, package.Code, package.Part));
            var folder = Path.Combine(locations.UserFolder, Safe(package.Code));
            Directory.CreateDirectory(folder);

            var written = new List<string>();
            foreach (var file in files)
            {
                var name = Path.GetFileName(file.Name);
                if (name.Length == 0 || name != file.Name) throw new InvalidDataException("Invalid dictionary file name.");

                var target = Path.Combine(folder, name);
                var temporary = target + ".tmp";
                File.WriteAllBytes(temporary, file.Data);
                File.Move(temporary, target, overwrite: true);
                written.Add(Path.Combine(Safe(package.Code), name));
            }

            if (previous is not null)
                foreach (var stale in previous.Files.Except(written, StringComparer.Ordinal)) Delete(stale);

            entries.RemoveAll(entry => Same(entry, package.Code, package.Part));
            entries.Add(new InstalledDictionary(package.Code, package.Part, package.SourceId, package.Version, package.License, package.Origin, written));
            Save(entries);
        }
    }

    public bool Remove(string code, DictionaryPart part)
    {
        lock (_gate)
        {
            var entries = Load();
            var entry = entries.FirstOrDefault(candidate => Same(candidate, code, part));
            if (entry is null) return false;

            foreach (var file in entry.Files) Delete(file);
            entries.Remove(entry);
            Save(entries);

            var folder = Path.Combine(locations.UserFolder, Safe(code));
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            return true;
        }
    }

    private void Delete(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(locations.UserFolder, relative));
        var root = Path.GetFullPath(locations.UserFolder) + Path.DirectorySeparatorChar;
        if (path.StartsWith(root, StringComparison.Ordinal) && File.Exists(path)) File.Delete(path);
    }

    private List<InstalledDictionary> Load()
    {
        var path = Path.Combine(locations.UserFolder, ManifestName);
        if (!File.Exists(path)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<InstalledDictionary>>(File.ReadAllText(path))?
                .Where(entry => entry is { Code.Length: > 0, Files: not null })
                .ToList() ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return [];
        }
    }

    private void Save(List<InstalledDictionary> entries)
    {
        Directory.CreateDirectory(locations.UserFolder);
        var path = Path.Combine(locations.UserFolder, ManifestName);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(entries));
        File.Move(temporary, path, overwrite: true);
    }

    private bool Same(InstalledDictionary entry, string code, DictionaryPart part) =>
        entry.Part == part && string.Equals(entry.Code, code, StringComparison.OrdinalIgnoreCase);

    private string Safe(string code) => new([.. code.Where(character => char.IsAsciiLetterOrDigit(character) || character == '-')]);
}

public sealed record SystemDictionary(DictionaryPart Part, LanguageTag Language, string Path);

public interface ISystemDictionaryInventory
{
    IReadOnlyList<SystemDictionary> Scan();
}

public sealed class SystemDictionaryInventory(IDictionaryLocations locations) : ISystemDictionaryInventory
{
    public IReadOnlyList<SystemDictionary> Scan()
    {
        var found = new List<SystemDictionary>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MaxRecursionDepth = 4,
        };

        foreach (var folder in locations.SystemFolders)
        {
            if (!Directory.Exists(folder)) continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*", options))
                    if (Classify(file) is { } dictionary) found.Add(dictionary);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
            }
        }

        return found;
    }

    private SystemDictionary? Classify(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var extension = Path.GetExtension(file);

        if (extension.Equals(".dic", StringComparison.OrdinalIgnoreCase) && name.StartsWith("hyph_", StringComparison.OrdinalIgnoreCase))
            return new SystemDictionary(DictionaryPart.Hyphenation, LanguageTag.Parse(name[5..]), file);

        if (extension.Equals(".dat", StringComparison.OrdinalIgnoreCase) && name.StartsWith("th_", StringComparison.OrdinalIgnoreCase))
            return new SystemDictionary(DictionaryPart.Thesaurus, LanguageTag.Parse(Strip(name[3..])), file);

        if (extension.Equals(".aff", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.ChangeExtension(file, ".dic")))
            return new SystemDictionary(DictionaryPart.Spelling, LanguageTag.Parse(name), file);

        return null;
    }

    private string Strip(string name)
    {
        var version = name.LastIndexOf("_v", StringComparison.OrdinalIgnoreCase);
        return version > 0 ? name[..version] : name;
    }
}
