using System.Runtime.InteropServices;

namespace PulseOffice.Scripting.CPython;

public enum PythonProvisioningMode
{
    SystemInstallation,

    Standalone,

    ExplicitFolder,

    Conda,
}

public sealed record PythonRuntimeDescriptor
{
    public required Version Version { get; init; }

    public required string Home { get; init; }

    public required string LibraryPath { get; init; }

    public required string ExecutablePath { get; init; }

    public required Architecture Architecture { get; init; }
    public PythonProvisioningMode Origin { get; init; }

    public string? VirtualEnvironmentPath { get; init; }

    public bool IsFreeThreaded { get; init; }

    public bool MatchesCurrentProcess => Architecture == RuntimeInformation.ProcessArchitecture;

    public override string ToString() =>
        $"Python {Version} ({Architecture}{(IsFreeThreaded ? ", free-threaded" : "")}) in {Home}";
}

public sealed record PythonDiscoveryResult(
    PythonRuntimeDescriptor? Runtime,
    string? RejectionReason = null)
{
    public bool Found => Runtime is not null;
}
