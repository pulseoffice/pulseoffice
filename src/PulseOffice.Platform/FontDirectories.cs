namespace PulseOffice.Platform;

public interface ISystemFontDirectories
{
    IReadOnlyList<string> Directories { get; }
}

public sealed class SystemFontDirectories(IEnvironmentVariables environment, PlatformFamily family) : ISystemFontDirectories
{
    private static readonly string[] FlatpakHostFonts = ["/run/host/fonts", "/run/host/local-fonts", "/run/host/user-fonts"];

    public IReadOnlyList<string> Directories => [.. Candidates().Where(Directory.Exists)];

    public IReadOnlyList<string> Candidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        IEnumerable<string> candidates = family switch
        {
            PlatformFamily.MacOS => ["/System/Library/Fonts", "/Library/Fonts", Path.Combine(home, "Library", "Fonts")],
            PlatformFamily.Windows =>
            [
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"),
            ],
            _ => Linux(home),
        };

        return [.. candidates.Where(candidate => candidate.Length > 0).Distinct(StringComparer.Ordinal)];
    }

    private IEnumerable<string> Linux(string home)
    {
        var data = new XdgDataDirectories(environment);
        return
        [
            .. data.SystemDirectories.Select(directory => Path.Combine(directory, "fonts")),
            Path.Combine(data.HomeDirectory, "fonts"),
            Path.Combine(home, ".fonts"),
            .. FlatpakHostFonts,
        ];
    }
}
