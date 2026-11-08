namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxLinkSyntax
{
    public bool IsInternal(string link) => link.StartsWith('#');

    public string ToLocation(string link)
    {
        var target = link.TrimStart('#');
        var dot = LastSeparator(target, '.');
        if (dot <= 0) return target;
        var sheet = target[..dot].Trim('\'').Replace("''", "'", StringComparison.Ordinal);
        return Quoted(sheet) + "!" + target[(dot + 1)..];
    }

    public string FromLocation(string location)
    {
        var bang = LastSeparator(location, '!');
        if (bang <= 0) return "#" + location;
        var sheet = location[..bang];
        if (sheet.Length >= 2 && sheet[0] == '\'' && sheet[^1] == '\'') sheet = sheet[1..^1].Replace("''", "'", StringComparison.Ordinal);
        return "#" + (sheet.Contains('\'') || sheet.Contains('.') ? "'" + sheet.Replace("'", "''", StringComparison.Ordinal) + "'" : sheet) + "." + location[(bang + 1)..];
    }

    private string Quoted(string sheet) =>
        sheet.Any(c => !char.IsLetterOrDigit(c) && c != '_')
            ? "'" + sheet.Replace("'", "''", StringComparison.Ordinal) + "'"
            : sheet;

    public Uri? External(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var absolute) && !link.StartsWith('/') ? absolute
        : Uri.TryCreate(link.Replace('\\', '/'), UriKind.Relative, out var relative) ? relative : null;

    private int LastSeparator(string text, char separator)
    {
        var quoted = false;
        var found = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\'') quoted = !quoted;
            else if (text[i] == separator && !quoted) found = i;
        }
        return found;
    }
}
