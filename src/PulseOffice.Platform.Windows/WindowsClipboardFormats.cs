using System.Globalization;
using System.Text;
using PulseOffice.Platform;

namespace PulseOffice.Platform.Windows;

public sealed class WindowsClipboardFormats() : NamedClipboardFormats("HTML Format", "Rich Text Format")
{
    private const string StartFragment = "<!--StartFragment-->";
    private const string EndFragment = "<!--EndFragment-->";
    private const string HeaderTemplate = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";

    public override byte[] EncodeHtml(string html)
    {
        var body = html ?? string.Empty;
        var opening = body.IndexOf(StartFragment, StringComparison.Ordinal);
        var closing = opening >= 0 ? body.IndexOf(EndFragment, opening + StartFragment.Length, StringComparison.Ordinal) : -1;
        var (document, fragmentStart, fragmentEnd) = closing >= 0
            ? (body, opening + StartFragment.Length, closing)
            : Wrapped(body);
        var headerLength = string.Format(CultureInfo.InvariantCulture, HeaderTemplate, 0, 0, 0, 0).Length;
        var bytes = Encoding.UTF8.GetBytes(document);
        var start = headerLength + Encoding.UTF8.GetByteCount(document.AsSpan(0, fragmentStart));
        var end = headerLength + Encoding.UTF8.GetByteCount(document.AsSpan(0, fragmentEnd));
        var header = string.Format(CultureInfo.InvariantCulture, HeaderTemplate, headerLength, headerLength + bytes.Length, start, end);
        return [.. Encoding.ASCII.GetBytes(header), .. bytes];
    }

    private (string Document, int FragmentStart, int FragmentEnd) Wrapped(string body)
    {
        const string Opening = "<html><body>" + StartFragment;
        return (Opening + body + EndFragment + "</body></html>", Opening.Length, Opening.Length + body.Length);
    }

    public override string? DecodeHtml(byte[] data)
    {
        if (data is not { Length: > 0 }) return null;
        var text = Decode(data);
        if (!text.StartsWith("Version:", StringComparison.Ordinal)) return text;
        var start = Offset(text, "StartHTML:");
        var end = Offset(text, "EndHTML:");
        if (start < 0) return text;
        var utf8 = Encoding.UTF8.GetBytes(text);
        var length = (end > start && end <= utf8.Length ? end : utf8.Length) - start;
        return start < utf8.Length ? Encoding.UTF8.GetString(utf8, start, length) : null;
    }

    private int Offset(string text, string key)
    {
        var at = text.IndexOf(key, StringComparison.Ordinal);
        if (at < 0) return -1;
        var from = at + key.Length;
        var to = text.IndexOfAny(['\r', '\n'], from);
        return int.TryParse(text.AsSpan(from, (to < 0 ? text.Length : to) - from), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : -1;
    }
}
