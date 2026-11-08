namespace PulseOffice.Documents.Controls;

public sealed class ContentControlLockTokens
{
    private const string Unlocked = "unlocked";
    private const string ContentOnly = "contentLocked";
    private const string ControlOnly = "sdtLocked";
    private const string Both = "sdtContentLocked";

    public string? Token(ContentControl control) => (control.ContentLocked, control.ControlLocked) switch
    {
        (true, true) => Both,
        (true, false) => ContentOnly,
        (false, true) => ControlOnly,
        _ => null,
    };

    public ContentControl Apply(ContentControl control, string? token) => token switch
    {
        _ when string.Equals(token, Both, StringComparison.OrdinalIgnoreCase) => control with { ContentLocked = true, ControlLocked = true },
        _ when string.Equals(token, ContentOnly, StringComparison.OrdinalIgnoreCase) => control with { ContentLocked = true, ControlLocked = false },
        _ when string.Equals(token, ControlOnly, StringComparison.OrdinalIgnoreCase) => control with { ContentLocked = false, ControlLocked = true },
        _ when string.Equals(token, Unlocked, StringComparison.OrdinalIgnoreCase) => control with { ContentLocked = false, ControlLocked = false },
        _ => control,
    };
}
