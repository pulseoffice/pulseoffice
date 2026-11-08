namespace PulseOffice.Text.Rich;

public interface IParagraphBreaks
{
    string Normalize(string text);
}

public sealed class ParagraphBreaks : IParagraphBreaks
{
    private const char ParagraphSeparator = (char)0x2029;
    private const char NextLine = (char)0x85;

    public string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Replace(ParagraphSeparator, '\n').Replace(NextLine, '\n');
    }
}
