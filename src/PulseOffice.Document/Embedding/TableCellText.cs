namespace PulseOffice.Document.Embedding;

public sealed record TableTextRun(string Text)
{
    public bool? Bold { get; init; }

    public bool? Italic { get; init; }

    public bool? Underline { get; init; }

    public bool? Strikethrough { get; init; }

    public string? TextHex { get; init; }

    public string? FontFamily { get; init; }

    public double? FontSize { get; init; }

    public bool IsPlain =>
        Bold is null && Italic is null && Underline is null && Strikethrough is null
        && TextHex is null && FontFamily is null && FontSize is null;

    public TableCellLook Over(TableCellLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        return look with
        {
            Bold = Bold ?? look.Bold,
            Italic = Italic ?? look.Italic,
            Underline = Underline ?? look.Underline,
            Strikethrough = Strikethrough ?? look.Strikethrough,
            TextHex = TextHex ?? look.TextHex,
            FontFamily = FontFamily ?? look.FontFamily,
            FontSize = FontSize ?? look.FontSize,
        };
    }

    public TableTextRun Beyond(TableCellLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        return this with
        {
            Bold = Bold == look.Bold ? null : Bold,
            Italic = Italic == look.Italic ? null : Italic,
            Underline = Underline == look.Underline ? null : Underline,
            Strikethrough = Strikethrough == look.Strikethrough ? null : Strikethrough,
            TextHex = string.Equals(TextHex, look.TextHex, StringComparison.OrdinalIgnoreCase) ? null : TextHex,
            FontFamily = string.Equals(FontFamily, look.FontFamily, StringComparison.Ordinal) ? null : FontFamily,
            FontSize = FontSize == look.FontSize ? null : FontSize,
        };
    }

    public bool SameFormat(TableTextRun other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return (this with { Text = string.Empty }) == (other with { Text = string.Empty });
    }
}

public sealed record TableCellParagraph(IReadOnlyList<TableTextRun> Runs)
{
    public string Text => string.Concat(Runs.Select(run => run.Text));

    public bool Equals(TableCellParagraph? other) => other is not null && Runs.SequenceEqual(other.Runs);

    public override int GetHashCode() => Runs.Aggregate(0, (hash, run) => HashCode.Combine(hash, run));
}

public sealed record TableCellText(int Row, int Column, IReadOnlyList<TableCellParagraph> Paragraphs)
{
    public string PlainText => string.Join("\n", Paragraphs.Select(paragraph => paragraph.Text));

    public bool IsPlain => Paragraphs.All(paragraph => paragraph.Runs.All(run => run.IsPlain));

    public bool Matches(string? value) =>
        string.Equals(PlainText, (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);

    public bool Equals(TableCellText? other) =>
        other is not null && Row == other.Row && Column == other.Column && Paragraphs.SequenceEqual(other.Paragraphs);

    public override int GetHashCode() => HashCode.Combine(Row, Column, Paragraphs.Count);
}
