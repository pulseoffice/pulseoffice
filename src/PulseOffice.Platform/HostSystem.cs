using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PulseOffice.Platform;

public enum HostSystemKind
{
    MacOS,
    Windows,
    Linux,
    OtherUnix,
}

public interface IHostSystem
{
    HostSystemKind Kind { get; }

    string VersionString { get; }

    Architecture ProcessArchitecture { get; }

    [SupportedOSPlatformGuard("macos")]
    bool IsMacOS => Kind == HostSystemKind.MacOS;

    [SupportedOSPlatformGuard("windows")]
    bool IsWindows => Kind == HostSystemKind.Windows;

    [SupportedOSPlatformGuard("linux")]
    bool IsLinux => Kind == HostSystemKind.Linux;

    StringComparison PathComparison => Kind is HostSystemKind.MacOS or HostSystemKind.Windows
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    string ExecutableName(string baseName) => IsWindows ? baseName + ".exe" : baseName;
}

public sealed class RuntimeHostSystem : IHostSystem
{
    public HostSystemKind Kind { get; } =
        OperatingSystem.IsMacOS() ? HostSystemKind.MacOS
        : OperatingSystem.IsWindows() ? HostSystemKind.Windows
        : OperatingSystem.IsLinux() ? HostSystemKind.Linux
        : HostSystemKind.OtherUnix;

    public string VersionString => Environment.OSVersion.VersionString;

    public Architecture ProcessArchitecture => RuntimeInformation.ProcessArchitecture;
}

public sealed class FixedHostSystem(HostSystemKind kind, string versionString = "", Architecture architecture = Architecture.X64) : IHostSystem
{
    public HostSystemKind Kind { get; } = kind;

    public string VersionString { get; } = versionString;

    public Architecture ProcessArchitecture { get; } = architecture;
}
