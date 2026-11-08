using Microsoft.Extensions.Logging;
using Python.Runtime;

namespace PulseOffice.Scripting.CPython;

public interface ICPythonHost
{
    PythonRuntimeDescriptor? Active { get; }

    bool Start(PythonRuntimeDescriptor runtime);
}

public sealed class CPythonHost(ILogger<CPythonHost> logger) : ICPythonHost
{
    private readonly Lock _gate = new();

    public PythonRuntimeDescriptor? Active { get; private set; }

    public bool Start(PythonRuntimeDescriptor runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        lock (_gate)
        {
            if (Active is { } active) return active.Home == runtime.Home;

            try
            {
                Runtime.PythonDLL = runtime.LibraryPath;
                PythonEngine.PythonHome = runtime.Home;
                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads();
                Active = runtime;
                logger.LogInformation("CPython started: {Runtime}", runtime);
                return true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                logger.LogError(ex, "CPython could not start from {Library}", runtime.LibraryPath);
                return false;
            }
        }
    }
}
