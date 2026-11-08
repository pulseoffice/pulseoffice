namespace PulseOffice.Formats;

public sealed record PreservedFieldPair(string WordKeyword, string OdfElement);

public interface IPreservedFieldTable
{
    string? OdfElementFor(string instruction);

    string WordInstructionFor(string? odfElement, string shown);

    string Keyword(string instruction);

    string? Prompt(string instruction);
}

public sealed class PreservedFieldTable : IPreservedFieldTable
{
    private const string Placeholder = "MACROBUTTON";

    private readonly IReadOnlyList<PreservedFieldPair> _pairs =
    [
        new("PRINTDATE", "print-date"),
        new("EDITTIME", "editing-duration"),
        new("REVNUM", "editing-cycles"),
        new("USERINITIALS", "author-initials"),
        new("USERNAME", "author-name"),
        new("TEMPLATE", "template-name"),
        new("FILLIN", "text-input"),
        new(Placeholder, "placeholder"),
    ];

    public string Keyword(string instruction)
    {
        var trimmed = instruction.AsSpan().TrimStart();
        var end = 0;
        while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end])) end++;
        return trimmed[..end].ToString().ToUpperInvariant();
    }

    public string? OdfElementFor(string instruction)
    {
        var keyword = Keyword(instruction);
        return _pairs.FirstOrDefault(pair => pair.WordKeyword == keyword)?.OdfElement;
    }

    public string WordInstructionFor(string? odfElement, string shown)
    {
        var keyword = _pairs.FirstOrDefault(pair => pair.OdfElement == odfElement)?.WordKeyword;
        return keyword switch
        {
            null => " QUOTE " + Quote(shown) + " ",
            Placeholder => " MACROBUTTON NoMacro " + shown.Replace('\n', ' ') + " ",
            _ => " " + keyword + " ",
        };
    }

    public string? Prompt(string instruction)
    {
        var open = instruction.IndexOf('"', StringComparison.Ordinal);
        if (open < 0) return null;
        var prompt = new System.Text.StringBuilder();
        for (var index = open + 1; index < instruction.Length; index++)
        {
            if (instruction[index] == '\\' && index + 1 < instruction.Length && instruction[index + 1] == '"') index++;
            else if (instruction[index] == '"') return prompt.ToString();
            prompt.Append(instruction[index]);
        }
        return prompt.ToString();
    }

    private string Quote(string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
