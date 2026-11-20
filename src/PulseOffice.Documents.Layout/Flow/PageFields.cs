using System.Runtime.CompilerServices;
using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Flow;

public interface IPageFieldText
{
    bool HasNumber(TextParagraph paragraph);

    bool HasCount(TextParagraph paragraph);

    TextParagraph Resolve(TextParagraph paragraph, string? number, string? count);
}

public sealed class PageFieldText(PulseOffice.Documents.Fields.ISequenceNumerals numerals) : IPageFieldText
{
    private readonly ConditionalWeakTable<TextParagraph, Dictionary<(string? Number, string? Count), TextParagraph>> _resolved = new();

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

    public TextParagraph Resolve(TextParagraph paragraph, string? number, string? count)
    {
        if (!HasNumber(paragraph) && !HasCount(paragraph)) return paragraph;
        var variants = _resolved.GetValue(paragraph, _ => []);
        lock (variants)
        {
            if (variants.TryGetValue((number, count), out var cached)) return cached;
            cached = paragraph.WithRuns([.. paragraph.Runs.Select(run => run.Field switch
            {
                TextFieldKind.PageNumber when number is not null => run with { Text = Shaped(number, run.FieldDetail?.Value) },
                TextFieldKind.PageCount when count is not null => run with { Text = count },
                _ => run,
            })]);
            variants[(number, count)] = cached;
            return cached;
        }
    }

    private string Shaped(string number, string? format) =>
        format is { Length: > 0 } && int.TryParse(number, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? numerals.Format(value, format)
            : number;
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
