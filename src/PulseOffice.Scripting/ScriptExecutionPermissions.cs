namespace PulseOffice.Scripting;

public enum ScriptCapability
{
    FileSystemRead,
    FileSystemWrite,
    MacroFolderWrite,
    Network,
    LaunchProcesses,
    DatabaseAccess,
}

public sealed class ScriptPermissionException(ScriptCapability capability)
    : UnauthorizedAccessException($"The macro is not allowed this operation ({capability}): macros run in a sandbox.")
{
    public ScriptCapability Capability { get; } = capability;
}

public interface IScriptExecutionPermissions
{
    ScriptPermissions Current { get; }

    IDisposable Enter(ScriptPermissions permissions);

    void Demand(ScriptCapability capability);
}

public sealed class ScriptExecutionPermissions : IScriptExecutionPermissions
{
    private readonly AsyncLocal<ScriptPermissions?> _current = new();

    public ScriptPermissions Current => _current.Value ?? ScriptPermissions.Sandboxed;

    public IDisposable Enter(ScriptPermissions permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var previous = _current.Value;
        _current.Value = permissions;
        return new Scope(this, previous);
    }

    public void Demand(ScriptCapability capability)
    {
        var current = Current;
        var allowed = capability switch
        {
            ScriptCapability.FileSystemRead => current.FileSystemRead,
            ScriptCapability.FileSystemWrite => current.FileSystemWrite,
            ScriptCapability.MacroFolderWrite => current.MacroFolderWrite || current.FileSystemWrite,
            ScriptCapability.Network => current.Network,
            ScriptCapability.LaunchProcesses => current.LaunchProcesses,
            ScriptCapability.DatabaseAccess => current.DatabaseAccess,
            _ => false,
        };
        if (!allowed) throw new ScriptPermissionException(capability);
    }

    private sealed class Scope(ScriptExecutionPermissions owner, ScriptPermissions? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner._current.Value = previous;
        }
    }
}
