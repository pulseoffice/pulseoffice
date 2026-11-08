using PulseOffice.Foundation.Modules;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckCharacterCommands(IActiveDeckContext context, IDeckTextInsertion insertion)
    : DeckCommandSet(context)
{
    private readonly IReadOnlyList<(string Key, string Text, string? Gesture)> _characters =
    [
        ("nonBreakingSpace", " ", "Ctrl+Shift+Space"),
        ("narrowNonBreakingSpace", " ", null),
        ("nonBreakingHyphen", "‑", "Ctrl+Shift+Minus"),
        ("softHyphen", "­", null),
        ("zeroWidthSpace", "​", null),
        ("wordJoiner", "⁠", null),
        ("leftToRightMark", "‎", null),
        ("rightToLeftMark", "‏", null),
    ];

    public IEnumerable<CommandDefinition> GetCommands() => _characters.Select(Character);

    private CommandDefinition Character((string Key, string Text, string? Gesture) character) =>
        Prototype($"deck.insert.character.{character.Key}") with
        {
            Surface = CommandSurface.Advanced,
            DefaultGesture = character.Gesture,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && (t.IsTyping() || t.Selected() is TextElement),
            Execute = (_, _) =>
            {
                if (Context.Target is { } t) insertion.Insert(t, new TextRun(character.Text), freeFontSize: 20);
                return Task.CompletedTask;
            },
        };
}
