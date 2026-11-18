namespace PulseOffice.Documents.Editing;

public abstract record StyleLink
{
    private StyleLink()
    {
    }

    public sealed record None : StyleLink;

    public sealed record ToNew : StyleLink;

    public sealed record ToExisting(string CharacterStyle) : StyleLink;
}
