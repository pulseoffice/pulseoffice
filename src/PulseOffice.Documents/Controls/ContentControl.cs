namespace PulseOffice.Documents.Controls;

public enum ContentControlKind
{
    RichText,
    PlainText,
    CheckBox,
    DropDownList,
    ComboBox,
    Date,
    Picture,
}

public sealed record ContentControlItem(string DisplayText, string Value);

public sealed record CheckBoxSymbol(string Character, string? Font);

public sealed record ContentControl
{
    public ContentControlKind Kind { get; init; } = ContentControlKind.RichText;

    public string? Title { get; init; }

    public string? Tag { get; init; }

    public int? NumericId { get; init; }

    public bool ShowingPlaceholder { get; init; }

    public string? PlaceholderText { get; init; }

    public string? PlaceholderPart { get; init; }

    public bool ContentLocked { get; init; }

    public bool ControlLocked { get; init; }

    public IReadOnlyList<ContentControlItem> Items { get; init; } = [];

    public string? DateFormat { get; init; }

    public string? DateLanguage { get; init; }

    public DateTime? Date { get; init; }

    public bool Checked { get; init; }

    public CheckBoxSymbol CheckedSymbol { get; init; } = new("☒", "MS Gothic");

    public CheckBoxSymbol UncheckedSymbol { get; init; } = new("☐", "MS Gothic");

    public bool MultiLine { get; init; }

    public bool Block { get; init; }

    public string CurrentSymbol => (Checked ? CheckedSymbol : UncheckedSymbol).Character;

    public bool Equals(ContentControl? other) =>
        other is not null
        && Kind == other.Kind && Title == other.Title && Tag == other.Tag && NumericId == other.NumericId
        && ShowingPlaceholder == other.ShowingPlaceholder && PlaceholderText == other.PlaceholderText && PlaceholderPart == other.PlaceholderPart
        && ContentLocked == other.ContentLocked && ControlLocked == other.ControlLocked && Items.SequenceEqual(other.Items)
        && DateFormat == other.DateFormat && DateLanguage == other.DateLanguage && Date == other.Date && Checked == other.Checked
        && CheckedSymbol == other.CheckedSymbol && UncheckedSymbol == other.UncheckedSymbol && MultiLine == other.MultiLine && Block == other.Block;

    public override int GetHashCode() => HashCode.Combine(Kind, Title, Tag, Items.Count, Checked, ShowingPlaceholder, Date);
}
