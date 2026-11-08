namespace PulseOffice.UI.Controls.Links;

public enum LinkKind
{
    Web,
    Place,
    File,
    Mail,
}

public sealed record LinkPlace(string Label, string Address, string Icon)
{
    public IReadOnlyList<LinkPlace> Children { get; init; } = [];

    public bool Selectable { get; init; } = true;
}

public sealed record LinkRequest(string TitleKey)
{
    public string Address { get; init; } = string.Empty;

    public string? Text { get; init; }

    public bool ShowsText { get; init; } = true;

    public bool TextEditable { get; init; } = true;

    public IReadOnlyList<LinkPlace> Places { get; init; } = [];

    public IReadOnlyList<LinkKind> Kinds { get; init; } = [LinkKind.Web, LinkKind.Place, LinkKind.File, LinkKind.Mail];

    public bool CanRemove { get; init; }
}

public sealed record LinkChoice(LinkKind Kind, string Address, string Text)
{
    public bool Remove { get; init; }
}

public sealed record MailAddress(string To, string Subject);
