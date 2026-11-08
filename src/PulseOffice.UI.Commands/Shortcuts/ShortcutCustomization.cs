namespace PulseOffice.UI.Commands.Shortcuts;

public interface IShortcutBindingStore
{
    IReadOnlyDictionary<string, string?> Load();

    void Save(IReadOnlyDictionary<string, string?> overrides);
}

public interface IShortcutCustomization
{
    int ApplySaved();

    void Apply(IReadOnlyDictionary<string, string?> edits);
}

public sealed class ShortcutCustomization(CommandRegistry registry, IShortcutBindingStore store) : IShortcutCustomization
{
    private readonly GestureChordParser _parser = new();

    public int ApplySaved()
    {
        var applied = 0;

        foreach (var (id, gesture) in store.Load())
        {
            if (gesture is not null && _parser.Parse(gesture) is null) continue;
            if (registry.Rebind(new CommandId(id), gesture)) applied++;
        }

        if (applied > 0) registry.NotifyBindingsChanged();
        return applied;
    }

    public void Apply(IReadOnlyDictionary<string, string?> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);

        foreach (var (id, gesture) in edits) registry.Rebind(new CommandId(id), gesture);

        var overrides = new Dictionary<string, string?>(store.Load(), StringComparer.Ordinal);

        foreach (var id in edits.Keys)
        {
            var command = registry.Find(new CommandId(id));
            if (command is null) continue;

            if (Same(command.DefaultGesture, registry.FactoryGestureOf(command.Id))) overrides.Remove(id);
            else overrides[id] = command.DefaultGesture;
        }

        store.Save(overrides);
        registry.NotifyBindingsChanged();
    }

    private bool Same(string? first, string? second) =>
        string.Equals(_parser.Parse(first)?.Canonical(), _parser.Parse(second)?.Canonical(), StringComparison.OrdinalIgnoreCase);
}
