using System.Globalization;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Fields;

public interface IMergePageStyles
{
    string? Adopt(TextDocument combined, TextDocument template, TextDocument record, MergeData data, string? start, int ordinal);
}

public sealed class MergePageStyles(IBlockTree tree, IMergeVariables merged) : IMergePageStyles
{
    public string? Adopt(TextDocument combined, TextDocument template, TextDocument record, MergeData data, string? start, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(combined);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(data);
        var personal = Personal(template, data);
        if (personal.Count == 0) return null;
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in personal)
        {
            var candidate = name + " " + ordinal.ToString(CultureInfo.InvariantCulture);
            for (var suffix = 2; combined.Styles.Page.Contains(candidate); suffix++)
                candidate = name + " " + ordinal.ToString(CultureInfo.InvariantCulture) + "." + suffix.ToString(CultureInfo.InvariantCulture);
            names[name] = candidate;
        }
        foreach (var (name, renamed) in names)
        {
            if (record.Styles.Page.Find(name) is not { } source) continue;
            var clone = (PageStyle)source.CloneAs(renamed);
            if (clone.NextStyleName is { } next && names.TryGetValue(next, out var following)) clone.NextStyleName = following;
            combined.Styles.Page.Add(clone);
        }
        return names.GetValueOrDefault(start ?? record.InitialPageStyle);
    }

    private HashSet<string> Personal(TextDocument template, MergeData data)
    {
        var styles = template.Styles.Page.All;
        var personal = new HashSet<string>(styles.Where(style => Mentions(style, template, data)).Select(style => style.Name), StringComparer.Ordinal);
        if (personal.Count == 0) return personal;
        bool grown;
        do
        {
            grown = false;
            foreach (var style in styles)
                if (style.NextStyleName is { } next && personal.Contains(next) && personal.Add(style.Name)) grown = true;
        }
        while (grown);
        return personal;
    }

    private bool Mentions(PageStyle style, TextDocument template, MergeData data) =>
        new[] { style.Header, style.Footer }
            .SelectMany(content => new[] { content.Default, content.First, content.Even })
            .OfType<BlockContainer>()
            .SelectMany(tree.Paragraphs)
            .SelectMany(paragraph => paragraph.Content.Runs)
            .Any(run => run.Field == TextFieldKind.Database
                || run.Field is TextFieldKind.ConditionalText or TextFieldKind.HiddenText or TextFieldKind.HiddenParagraph && merged.Mentions(run.FieldArgument, template, data));
}
