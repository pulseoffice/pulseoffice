namespace PulseOffice.UI.Commands;

public sealed record CommandPresentation(
    CommandDefinition Command,
    string Title,
    string? Description,
    string? Path,
    string? Gesture)
{
    public string? Category { get; init; }
}

public sealed record CommandPaletteSection(string? Title, IReadOnlyList<CommandPresentation> Items);
