using System.Globalization;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Interchange.Text.Assembly;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Interchange.Text.Rtf;

public interface IRtfVocabulary
{
    RtfDestination? Destination(string word, bool starred);

    int CharsetCodePage(int charset);

    NumberFormat ListFormat(int nfc);

    int ListCode(NumberFormat format);

    string? ParagraphStyle(string name, StyleSheet sheet);

    string? CharacterStyle(string name, StyleSheet sheet);

    string ExportName(string styleName);

    RunShape ApplyField(RunShape shape, string instruction);

    string? FieldInstruction(TextFieldKind kind, string? argument);
}

public sealed class RtfVocabulary : IRtfVocabulary
{
    private readonly Dictionary<string, RtfDestination> _destinations = new(StringComparer.Ordinal)
    {
        ["fonttbl"] = RtfDestination.FontTable,
        ["colortbl"] = RtfDestination.ColorTable,
        ["stylesheet"] = RtfDestination.StyleSheet,
        ["info"] = RtfDestination.InfoText,
        ["title"] = RtfDestination.InfoText,
        ["author"] = RtfDestination.InfoText,
        ["subject"] = RtfDestination.InfoText,
        ["listtable"] = RtfDestination.ListTable,
        ["listoverridetable"] = RtfDestination.ListOverrideTable,
        ["bkmkstart"] = RtfDestination.BookmarkStart,
        ["bkmkend"] = RtfDestination.BookmarkEnd,
        ["footnote"] = RtfDestination.Body,
        ["field"] = RtfDestination.Body,
        ["fldinst"] = RtfDestination.FieldInstruction,
        ["fldrslt"] = RtfDestination.FieldResult,
        ["pict"] = RtfDestination.Picture,
        ["object"] = RtfDestination.ObjectWrapper,
        ["result"] = RtfDestination.Body,
        ["shppict"] = RtfDestination.Body,
        ["nonshppict"] = RtfDestination.Skip,
        ["header"] = RtfDestination.Body,
        ["footer"] = RtfDestination.Body,
        ["shpinst"] = RtfDestination.Skip,
        ["shptxt"] = RtfDestination.Body,
    };

    private readonly HashSet<string> _skipped = new(StringComparer.Ordinal)
    {
        "headerl", "headerr", "headerf", "footerl", "footerr", "footerf", "pntext", "listtext", "pn", "annotation", "atnid",
        "atnauthor", "atndate", "atnref", "atrfstart", "atrfend", "xmlnstbl", "generator", "themedata", "colorschememapping", "latentstyles",
        "datastore", "rsidtbl", "mmathPr", "pgdsctbl", "docvar", "txe", "ftnsep", "ftnsepc", "aftnsep", "aftnsepc", "operator", "keywords",
        "comment", "doccomm", "company", "manager", "category", "buptim", "creatim", "revtim", "printim", "hlinkbase", "userprops",
        "falt", "panose", "fname", "leveltext", "levelnumbers", "listname", "liststylename", "sp", "sn", "sv", "shprslt",
        "fldtype", "formfield", "datafield", "objdata", "objclass", "objname", "blipuid", "picprop", "bkmkcolf", "template", "wgrffmtfilter",
        "passwordhash", "protusertbl", "revtbl", "filetbl", "fchars", "lchars", "nextfile", "ud", "upr", "do", "xe", "tc", "tcn", "pnseclvl",
    };

    private readonly HashSet<string> _starredKnown = new(StringComparer.Ordinal) { "cs", "ts", "ds", "shppict", "pn", "bkmkstart", "bkmkend", "fldinst", "listtable", "listoverridetable", "ftnalt" };

    public RtfDestination? Destination(string word, bool starred)
    {
        if (_destinations.TryGetValue(word, out var destination)) return destination;
        if (_skipped.Contains(word)) return RtfDestination.Skip;
        if (starred && !_starredKnown.Contains(word)) return RtfDestination.Skip;
        return null;
    }

    public int CharsetCodePage(int charset) => charset switch
    {
        0 => 1252,
        2 => 1252,
        77 => 10000,
        128 => 932,
        129 => 949,
        134 => 936,
        136 => 950,
        161 => 1253,
        162 => 1254,
        163 => 1258,
        177 => 1255,
        178 => 1256,
        186 => 1257,
        204 => 1251,
        222 => 874,
        238 => 1250,
        255 => 437,
        _ => 0,
    };

    public NumberFormat ListFormat(int nfc) => nfc switch
    {
        23 => NumberFormat.Bullet,
        255 => NumberFormat.None,
        1 => NumberFormat.UpperRoman,
        2 => NumberFormat.LowerRoman,
        3 => NumberFormat.UpperLetter,
        4 => NumberFormat.LowerLetter,
        _ => NumberFormat.Arabic,
    };

    public int ListCode(NumberFormat format) => format switch
    {
        NumberFormat.Bullet => 23,
        NumberFormat.None => 255,
        NumberFormat.UpperRoman => 1,
        NumberFormat.LowerRoman => 2,
        NumberFormat.UpperLetter => 3,
        NumberFormat.LowerLetter => 4,
        _ => 0,
    };

    public string? ParagraphStyle(string name, StyleSheet sheet)
    {
        var trimmed = name.Trim().TrimEnd(';');
        if (trimmed.StartsWith("heading ", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(trimmed.AsSpan(8), NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) && level is >= 1 and <= 10)
            return DefaultStyleNames.HeadingLevel(level);
        if (trimmed.Equals("Normal", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("Default", StringComparison.OrdinalIgnoreCase)) return null;
        if (trimmed.Equals("Body Text", StringComparison.OrdinalIgnoreCase)) return DefaultStyleNames.TextBody;
        if (trimmed.Equals("footnote text", StringComparison.OrdinalIgnoreCase)) return DefaultStyleNames.Footnote;
        if (trimmed.Equals("endnote text", StringComparison.OrdinalIgnoreCase)) return DefaultStyleNames.Endnote;
        return sheet.Paragraph.Find(trimmed)?.Name ?? sheet.Paragraph.All.FirstOrDefault(style => string.Equals(style.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase))?.Name;
    }

    public string? CharacterStyle(string name, StyleSheet sheet)
    {
        var trimmed = name.Trim().TrimEnd(';');
        return sheet.Character.Find(trimmed)?.Name;
    }

    public string ExportName(string styleName) =>
        styleName.StartsWith("Heading ", StringComparison.Ordinal) ? "heading " + styleName[8..] : styleName;

    public RunShape ApplyField(RunShape shape, string instruction)
    {
        var words = Words(instruction);
        if (words.Count == 0) return shape;
        var argument = words.Count > 1 ? words[1] : null;
        switch (words[0].ToUpperInvariant())
        {
            case "HYPERLINK":
                var anchor = words.FindIndex(word => word == "\\l");
                var target = anchor >= 0 && anchor + 1 < words.Count ? "#" + words[anchor + 1] : argument;
                return target is { Length: > 0 } && Safe(target) ? shape with { Hyperlink = target } : shape;
        }
        var kind = Kind(words[0].ToUpperInvariant());
        return kind == TextFieldKind.None ? shape : shape with { Field = kind, FieldArgument = kind is TextFieldKind.Variable or TextFieldKind.Reference or TextFieldKind.Sequence ? argument : null };
    }

    public string? FieldInstruction(TextFieldKind kind, string? argument) => kind switch
    {
        TextFieldKind.PageNumber => "PAGE",
        TextFieldKind.PageCount => "NUMPAGES",
        TextFieldKind.Date => "DATE",
        TextFieldKind.Time => "TIME",
        TextFieldKind.Author => "AUTHOR",
        TextFieldKind.Title => "TITLE",
        TextFieldKind.Subject => "SUBJECT",
        TextFieldKind.FileName => "FILENAME",
        TextFieldKind.Keywords => "KEYWORDS",
        TextFieldKind.WordCount => "NUMWORDS",
        TextFieldKind.CharacterCount => "NUMCHARS",
        TextFieldKind.CreatedDate => "CREATEDATE",
        TextFieldKind.ModifiedDate => "SAVEDATE",
        TextFieldKind.Description => "COMMENTS",
        TextFieldKind.Variable when argument is { Length: > 0 } => "DOCVARIABLE " + Quote(argument),
        TextFieldKind.Reference when argument is { Length: > 0 } => "REF " + Quote(argument),
        TextFieldKind.Sequence when argument is { Length: > 0 } => "SEQ " + Quote(argument),
        _ => null,
    };

    private TextFieldKind Kind(string word) => word switch
    {
        "PAGE" => TextFieldKind.PageNumber,
        "NUMPAGES" => TextFieldKind.PageCount,
        "DATE" => TextFieldKind.Date,
        "TIME" => TextFieldKind.Time,
        "AUTHOR" => TextFieldKind.Author,
        "TITLE" => TextFieldKind.Title,
        "SUBJECT" => TextFieldKind.Subject,
        "FILENAME" => TextFieldKind.FileName,
        "KEYWORDS" => TextFieldKind.Keywords,
        "NUMWORDS" => TextFieldKind.WordCount,
        "NUMCHARS" => TextFieldKind.CharacterCount,
        "CREATEDATE" => TextFieldKind.CreatedDate,
        "SAVEDATE" => TextFieldKind.ModifiedDate,
        "COMMENTS" => TextFieldKind.Description,
        "DOCVARIABLE" => TextFieldKind.Variable,
        "REF" => TextFieldKind.Reference,
        "SEQ" => TextFieldKind.Sequence,
        _ => TextFieldKind.None,
    };

    private string Quote(string value) => "\"" + value.Replace("\"", string.Empty, StringComparison.Ordinal) + "\"";

    private bool Safe(string link) =>
        link.StartsWith('#') || Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto" or "ftp" or "file";

    private List<string> Words(string instruction)
    {
        var words = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var character in instruction)
        {
            if (character == '"')
            {
                if (quoted) words.Add(current.ToString());
                else if (current.Length > 0) words.Add(current.ToString());
                current.Clear();
                quoted = !quoted;
                continue;
            }
            if (!quoted && char.IsWhiteSpace(character))
            {
                if (current.Length > 0) words.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(character);
        }
        if (current.Length > 0) words.Add(current.ToString());
        return words;
    }
}
