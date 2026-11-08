using Avalonia.Input;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Controls.Shortcuts;

public interface IShortcutDispatcher
{
    CommandDefinition? Resolve(KeyEventArgs key, CommandContext context);

    IReadOnlyList<(CommandPresentation Presentation, ShortcutGesture Gesture)> Known(CommandContext context);
}

public sealed class ShortcutDispatcher(CommandRegistry registry) : IShortcutDispatcher
{
    private readonly CommandRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly Dictionary<string, ShortcutGesture?> _parsed = [];

    public CommandDefinition? Resolve(KeyEventArgs key, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return null;

        if (key.KeyModifiers == KeyModifiers.None && key.Key is not (>= Key.F1 and <= Key.F24)) return null;

        foreach (var (gesture, command) in _registry.ActiveGestures(context))
            if (GestureOf(gesture) is { } parsed && parsed.Matches(key))
                return command;

        return null;
    }

    public IReadOnlyList<(CommandPresentation Presentation, ShortcutGesture Gesture)> Known(CommandContext context) =>
    [
        .. _registry.ActiveGestures(context)
            .Select(pair => (Presentation: _registry.Present(pair.Value), Gesture: GestureOf(pair.Key)))
            .Where(entry => entry.Gesture is not null)
            .Select(entry => (entry.Presentation, entry.Gesture!.Value))
            .OrderBy(entry => entry.Presentation.Path ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.Presentation.Title, StringComparer.CurrentCultureIgnoreCase),
    ];

    private ShortcutGesture? GestureOf(string text)
    {
        if (_parsed.TryGetValue(text, out var cached)) return cached;

        cached = ShortcutGesture.Parse(text);
        _parsed[text] = cached;

        return cached;
    }
}
