using System.Reflection;

namespace PulseOffice.Updates;

public interface IInstalledVersion
{
    ReleaseVersion Current { get; }
}

public sealed class EntryAssemblyVersion : IInstalledVersion
{
    public ReleaseVersion Current { get; } = Read();

    private static ReleaseVersion Read()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(EntryAssemblyVersion).Assembly;
        var text = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString();
        return ReleaseVersion.TryParse(text, out var version) ? version : default;
    }
}

public sealed class FixedInstalledVersion(ReleaseVersion current) : IInstalledVersion
{
    public ReleaseVersion Current { get; } = current;
}
