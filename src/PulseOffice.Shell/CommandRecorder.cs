using PulseOffice.UI.Commands;

namespace PulseOffice.Shell;

public interface ICommandRecorder
{
    bool IsRecording { get; }

    void Start();

    IReadOnlyList<CommandId> Stop();

    void Note(CommandDefinition command);

    event EventHandler? Changed;
}

public interface IScriptableCommandPolicy
{
    bool Allows(CommandId command);

    bool Allows(CommandDefinition command);
}

public sealed class ScriptableCommandPolicy : IScriptableCommandPolicy
{
    private static readonly string[] Outside =
    [
        ".file.", ".export", ".send", ".mail", ".bluetooth", ".print", ".scan", ".remote", ".external",
        ".openObject", ".media", ".macro", ".script", ".recordMacro", ".signature", ".sign", ".share",
        ".quit", ".close", ".browser", ".online", ".link", ".insert.image",
        ".data.refresh", ".database.refresh", ".providerRefresh", ".data.provider", ".xmlSource",
        "updateLinks", ".updateAll", ".mailMerge", ".hyperlink.open",
    ];

    public bool Allows(CommandId command)
    {
        var value = command.Value?.Trim();
        if (string.IsNullOrEmpty(value)) return false;
        return !Outside.Any(part => value.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    public bool Allows(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !command.OutsideHistory && Allows(command.Id);
    }
}

public sealed class CommandRecorder(IScriptableCommandPolicy policy) : ICommandRecorder
{
    private readonly List<CommandId> _recorded = [];

    public bool IsRecording { get; private set; }

    public event EventHandler? Changed;

    public void Start()
    {
        _recorded.Clear();
        IsRecording = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<CommandId> Stop()
    {
        IsRecording = false;
        var recorded = _recorded.ToList();
        _recorded.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
        return recorded;
    }

    public void Note(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsRecording && policy.Allows(command)) _recorded.Add(command.Id);
    }
}

public interface IMacroScriptWriter
{
    string Write(IReadOnlyList<CommandId> commands);
}

public sealed class JavaScriptMacroWriter : IMacroScriptWriter
{
    public string Write(IReadOnlyList<CommandId> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        return string.Concat(commands.Select(command => $"app.Run(\"{command.Value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\");\n"));
    }
}
