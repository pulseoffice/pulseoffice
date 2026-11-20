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

    public TableCellText Stacked()
    {
        var paragraphs = new List<TableCellParagraph>();
        foreach (var run in Paragraphs.SelectMany(paragraph => paragraph.Runs))
        {
            var elements = System.Globalization.StringInfo.GetTextElementEnumerator(run.Text);
            while (elements.MoveNext()) paragraphs.Add(new TableCellParagraph([run with { Text = elements.GetTextElement() }]));
        }
        return this with { Paragraphs = paragraphs };
    }

    public TableCellText? Rebased(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var before = PlainText;
        var formats = new List<TableTextRun>(before.Length);
        var plain = new TableTextRun(string.Empty);
        for (var index = 0; index < Paragraphs.Count; index++)
        {
            var paragraph = Paragraphs[index];
            if (index > 0) formats.Add(formats.Count > 0 ? formats[^1] : plain);
            foreach (var run in paragraph.Runs)
                for (var i = 0; i < run.Text.Length; i++) formats.Add(run with { Text = string.Empty });
        }
        var prefix = 0;
        while (prefix < before.Length && prefix < normalized.Length && before[prefix] == normalized[prefix]) prefix++;
        var suffix = 0;
        while (suffix < before.Length - prefix && suffix < normalized.Length - prefix
            && before[before.Length - 1 - suffix] == normalized[normalized.Length - 1 - suffix]) suffix++;
        var inserted = prefix > 0 ? formats[prefix - 1]
            : formats.Count > 0 ? formats[Math.Min(prefix, formats.Count - 1)]
            : plain;
        var paragraphs = new List<TableCellParagraph>();
        var runs = new List<TableTextRun>();
        for (var i = 0; i < normalized.Length; i++)
        {
            if (normalized[i] == '\n')
            {
                paragraphs.Add(new TableCellParagraph([.. runs]));
                runs.Clear();
                continue;
            }
            var format = i < prefix ? formats[i]
                : i >= normalized.Length - suffix ? formats[before.Length - (normalized.Length - i)]
                : inserted;
            if (runs.Count > 0 && runs[^1].SameFormat(format)) runs[^1] = runs[^1] with { Text = runs[^1].Text + normalized[i] };
            else runs.Add(format with { Text = normalized[i].ToString() });
        }
        paragraphs.Add(new TableCellParagraph([.. runs]));
        var rebased = this with { Paragraphs = paragraphs };
        return rebased.IsPlain ? null : rebased;
    }

    public bool Equals(TableCellText? other) =>
        other is not null && Row == other.Row && Column == other.Column && Paragraphs.SequenceEqual(other.Paragraphs);

    public override int GetHashCode() => HashCode.Combine(Row, Column, Paragraphs.Count);
}
