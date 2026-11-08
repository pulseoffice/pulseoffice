using System.Text;

namespace PulseOffice.Platform;

public interface IRichClipboardFormats
{
    string HtmlFormat { get; }

    string RtfFormat { get; }

    byte[] EncodeHtml(string html);

    string? DecodeHtml(byte[] data);

    byte[] EncodeRtf(string rtf);

    string? DecodeRtf(byte[] data);

    string NativeFormat { get; }

    string PackageFormat(string mediaType);
}

public interface IRichClipboardFormatsProvider
{
    IRichClipboardFormats Current { get; }
}

public class NamedClipboardFormats(string htmlFormat, string rtfFormat) : IRichClipboardFormats
{
    public string HtmlFormat { get; } = htmlFormat;

    public string RtfFormat { get; } = rtfFormat;

    public virtual byte[] EncodeHtml(string html) => Encoding.UTF8.GetBytes(html ?? string.Empty);

    public virtual string? DecodeHtml(byte[] data) => data is { Length: > 0 } ? Decode(data) : null;

    public string NativeFormat => "pulseoffice-exchange";

    public string PackageFormat(string mediaType) =>
        "pulseoffice-package-" + new string([.. (mediaType ?? string.Empty).Select(character => char.IsAsciiLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')]);

    public byte[] EncodeRtf(string rtf) => Encoding.Latin1.GetBytes(rtf ?? string.Empty);

    public string? DecodeRtf(byte[] data) => data is { Length: > 0 } ? Encoding.Latin1.GetString(data).TrimEnd('\0') : null;

    protected string Decode(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) return Encoding.Unicode.GetString(data, 2, data.Length - 2).TrimEnd('\0');
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2).TrimEnd('\0');
        return Encoding.UTF8.GetString(data).TrimStart('﻿').TrimEnd('\0');
    }
}
