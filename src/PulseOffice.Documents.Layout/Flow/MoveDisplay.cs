namespace PulseOffice.Documents.Layout.Flow;

public enum MovedFromMark
{
    DoubleStrikethrough,
    Strikethrough,
}

public enum MovedToMark
{
    DoubleUnderline,
    Underline,
    Bold,
}

public sealed record MoveDisplay(uint? Color, MovedFromMark From, MovedToMark To);

public interface IMoveDisplayOptions
{
    MoveDisplay Current { get; }

    event EventHandler? Changed;
}

public sealed class StandardMoveDisplayOptions : IMoveDisplayOptions
{
    public MoveDisplay Current { get; } = new(0xFF008A00, MovedFromMark.DoubleStrikethrough, MovedToMark.DoubleUnderline);

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }
}
