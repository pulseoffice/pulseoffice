using System.Globalization;
using System.Text;

namespace PulseOffice.Security;

public interface IPdfAppendedObjects
{
    int Reserve();

    void Write(int id, string body);

    int WriteStream(byte[] payload, string? extraDict);
}

public sealed record PdfSignatureText(string Resources, IReadOnlyList<string> Lines);

public interface IPdfSignatureFont
{
    PdfSignatureText Typeset(IReadOnlyList<string> lines, double size, IPdfAppendedObjects objects);
}

public sealed class StandardPdfSignatureFont : IPdfSignatureFont
{
    private readonly PdfLiteral _literal = new();

    public PdfSignatureText Typeset(IReadOnlyList<string> lines, double size, IPdfAppendedObjects objects)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(objects);
        var id = objects.Reserve();
        objects.Write(id, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        var font = string.Create(CultureInfo.InvariantCulture, $"/Helv {Math.Round(size, 2):0.##} Tf ");
        return new PdfSignatureText($"<< /Helv {id} 0 R >>", [.. lines.Select(line => font + "(" + _literal.Escape(WinAnsi(line)) + ") Tj")]);
    }

    private string WinAnsi(string text) =>
        new([.. text.Select(character => character is >= ' ' and <= 'ÿ' and not ('\u007F' or (>= '\u0080' and <= '\u009F')) ? character : '?')]);
}

internal sealed class AppendedObjects(PdfIncrementalUpdate update, int firstId) : IPdfAppendedObjects
{
    private int _next = firstId;

    public int Next => _next;

    public int Reserve() => _next++;

    public void Write(int id, string body) => update.Add(id, body);

    public int WriteStream(byte[] payload, string? extraDict)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var id = Reserve();
        var dictionary = new StringBuilder("<< /Length ").Append(payload.Length.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(extraDict)) dictionary.Append(' ').Append(extraDict);
        update.Add(id, dictionary.Append(" >>\nstream\n").Append(Encoding.Latin1.GetString(payload)).Append("\nendstream").ToString());
        return id;
    }
}
