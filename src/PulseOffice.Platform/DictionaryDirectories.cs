namespace PulseOffice.Platform;

public interface ISystemDictionaryDirectories
{
    IReadOnlyList<string> Directories { get; }
}

public sealed class SystemDictionaryDirectories(IEnvironmentVariables environment, PlatformFamily family) : ISystemDictionaryDirectories
{
    private static readonly string[] LinuxFolders = ["hunspell", "myspell", Path.Combine("myspell", "dicts"), "hyphen", "mythes"];

    public IReadOnlyList<string> Directories => [.. Candidates().Distinct(StringComparer.Ordinal)];

    private IEnumerable<string> Candidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return family switch
        {
            PlatformFamily.MacOS => ["/Library/Spelling", Path.Combine(home, "Library", "Spelling")],
            PlatformFamily.Windows => [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Spelling")],
            _ => Linux(),
        };
    }

    private IEnumerable<string> Linux()
    {
        var data = new XdgDataDirectories(environment);
        return
        [
            .. data.SystemDirectories.SelectMany(directory => LinuxFolders.Select(folder => Path.Combine(directory, folder))),
            Path.Combine(data.HomeDirectory, "hunspell"),
        ];
    }
}
