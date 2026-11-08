namespace PulseOffice.Scripting.CPython;

public sealed class PythonRuntimeLocator(IStandalonePythonStore standalone, IPythonDiscovery discovery) : IPythonRuntimeLocator
{
    public PythonRuntimeDescriptor? Locate() =>
        standalone.Installed() is { MatchesCurrentProcess: true } installed ? installed : discovery.FindBest();
}
