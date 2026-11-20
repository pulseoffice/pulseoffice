using System.Runtime.CompilerServices;
using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Flow;

public sealed record SectionPages(int Number, int Pages);

public interface IPageFieldText
{
    bool HasNumber(TextParagraph paragraph);

    bool HasCount(TextParagraph paragraph);

    TextParagraph Resolve(TextParagraph paragraph, string? number, string? count, int? value = null, SectionPages? section = null);
}

public sealed class PageFieldText(PulseOffice.Documents.Fields.ISequenceNumerals numerals) : IPageFieldText
{
    private readonly ConditionalWeakTable<TextParagraph, Dictionary<(string? Number, string? Count, int? Value, SectionPages? Section), TextParagraph>> _resolved = new();

    public bool HasNumber(TextParagraph paragraph) => Has(paragraph, TextFieldKind.PageNumber);

    public bool HasCount(TextParagraph paragraph) => Has(paragraph, TextFieldKind.PageCount);

    private bool Has(TextParagraph paragraph, TextFieldKind kind)
    {
        var runs = paragraph.Runs;
        for (var i = 0; i < runs.Count; i++)
        {
            if (runs[i].Field == kind) return true;
        }
        return false;
    }

    private bool HasSection(TextParagraph paragraph) => Has(paragraph, TextFieldKind.SectionNumber) || Has(paragraph, TextFieldKind.SectionPageCount);

    public TextParagraph Resolve(TextParagraph paragraph, string? number, string? count, int? value = null, SectionPages? section = null)
    {
        var sectioned = section is not null && HasSection(paragraph);
        if (!sectioned) section = null;
        if (!HasNumber(paragraph) && !HasCount(paragraph) && !sectioned) return paragraph;
        var variants = _resolved.GetValue(paragraph, _ => []);
        lock (variants)
        {
            if (variants.TryGetValue((number, count, value, section), out var cached)) return cached;
            cached = paragraph.WithRuns([.. paragraph.Runs.Select(run => run.Field switch
            {
                TextFieldKind.PageNumber when number is not null => run with { Text = Shaped(number, value, run.FieldDetail?.Value) },
                TextFieldKind.PageCount when count is not null => run with { Text = count },
                TextFieldKind.SectionNumber when section is not null => run with { Text = Shaped(section.Number, run.FieldDetail?.Value) },
                TextFieldKind.SectionPageCount when section is not null => run with { Text = Shaped(section.Pages, run.FieldDetail?.Value) },
                _ => run,
            })]);
            variants[(number, count, value, section)] = cached;
            return cached;
        }
    }

    private string Shaped(int value, string? format) =>
        format is { Length: > 0 } ? numerals.Format(value, format) : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private string Shaped(string number, int? value, string? format)
    {
        if (format is not { Length: > 0 }) return number;
        if (value is { } known) return numerals.Format(known, format);
        return int.TryParse(number, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? numerals.Format(parsed, format)
            : number;
    }
}

public interface IPageCountFields
{
    bool Present(TextDocument document);
}

public sealed class PageCountFields(IBlockTree tree, IPageFieldText fields) : IPageCountFields
{
    public bool Present(TextDocument document)
    {
        if (tree.Paragraphs(document.Body).Any(paragraph => fields.HasCount(paragraph.Content))) return true;
        foreach (var style in document.Styles.Page.All)
        {
            foreach (var content in new[] { style.Header, style.Footer })
            {
                if (!content.Enabled) continue;
                var containers = new[] { content.Default, content.First, content.Even }.OfType<BlockContainer>();
                if (containers.Any(container => tree.Paragraphs(container).Any(paragraph => fields.HasCount(paragraph.Content)))) return true;
            }
        }
        return false;
    }
}
