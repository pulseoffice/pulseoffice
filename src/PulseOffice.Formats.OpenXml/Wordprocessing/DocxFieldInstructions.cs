using System.Text;
using PulseOffice.Formats;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed record DocxField(TextFieldKind Kind, string? Argument, FieldDetail? Detail = null)
{
    public bool? Hidden { get; init; }
}

public sealed record DocxNestedField(string Instruction, string Result);

public sealed record DocxInstructionPart(string Text, string? Reference = null)
{
    public bool Nested => Reference is not null;
}

public sealed record DocxResolvedInstruction(string Text, bool Lossy);

public static class DocxNoteBookmarks
{
    public const string Prefix = "_RefNote_";
}

public sealed class DocxFieldInstructions(IPreservedFieldTable preserved, IWordDatePictures pictures)
{
    private const string NativeMarker = "urn:pulseoffice:odf-field\n";

    private const int NativeLimit = 65536;

    private readonly System.Xml.Linq.XNamespace OdfText = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    private const string ChapterStyle = "Heading 1";

    private readonly IReadOnlyList<(TextFieldKind Kind, string Keyword)> _simple =
    [
        (TextFieldKind.PageNumber, "PAGE"),
        (TextFieldKind.PageCount, "NUMPAGES"),
        (TextFieldKind.Author, "AUTHOR"),
        (TextFieldKind.Title, "TITLE"),
        (TextFieldKind.Subject, "SUBJECT"),
        (TextFieldKind.FileName, "FILENAME"),
        (TextFieldKind.WordCount, "NUMWORDS"),
        (TextFieldKind.CharacterCount, "NUMCHARS"),
        (TextFieldKind.Keywords, "KEYWORDS"),
        (TextFieldKind.Description, "COMMENTS"),
        (TextFieldKind.CreatedDate, "CREATEDATE"),
        (TextFieldKind.ModifiedDate, "SAVEDATE"),
        (TextFieldKind.Date, "DATE"),
        (TextFieldKind.Time, "TIME"),
    ];

    public string? Instruction(TextRun run, IReadOnlyDictionary<string, string> userVariables)
    {
        var argument = run.FieldArgument;
        var detail = run.FieldDetail;
        if (run.Field == TextFieldKind.Preserved)
            return argument is { Length: > 0 } native ? native : Preserved(detail?.NativeOdf, run.Text);
        foreach (var (kind, keyword) in _simple)
        {
            if (kind != run.Field) continue;
            return detail?.Format is { Length: > 0 } format && kind is TextFieldKind.Date or TextFieldKind.Time or TextFieldKind.CreatedDate or TextFieldKind.ModifiedDate
                ? $" {keyword} \\@ {Quote(pictures.ToWord(format))} "
                : $" {keyword} ";
        }

        if (string.IsNullOrEmpty(argument) && run.Field is not TextFieldKind.Chapter) return null;
        return run.Field switch
        {
            TextFieldKind.Bookmark or TextFieldKind.Reference => (detail?.Format ?? "text") switch
            {
                "page" => $" PAGEREF {argument} \\h ",
                "number" => $" REF {argument} \\r \\h ",
                "direction" => $" REF {argument} \\p \\h ",
                _ => $" REF {argument} \\h ",
            },
            TextFieldKind.NoteReference => $" NOTEREF {DocxNoteBookmarks.Prefix}{argument} \\h ",
            TextFieldKind.Sequence => $" SEQ {argument} \\* ARABIC ",
            TextFieldKind.Variable => userVariables.ContainsKey(argument!) ? $" DOCVARIABLE {argument} " : $" REF {argument} \\h ",
            TextFieldKind.VariableSet => $" SET {argument} {Quote(detail?.Format ?? run.Text)} ",
            TextFieldKind.HiddenParagraph => $" IF {WordCondition(argument!)} \"\" \"\" ",
            TextFieldKind.ConditionalText => $" IF {WordCondition(argument!)} {Quote(detail?.WhenTrue ?? string.Empty)} {Quote(detail?.WhenFalse ?? string.Empty)} ",
            TextFieldKind.CustomProperty => $" DOCPROPERTY {Quote(argument!)} ",
            TextFieldKind.Database => argument!.Contains(' ', StringComparison.Ordinal) ? $" MERGEFIELD {Quote(argument)} " : $" MERGEFIELD {argument} ",
            TextFieldKind.Citation => $" CITATION {argument} ",
            TextFieldKind.Chapter => argument == "number" ? $" STYLEREF {Quote(ChapterStyle)} \\n " : $" STYLEREF {Quote(ChapterStyle)} ",
            _ => null,
        };
    }

    public DocxField? Parse(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return null;
        var words = Split(instruction);
        if (words.Count == 0) return null;
        var keyword = words[0].Text.ToUpperInvariant();
        var argument = words.Count > 1 && !words[1].Text.StartsWith('\\') ? words[1].Text : null;
        var switches = words.Where(word => !word.Quoted && word.Text.StartsWith('\\')).Select(word => word.Text.ToLowerInvariant()).ToList();
        foreach (var (kind, simple) in _simple)
        {
            if (simple != keyword) continue;
            var format = Switch(words, "\\@");
            return new DocxField(kind, null, format is null ? null : new FieldDetail { Format = pictures.FromWord(format) });
        }

        return keyword switch
        {
            "REF" when argument is not null => new DocxField(TextFieldKind.Bookmark, argument, Reference(switches)),
            "PAGEREF" when argument is not null => new DocxField(TextFieldKind.Bookmark, argument, new FieldDetail { Format = "page" }),
            "NOTEREF" when argument is not null => new DocxField(TextFieldKind.NoteReference, argument),
            "SEQ" when argument is not null && PlainSequence(words, switches) => new DocxField(TextFieldKind.Sequence, argument),
            "DOCVARIABLE" when argument is not null => new DocxField(TextFieldKind.Variable, argument),
            "SET" when argument is not null => new DocxField(TextFieldKind.VariableSet, argument, new FieldDetail { Format = words.Count > 2 ? words[2].Text : string.Empty }),
            "IF" when words.Count >= 4 => Conditional(words),
            "DOCPROPERTY" when argument is not null => new DocxField(TextFieldKind.CustomProperty, argument),
            "MERGEFIELD" when argument is not null => new DocxField(TextFieldKind.Database, argument),
            "CITATION" when argument is not null => new DocxField(TextFieldKind.Citation, argument),
            "STYLEREF" => new DocxField(TextFieldKind.Chapter, switches.Contains("\\n") ? "number" : null),
            _ when _structural.Contains(keyword) => null,
            _ => new DocxField(TextFieldKind.Preserved, instruction),
        };
    }

    private bool PlainSequence(IReadOnlyList<(string Text, bool Quoted)> words, IReadOnlyList<string> switches) =>
        !switches.Any(item => item is "\\c" or "\\h" or "\\n" or "\\r" or "\\s" || item.StartsWith("\\#", StringComparison.Ordinal))
        && Switch(words, "\\@") is null
        && words.Select((word, i) => (word, i)).Where(pair => !pair.word.Quoted && pair.word.Text.StartsWith("\\*", StringComparison.Ordinal))
            .Select(pair => pair.word.Text.Length > 2 ? pair.word.Text[2..] : pair.i + 1 < words.Count ? words[pair.i + 1].Text : string.Empty)
            .All(shape => shape.Equals("ARABIC", StringComparison.OrdinalIgnoreCase) || shape.Equals("MERGEFORMAT", StringComparison.OrdinalIgnoreCase));

    private readonly IReadOnlySet<string> _structural = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "HYPERLINK", "XE", "TOC", "INDEX", "BIBLIOGRAPHY", "FORMTEXT", "FORMCHECKBOX", "FORMDROPDOWN",
    };

    public bool Structural(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return false;
        var words = Split(instruction);
        return words.Count > 0 && _structural.Contains(words[0].Text);
    }

    public DocxField? WithNativeData(DocxField? field, string? data)
    {
        if (field is null || NativeOdf(data) is not { } native) return field;
        if (HiddenParagraph(native) is { } hidden) return hidden;
        if (field.Kind != TextFieldKind.Preserved) return field;
        return field with
        {
            Argument = preserved.Keyword(field.Argument ?? string.Empty) == "QUOTE" ? null : field.Argument,
            Detail = (field.Detail ?? new FieldDetail()) with { NativeOdf = native },
        };
    }

    public string? Data(TextRun run) =>
        run is { Field: TextFieldKind.HiddenParagraph, FieldArgument: { Length: > 0 } condition }
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes(NativeMarker + new System.Xml.Linq.XElement(OdfText + "hidden-paragraph",
                new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "text", OdfText.NamespaceName),
                new System.Xml.Linq.XAttribute(OdfText + "condition", "ooow:" + condition),
                new System.Xml.Linq.XAttribute(OdfText + "is-hidden", run.Hidden == true ? "true" : "false")).ToString(System.Xml.Linq.SaveOptions.DisableFormatting)))
        : run.Field == TextFieldKind.Preserved && run.FieldDetail?.NativeOdf is { Length: > 0 and <= NativeLimit } native
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes(NativeMarker + native))
            : null;

    public string Shown(DocxField field)
    {
        if (field.Kind != TextFieldKind.Preserved || field.Argument is not { } instruction || preserved.Keyword(instruction) != "MACROBUTTON") return string.Empty;
        var words = Split(instruction);
        return words.Count > 2 ? string.Join(' ', words.Skip(2).Select(word => word.Text)) : string.Empty;
    }

    private string? NativeOdf(string? data)
    {
        if (string.IsNullOrWhiteSpace(data) || data.Length > NativeLimit * 2) return null;
        var bytes = new byte[data.Length];
        if (!Convert.TryFromBase64String(data.Trim(), bytes, out var written)) return null;
        var text = Encoding.UTF8.GetString(bytes, 0, written);
        return text.StartsWith(NativeMarker, StringComparison.Ordinal) && text.Length > NativeMarker.Length ? text[NativeMarker.Length..] : null;
    }

    private DocxField? HiddenParagraph(string native)
    {
        System.Xml.Linq.XElement element;
        try
        {
            element = System.Xml.Linq.XElement.Parse(native);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
        if (element.Name != OdfText + "hidden-paragraph" || (string?)element.Attribute(OdfText + "condition") is not { Length: > 0 } condition) return null;
        var colon = condition.IndexOf(':', StringComparison.Ordinal);
        var plain = colon > 0 && condition[..colon] is "ooow" or "ooo" or "oooc" ? condition[(colon + 1)..] : condition;
        return plain.Length == 0 ? null : new DocxField(TextFieldKind.HiddenParagraph, plain) { Hidden = (string?)element.Attribute(OdfText + "is-hidden") == "true" };
    }

    private string Preserved(string? native, string shown)
    {
        var element = OdfElement(native);
        var instruction = preserved.WordInstructionFor(element?.Name.LocalName, shown);
        return element is not null && element.Name == OdfText + "text-input" && (string?)element.Attribute(OdfText + "description") is { Length: > 0 } prompt
            ? instruction.TrimEnd() + " " + Quote(prompt) + " "
            : instruction;
    }

    private System.Xml.Linq.XElement? OdfElement(string? native)
    {
        if (native is null) return null;
        try
        {
            return System.Xml.Linq.XElement.Parse(native);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    public string? HyperlinkTarget(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return null;
        var words = Split(instruction);
        if (words.Count == 0 || !words[0].Text.Equals("HYPERLINK", StringComparison.OrdinalIgnoreCase)) return null;
        string? address = null;
        string? location = null;
        for (var i = 1; i < words.Count; i++)
        {
            if (!words[i].Quoted && words[i].Text.StartsWith('\\'))
            {
                if (words[i].Text.Equals("\\l", StringComparison.OrdinalIgnoreCase) && i + 1 < words.Count) location = words[++i].Text;
                else if (words[i].Text.Length == 2 && i + 1 < words.Count && words[i + 1].Quoted) i++;
                continue;
            }

            address ??= words[i].Text;
        }

        if (string.IsNullOrEmpty(address)) return string.IsNullOrEmpty(location) ? null : "#" + location;
        return string.IsNullOrEmpty(location) ? address : address + "#" + location;
    }

    private FieldDetail? Reference(IReadOnlyList<string> switches) =>
        switches.Contains("\\p") ? new FieldDetail { Format = "direction" }
        : switches.Contains("\\r") || switches.Contains("\\n") || switches.Contains("\\w") ? new FieldDetail { Format = "number" }
        : null;

    private DocxField Conditional(IReadOnlyList<(string Text, bool Quoted)> words)
    {
        var condition = string.Join(' ', words.Skip(1).Take(words.Count - 3).Select(word => word.Quoted ? Quote(word.Text) : word.Text));
        return new DocxField(TextFieldKind.ConditionalText, OurCondition(condition), new FieldDetail { WhenTrue = words[^2].Text, WhenFalse = words[^1].Text });
    }

    private string WordCondition(string condition) => condition.Replace("==", "=", StringComparison.Ordinal).Replace("!=", "<>", StringComparison.Ordinal);

    private string OurCondition(string condition)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < condition.Length; i++)
        {
            var c = condition[i];
            if (c == '<' && i + 1 < condition.Length && condition[i + 1] == '>')
            {
                builder.Append("!=");
                i++;
            }
            else if (c == '=' && (i == 0 || condition[i - 1] is not ('<' or '>' or '!' or '=')) && (i + 1 >= condition.Length || condition[i + 1] != '='))
            {
                builder.Append("==");
            }
            else builder.Append(c);
        }
        return builder.ToString();
    }

    private string? Switch(IReadOnlyList<(string Text, bool Quoted)> words, string name)
    {
        for (var i = 0; i < words.Count; i++)
        {
            if (words[i].Quoted || !words[i].Text.StartsWith(name, StringComparison.Ordinal)) continue;
            if (words[i].Text.Length > name.Length) return words[i].Text[name.Length..].Trim('"');
            if (i + 1 < words.Count) return words[i + 1].Text;
        }
        return null;
    }

    private string Quote(string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private List<(string Text, bool Quoted)> Split(string instruction)
    {
        var words = new List<(string, bool)>();
        var i = 0;
        while (i < instruction.Length)
        {
            if (char.IsWhiteSpace(instruction[i]))
            {
                i++;
                continue;
            }
            var builder = new StringBuilder();
            if (instruction[i] == '"')
            {
                i++;
                while (i < instruction.Length && instruction[i] != '"')
                {
                    if (instruction[i] == '\\' && i + 1 < instruction.Length && instruction[i + 1] == '"') i++;
                    builder.Append(instruction[i++]);
                }
                i++;
                words.Add((builder.ToString(), true));
                continue;
            }
            while (i < instruction.Length && !char.IsWhiteSpace(instruction[i])) builder.Append(instruction[i++]);
            words.Add((builder.ToString(), false));
        }
        return words;
    }

    public const char NestedOpen = '\uE000';

    public const char NestedClose = '\uE001';

    private readonly IReadOnlySet<string> _conditionWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "EQ", "NEQ", "NE", "LT", "GT", "LEQ", "LE", "GEQ", "GE", "AND", "OR", "NOT", "TRUE", "FALSE",
    };

    public string Placeholder(int index) => NestedOpen + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + NestedClose;

    public DocxResolvedInstruction Resolve(string instruction, IReadOnlyList<DocxNestedField> nested)
    {
        if (nested.Count == 0 || instruction.IndexOf(NestedOpen) < 0) return new DocxResolvedInstruction(instruction, false);
        var conditional = Split(instruction) is { Count: > 0 } words && words[0].Text.Equals("IF", StringComparison.OrdinalIgnoreCase);
        var lossy = false;
        var builder = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < instruction.Length; i++)
        {
            var c = instruction[i];
            if (c == '\\' && i + 1 < instruction.Length && instruction[i + 1] == '"')
            {
                builder.Append(c).Append('"');
                i++;
                continue;
            }
            if (c == '"')
            {
                if (!quoted && Wrapped(instruction, i) is { } whole)
                {
                    var (text, exact) = Substitute(nested, whole.Index, conditional);
                    lossy |= !exact;
                    builder.Append(text);
                    i = whole.End;
                    continue;
                }
                quoted = !quoted;
                builder.Append(c);
                continue;
            }
            if (c != NestedOpen)
            {
                builder.Append(c);
                continue;
            }
            var close = instruction.IndexOf(NestedClose, i + 1);
            if (close < 0 || !int.TryParse(instruction[(i + 1)..close], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index) || index >= nested.Count)
            {
                lossy = true;
                i = close < 0 ? instruction.Length : close;
                continue;
            }
            if (quoted)
            {
                lossy = true;
                builder.Append(nested[index].Result.Replace("\"", "\\\"", StringComparison.Ordinal));
            }
            else
            {
                var (text, exact) = Substitute(nested, index, conditional);
                lossy |= !exact;
                builder.Append(text);
            }
            i = close;
        }
        return new DocxResolvedInstruction(builder.ToString(), lossy);
    }

    private (int Index, int End)? Wrapped(string instruction, int quote)
    {
        if (quote + 1 >= instruction.Length || instruction[quote + 1] != NestedOpen) return null;
        var close = instruction.IndexOf(NestedClose, quote + 2);
        if (close < 0 || close + 1 >= instruction.Length || instruction[close + 1] != '"') return null;
        return int.TryParse(instruction[(quote + 2)..close], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index)
            ? (index, close + 1)
            : null;
    }

    private (string Text, bool Exact) Substitute(IReadOnlyList<DocxNestedField> nested, int index, bool conditional)
    {
        if (index >= nested.Count) return (string.Empty, false);
        var field = nested[index];
        if (conditional && Reference(field.Instruction) is { } name) return (name, true);
        return (Quote(field.Result), false);
    }

    private string? Reference(string instruction) =>
        Parse(instruction) is { Kind: TextFieldKind.Database or TextFieldKind.Variable or TextFieldKind.Bookmark, Argument: { } name } field
        && (field.Kind != TextFieldKind.Bookmark || field.Detail is null)
        && Identifier(name)
            ? name
            : null;

    private bool Identifier(string name) =>
        name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c == '_') && !_conditionWords.Contains(name);

    public IReadOnlyList<DocxInstructionPart>? Composite(TextRun run, IReadOnlyDictionary<string, string> userVariables, Func<string, bool> referenced)
    {
        if (run.Field != TextFieldKind.ConditionalText || run.FieldArgument is not { Length: > 0 } argument) return null;
        var condition = WordCondition(argument);
        var parts = new List<DocxInstructionPart> { new(" IF ") };
        var nested = false;
        var plain = new StringBuilder();
        var i = 0;
        while (i < condition.Length)
        {
            var c = condition[i];
            if (c is '"' or '\'')
            {
                var end = condition.IndexOf(c, i + 1);
                end = end < 0 ? condition.Length - 1 : end;
                plain.Append(condition, i, end - i + 1);
                i = end + 1;
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < condition.Length && (char.IsLetterOrDigit(condition[i]) || condition[i] == '_')) i++;
                var word = condition[start..i];
                if (i < condition.Length && condition[i] is '.' or ',' || !Identifier(word))
                {
                    plain.Append(word);
                    continue;
                }
                if (plain.Length > 0) parts.Add(new DocxInstructionPart(plain.ToString()));
                plain.Clear();
                parts.Add(new DocxInstructionPart(userVariables.ContainsKey(word) ? $" DOCVARIABLE {word} " : referenced(word) ? $" REF {word} \\h " : $" MERGEFIELD {word} ", word));
                nested = true;
                continue;
            }
            plain.Append(c);
            i++;
        }
        if (!nested) return null;
        plain.Append(' ').Append(Quote(run.FieldDetail?.WhenTrue ?? string.Empty)).Append(' ').Append(Quote(run.FieldDetail?.WhenFalse ?? string.Empty)).Append(' ');
        parts.Add(new DocxInstructionPart(plain.ToString()));
        return parts;
    }
}
