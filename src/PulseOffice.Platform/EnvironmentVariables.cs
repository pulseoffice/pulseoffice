using System.Runtime.InteropServices;

namespace PulseOffice.Platform;

public interface IEnvironmentVariables
{
    string? Get(string name);
}

public sealed class SystemEnvironmentVariables : IEnvironmentVariables
{
    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
}

public enum PlatformFamily
{
    MacOS,
    Windows,
    Linux,
}

public sealed class RunningPlatform
{
    public PlatformFamily Family =>
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? PlatformFamily.MacOS
        : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? PlatformFamily.Windows
        : PlatformFamily.Linux;
}

public sealed class XdgDataDirectories(IEnvironmentVariables environment)
{
    private const string DefaultDataDirectories = "/usr/local/share:/usr/share";

    public IReadOnlyList<string> SystemDirectories
    {
        get
        {
            var listed = environment.Get("XDG_DATA_DIRS");
            var directories = (string.IsNullOrWhiteSpace(listed) ? DefaultDataDirectories : listed)
                .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(Path.IsPathFullyQualified)
                .ToList();

            return directories.Count > 0 ? directories : [.. DefaultDataDirectories.Split(':')];
        }
    }

    public string HomeDirectory
    {
        get
        {
            var data = environment.Get("XDG_DATA_HOME");
            return !string.IsNullOrEmpty(data) && Path.IsPathFullyQualified(data)
                ? data
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }
    }
}
