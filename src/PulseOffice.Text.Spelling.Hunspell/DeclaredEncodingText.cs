using System.Text;

namespace PulseOffice.Text.Spelling.Hunspell;

public interface IDeclaredEncodingText
{
    IReadOnlyList<string> ReadLines(string path);
}

public sealed class DeclaredEncodingText : IDeclaredEncodingText
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ISO8859-1"] = "iso-8859-1",
        ["ISO8859-2"] = "iso-8859-2",
        ["ISO8859-5"] = "iso-8859-5",
        ["ISO8859-7"] = "iso-8859-7",
        ["ISO8859-15"] = "iso-8859-15",
        ["KOI8-R"] = "koi8-r",
        ["KOI8-U"] = "koi8-u",
        ["microsoft-cp1251"] = "windows-1251",
    };

    public DeclaredEncodingText() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public IReadOnlyList<string> ReadLines(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var bytes = File.ReadAllBytes(path);
        var encoding = Declared(bytes) ?? new UTF8Encoding(false);

        using var reader = new StreamReader(new MemoryStream(bytes), encoding, detectEncodingFromByteOrderMarks: true);

        var lines = new List<string>();
        while (reader.ReadLine() is { } line) lines.Add(line);

        return lines;
    }

    private static Encoding? Declared(byte[] bytes)
    {
        var end = Array.IndexOf(bytes, (byte)'\n');
        var head = Encoding.ASCII.GetString(bytes, 0, end < 0 ? Math.Min(bytes.Length, 64) : Math.Min(end, 64))
            .Trim('\r', ' ', '\t', '﻿');

        if (head.Length == 0 || head.Any(char.IsWhiteSpace)) return null;

        var name = Aliases.TryGetValue(head, out var alias) ? alias : head;

        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
