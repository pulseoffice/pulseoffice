using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed partial class PdfProbe
{
    private readonly Dictionary<int, string> _dictionaries = [];
    private readonly Dictionary<int, byte[]> _streams = [];

    public PdfProbe(byte[] file)
    {
        var text = Encoding.Latin1.GetString(file);
        foreach (Match match in ObjectPattern().Matches(text))
        {
            var id = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var body = match.Groups[2].Value;
            var marker = body.IndexOf("stream\n", StringComparison.Ordinal);
            if (marker < 0)
            {
                _dictionaries[id] = body;
                continue;
            }
            var dictionary = body[..marker];
            _dictionaries[id] = dictionary;
            var start = match.Groups[2].Index + marker + 7;
            var length = int.Parse(LengthPattern().Match(dictionary).Groups[1].Value, CultureInfo.InvariantCulture);
            var raw = file.AsSpan(start, length).ToArray();
            _streams[id] = dictionary.Contains("/FlateDecode", StringComparison.Ordinal) ? Inflate(raw) : raw;
        }
    }

    public IReadOnlyDictionary<int, string> Objects => _dictionaries;

    public byte[]? Stream(int id) => _streams.GetValueOrDefault(id);

    public IEnumerable<string> EmbeddedFontNames =>
        _dictionaries.Values.Where(body => body.Contains("/Type /FontDescriptor", StringComparison.Ordinal)
            && (body.Contains("/FontFile2", StringComparison.Ordinal) || body.Contains("/FontFile3", StringComparison.Ordinal)))
            .Select(body => FontNamePattern().Match(body).Groups[1].Value);

    public bool UsesStandardFonts => _dictionaries.Values.Any(body => body.Contains("/Subtype /Type1", StringComparison.Ordinal));

    public string ExtractText()
    {
        var builder = new StringBuilder();
        foreach (var (id, body) in _dictionaries.Where(pair => pair.Value.Contains("/Type /Page ", StringComparison.Ordinal)).OrderBy(pair => pair.Key))
        {
            var fonts = PageFonts(body);
            var content = int.Parse(ContentsPattern().Match(body).Groups[1].Value, CultureInfo.InvariantCulture);
            builder.Append(Decode(Encoding.Latin1.GetString(_streams[content]), fonts));
        }
        return builder.ToString();
    }

    private Dictionary<string, Dictionary<int, string>?> PageFonts(string page)
    {
        var fonts = new Dictionary<string, Dictionary<int, string>?>(StringComparer.Ordinal);
        var section = FontSectionPattern().Match(page).Groups[1].Value;
        foreach (Match entry in FontEntryPattern().Matches(section))
        {
            var font = _dictionaries[int.Parse(entry.Groups[2].Value, CultureInfo.InvariantCulture)];
            var unicode = ToUnicodePattern().Match(font);
            fonts[entry.Groups[1].Value] = unicode.Success ? Map(int.Parse(unicode.Groups[1].Value, CultureInfo.InvariantCulture)) : null;
        }
        return fonts;
    }

    private Dictionary<int, string> Map(int stream)
    {
        var map = new Dictionary<int, string>();
        foreach (Match pair in BfCharPattern().Matches(Encoding.ASCII.GetString(_streams[stream])))
        {
            var code = int.Parse(pair.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var hex = pair.Groups[2].Value;
            var units = Enumerable.Range(0, hex.Length / 4).Select(i => (char)int.Parse(hex.AsSpan(i * 4, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            map[code] = new string([.. units]);
        }
        return map;
    }

    private string Decode(string content, Dictionary<string, Dictionary<int, string>?> fonts)
    {
        var builder = new StringBuilder();
        Dictionary<int, string>? current = null;
        var replaced = false;
        foreach (Match token in TokenPattern().Matches(content))
        {
            if (token.Groups["actual"].Success)
            {
                var hex = token.Groups["actual"].Value;
                for (var i = 4; i + 4 <= hex.Length; i += 4) builder.Append((char)int.Parse(hex.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                replaced = true;
            }
            else if (token.Groups["emc"].Success) replaced = false;
            else if (replaced && !token.Groups["end"].Success) continue;
            else if (token.Groups["font"].Success) current = fonts.GetValueOrDefault(token.Groups["font"].Value);
            else if (token.Groups["hex"].Success && current is not null)
            {
                var hex = token.Groups["hex"].Value;
                for (var i = 0; i + 4 <= hex.Length; i += 4)
                    builder.Append(current.GetValueOrDefault(int.Parse(hex.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture), "�"));
            }
            else if (token.Groups["literal"].Success) builder.Append(token.Groups["literal"].Value);
            else if (token.Groups["end"].Success) builder.Append('\n');
        }
        return builder.ToString();
    }

    private byte[] Inflate(byte[] raw)
    {
        using var input = new MemoryStream(raw);
        using var inflater = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflater.CopyTo(output);
        return output.ToArray();
    }

    [GeneratedRegex(@"(\d+) 0 obj\n(.*?)\nendobj", RegexOptions.Singleline)]
    private static partial Regex ObjectPattern();

    [GeneratedRegex(@"/Length (\d+)")]
    private static partial Regex LengthPattern();

    [GeneratedRegex(@"/FontName /(\S+)")]
    private static partial Regex FontNamePattern();

    [GeneratedRegex(@"/Contents (\d+) 0 R")]
    private static partial Regex ContentsPattern();

    [GeneratedRegex(@"/Font << (.*?)>>")]
    private static partial Regex FontSectionPattern();

    [GeneratedRegex(@"/(F\d+) (\d+) 0 R")]
    private static partial Regex FontEntryPattern();

    [GeneratedRegex(@"/ToUnicode (\d+) 0 R")]
    private static partial Regex ToUnicodePattern();

    [GeneratedRegex(@"<([0-9A-F]{4})> <([0-9A-F]+)>")]
    private static partial Regex BfCharPattern();

    [GeneratedRegex(@"/ActualText <(?<actual>[0-9A-F]*)>|(?<emc>EMC)|/(?<font>F\d+) [\d.]+ Tf|<(?<hex>[0-9A-F]*)>|\((?<literal>(?:\\.|[^\\)])*)\)|(?<end>ET)")]
    private static partial Regex TokenPattern();
}
