namespace PulseOffice.UI.Controls.Dialogs;

public sealed record FormFieldGroup(string? Title, IReadOnlyList<FormField> Fields);

public sealed class FormSectionGrouping(string optionsTitle)
{
    private const int OptionsRunMinimum = 2;

    public IReadOnlyList<FormFieldGroup> Group(IReadOnlyList<FormField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var groups = new List<FormFieldGroup>();
        foreach (var segment in Segments(fields))
        {
            if (segment[0].Section is { } declared) groups.Add(new FormFieldGroup(declared.Length == 0 ? null : declared, segment));
            else groups.AddRange(Automatic(segment));
        }

        return groups;
    }

    private static List<List<FormField>> Segments(IReadOnlyList<FormField> fields)
    {
        var segments = new List<List<FormField>>();
        foreach (var field in fields)
        {
            if (segments.Count == 0 || segments[^1][0].Section != field.Section) segments.Add([]);
            segments[^1].Add(field);
        }

        return segments;
    }

    private IEnumerable<FormFieldGroup> Automatic(List<FormField> segment)
    {
        var mixed = segment.Any(field => field is CheckField) && segment.Any(field => field is not CheckField);
        if (!mixed)
        {
            yield return new FormFieldGroup(null, segment);
            yield break;
        }

        var pending = new List<FormField>();
        var index = 0;
        while (index < segment.Count)
        {
            var end = index;
            while (end < segment.Count && segment[end] is CheckField) end++;
            var checks = end - index;

            if (checks >= OptionsRunMinimum && index > 0)
            {
                if (pending.Count > 0) yield return new FormFieldGroup(null, pending);
                pending = [];
                yield return new FormFieldGroup(optionsTitle, segment.GetRange(index, checks));
                index = end;
                continue;
            }

            if (checks == 0) pending.Add(segment[index++]);
            else
            {
                pending.AddRange(segment.GetRange(index, checks));
                index = end;
            }
        }

        if (pending.Count > 0) yield return new FormFieldGroup(null, pending);
    }
}
