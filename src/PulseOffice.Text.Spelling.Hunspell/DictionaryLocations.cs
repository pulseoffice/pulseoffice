using PulseOffice.Platform;

namespace PulseOffice.Text.Spelling.Hunspell;

public interface IDictionaryLocations
{
    IReadOnlyList<string> Folders { get; }

    IReadOnlyList<string> SystemFolders { get; }

    string UserFolder { get; }
}

public sealed class DictionaryLocations(string userFolder, ISystemDictionaryDirectories system) : IDictionaryLocations
{
    public string UserFolder { get; } = userFolder;

    public IReadOnlyList<string> SystemFolders => system.Directories;

    public IReadOnlyList<string> Folders => [UserFolder, .. SystemFolders];
}
