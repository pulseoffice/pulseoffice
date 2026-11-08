namespace PulseOffice.UI.Commands.Shortcuts;

public interface IReservedShortcuts
{
    bool IsReserved(GestureChord chord);
}

public abstract class ReservedShortcutTable : IReservedShortcuts
{
    private readonly HashSet<string> _reserved;

    protected ReservedShortcutTable()
    {
        var parser = new GestureChordParser();
        _reserved = new HashSet<string>(
            Gestures().Select(parser.Parse).OfType<GestureChord>().Select(Key),
            StringComparer.OrdinalIgnoreCase);
    }

    public static IReservedShortcuts ForSystem(bool macOS) =>
        macOS ? new MacReservedShortcuts() : new PcReservedShortcuts();

    public bool IsReserved(GestureChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        return ReservedModifiers(chord.Modifiers) || _reserved.Contains(Key(chord));
    }

    protected abstract IEnumerable<string> Gestures();

    protected virtual bool ReservedModifiers(ChordModifiers modifiers) => false;

    private string Key(GestureChord chord) => chord.Canonical();
}

public sealed class MacReservedShortcuts : ReservedShortcutTable
{
    protected override IEnumerable<string> Gestures() =>
    [
        "Ctrl+Q", "Ctrl+H", "Ctrl+Alt+H", "Ctrl+M", "Ctrl+Tab", "Ctrl+Shift+Tab", "Ctrl+Space", "Ctrl+Alt+Space",
        "Ctrl+Alt+Escape", "Ctrl+Shift+3", "Ctrl+Shift+4", "Ctrl+Shift+5", "Ctrl+Control+Q", "Ctrl+Control+Space",
        "Ctrl+Control+F", "Ctrl+Period", "Control+Up", "Control+Down", "Control+Left", "Control+Right",
    ];
}

public sealed class PcReservedShortcuts : ReservedShortcutTable
{
    protected override IEnumerable<string> Gestures() =>
    [
        "Alt+F4", "Alt+Tab", "Alt+Shift+Tab", "Alt+Escape", "Alt+Space", "Ctrl+Escape", "Ctrl+Alt+Delete",
        "Ctrl+Shift+Escape",
    ];

    protected override bool ReservedModifiers(ChordModifiers modifiers) => modifiers.HasFlag(ChordModifiers.Super);
}
