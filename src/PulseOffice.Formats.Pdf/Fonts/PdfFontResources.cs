using System.Globalization;
using System.Text;
using PulseOffice.Typography.OpenType.Metrics;
using PulseOffice.Typography.Subsetting;

namespace PulseOffice.Formats.Pdf.Fonts;

public readonly record struct PdfGlyph(int Code, double Width)
{
    public double Advance { get; init; } = Width;

    public double OffsetX { get; init; }

    public double Rise { get; init; }

    public bool Positioned => Math.Abs(Advance - Width) >= 0.01 || Math.Abs(OffsetX) >= 0.01;
}

public interface IPdfFontResource
{
    string Name { get; }

    bool Embedded { get; }

    bool Bold { get; }

    bool Italic { get; }

    bool Covers(int codePoint);

    double Advance(int codePoint);

    FontMetricsFace? Face { get; }

    PdfGlyph Use(int codePoint);

    PdfGlyph UseGlyph(int glyph, string? text);

    string Show(IEnumerable<PdfGlyph> glyphs);

    void Write(IPdfObjectSink file, int id, bool compress);
}

public sealed class StandardFontResource(string name, PdfStandardFont font, IPdfFontMetrics metrics) : IPdfFontResource
{
    public string Name { get; } = name;

    public PdfStandardFont Font { get; } = font;

    public bool Embedded => false;

    public bool Bold => Font is PdfStandardFont.HelveticaBold or PdfStandardFont.HelveticaBoldOblique or PdfStandardFont.TimesBold
        or PdfStandardFont.TimesBoldItalic or PdfStandardFont.CourierBold or PdfStandardFont.CourierBoldOblique;

    public bool Italic => Font is PdfStandardFont.HelveticaOblique or PdfStandardFont.HelveticaBoldOblique or PdfStandardFont.TimesItalic
        or PdfStandardFont.TimesBoldItalic or PdfStandardFont.CourierOblique or PdfStandardFont.CourierBoldOblique;

    public bool Covers(int codePoint) =>
        codePoint is >= 0x20 and < 0xFFFF && (codePoint == '?' || PdfEncoding.ToWinAnsi((char)codePoint) != '?');

    public double Advance(int codePoint) => metrics.WidthOf(Character(codePoint).ToString(), Font, 1000);

    public FontMetricsFace? Face => null;

    public PdfGlyph UseGlyph(int glyph, string? text) =>
        Use(Rune.DecodeFromUtf16(text, out var rune, out _) == System.Buffers.OperationStatus.Done ? rune.Value : '?');

    public PdfGlyph Use(int codePoint)
    {
        var character = Character(codePoint);
        return new PdfGlyph(PdfEncoding.ToWinAnsi(character), metrics.WidthOf(character.ToString(), Font, 1000));
    }

    public string Show(IEnumerable<PdfGlyph> glyphs) =>
        PdfEncoding.Literal(new string([.. glyphs.Select(glyph => (char)glyph.Code)]));

    public void Write(IPdfObjectSink file, int id, bool compress) =>
        file.WriteObject(id, $"<< /Type /Font /Subtype /Type1 /BaseFont /{metrics.BaseFontName(Font)} /Encoding /WinAnsiEncoding >>");

    private char Character(int codePoint) => Covers(codePoint) ? (char)codePoint : '?';
}

public enum EmbeddingMode
{
    TrueTypeSubset,
    TrueTypeWhole,
    OpenTypeWhole,
}

public sealed class EmbeddedFontResource : IPdfFontResource
{
    private readonly FontMetricsFace _face;
    private readonly ITrueTypeSubsetter _subsetter;
    private readonly IToUnicodeWriter _unicode;
    private readonly Dictionary<int, int> _codes = [];
    private readonly List<int> _glyphs = [0];
    private readonly Dictionary<int, string> _text = [];
    private readonly double _scale;

    public EmbeddedFontResource(string name, FontMetricsFace face, EmbeddingMode mode, bool bold, bool italic, ITrueTypeSubsetter subsetter, IToUnicodeWriter unicode)
    {
        Name = name;
        _face = face;
        Mode = mode;
        Bold = bold;
        Italic = italic;
        _subsetter = subsetter;
        _unicode = unicode;
        _scale = 1000.0 / face.UnitsPerEm;
        _codes[0] = 0;
    }

    public string Name { get; }

    public EmbeddingMode Mode { get; }

    public FontMetricsFace Face => _face;

    FontMetricsFace? IPdfFontResource.Face => _face;

    public bool Embedded => true;

    public bool Bold { get; }

    public bool Italic { get; }

    public int GlyphsUsed => _glyphs.Count;

    public bool Covers(int codePoint) => _face.GlyphOf(codePoint) > 0;

    public double Advance(int codePoint) => _face.AdvanceOf(codePoint) * _scale;

    public PdfGlyph Use(int codePoint) => UseGlyph(_face.GlyphOf(codePoint), char.ConvertFromUtf32(codePoint));

    public PdfGlyph UseGlyph(int glyph, string? text)
    {
        glyph = glyph >= 0 && glyph < Math.Max(1, _face.GlyphCount) ? glyph : 0;
        if (!_codes.TryGetValue(glyph, out var code))
        {
            code = Mode == EmbeddingMode.TrueTypeSubset ? _glyphs.Count : glyph;
            _codes[glyph] = code;
            _glyphs.Add(glyph);
        }
        if (glyph > 0 && !string.IsNullOrEmpty(text)) _text.TryAdd(code, text);
        return new PdfGlyph(code, _face.AdvanceOfGlyph(glyph) * _scale);
    }

    public string Show(IEnumerable<PdfGlyph> glyphs)
    {
        var shown = new StringBuilder();
        var open = false;
        foreach (var glyph in glyphs)
        {
            if (Math.Abs(glyph.OffsetX) >= 0.01) Adjust(shown, ref open, -glyph.OffsetX);
            if (!open)
            {
                shown.Append('<');
                open = true;
            }
            shown.Append(glyph.Code.ToString("X4", CultureInfo.InvariantCulture));
            var after = glyph.Width - glyph.Advance + glyph.OffsetX;
            if (Math.Abs(after) >= 0.01) Adjust(shown, ref open, after);
        }
        if (open) shown.Append('>');
        return shown.Length == 0 ? "<>" : shown.ToString().TrimEnd();
    }

    private void Adjust(StringBuilder shown, ref bool open, double thousandths)
    {
        if (open) shown.Append('>');
        open = false;
        shown.Append(' ').Append(Math.Round(thousandths, 3).ToString("0.###", CultureInfo.InvariantCulture)).Append(' ');
    }

    public void Write(IPdfObjectSink file, int id, bool compress)
    {
        var baseFont = BaseFont();
        var program = Mode switch
        {
            EmbeddingMode.TrueTypeSubset => _subsetter.Subset(_face.Tables, _glyphs).Data,
            _ => _face.Tables.Whole(),
        };
        var fontFile = Mode == EmbeddingMode.OpenTypeWhole
            ? file.WriteStream(program, compress, "/Subtype /OpenType")
            : file.WriteStream(program, compress, FormattableString.Invariant($"/Length1 {program.Length}"));
        var (xMin, yMin, xMax, yMax) = _face.Box;
        var flags = 4 | (_face.Face.IsFixedPitch ? 1 : 0) | (_face.Italic ? 64 : 0);
        var descriptor = file.WriteObject(FormattableString.Invariant(
            $"<< /Type /FontDescriptor /FontName /{baseFont} /Flags {flags} /FontBBox [{Units(xMin)} {Units(yMin)} {Units(xMax)} {Units(yMax)}] ") +
            FormattableString.Invariant($"/ItalicAngle {Math.Round(_face.ItalicAngle, 2)} /Ascent {Units(_face.HheaAscent)} /Descent {Units(-_face.HheaDescent)} ") +
            FormattableString.Invariant($"/CapHeight {Units(_face.CapHeight)} /StemV {(_face.Bold ? 120 : 80)} ") +
            (Mode == EmbeddingMode.OpenTypeWhole ? "/FontFile3 " : "/FontFile2 ") + fontFile + " 0 R >>");
        var subtype = Mode == EmbeddingMode.OpenTypeWhole ? "CIDFontType0" : "CIDFontType2";
        var map = Mode == EmbeddingMode.OpenTypeWhole ? string.Empty : " /CIDToGIDMap /Identity";
        var descendant = file.WriteObject(
            $"<< /Type /Font /Subtype /{subtype} /BaseFont /{baseFont} /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> " +
            $"/FontDescriptor {descriptor} 0 R /DW {Units(_face.AdvanceOfGlyph(0))} /W {Widths()}{map} >>");
        var toUnicode = file.WriteStream(Encoding.ASCII.GetBytes(_unicode.Write(_text)), compress, null);
        file.WriteObject(id,
            $"<< /Type /Font /Subtype /Type0 /BaseFont /{baseFont} /Encoding /Identity-H /DescendantFonts [{descendant} 0 R] /ToUnicode {toUnicode} 0 R >>");
    }

    private string Widths()
    {
        var builder = new StringBuilder("[");
        var ordered = _codes.OrderBy(pair => pair.Value).ToList();
        var index = 0;
        while (index < ordered.Count)
        {
            var start = ordered[index].Value;
            builder.Append(start.ToString(CultureInfo.InvariantCulture)).Append(" [");
            var expected = start;
            while (index < ordered.Count && ordered[index].Value == expected)
            {
                builder.Append(Units(_face.AdvanceOfGlyph(ordered[index].Key))).Append(' ');
                expected++;
                index++;
            }
            builder.Length--;
            builder.Append("] ");
        }
        if (builder.Length > 1) builder.Length--;
        return builder.Append(']').ToString();
    }

    private string BaseFont()
    {
        var raw = _face.Face.PostScriptName ?? _face.Family;
        var name = new string([.. raw.Where(character => character is > ' ' and < '\u007F' and not ('(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%' or '#'))]);
        if (name.Length == 0) name = "Font";
        if (Mode != EmbeddingMode.TrueTypeSubset) return name;
        var hash = 17L;
        foreach (var glyph in _glyphs) hash = unchecked(hash * 31 + glyph);
        foreach (var character in name) hash = unchecked(hash * 31 + character);
        var tag = new char[6];
        var value = (ulong)hash;
        for (var i = 0; i < 6; i++)
        {
            tag[i] = (char)('A' + (int)(value % 26));
            value /= 26;
        }
        return new string(tag) + "+" + name;
    }

    private string Units(int value) => Math.Round(value * _scale).ToString(CultureInfo.InvariantCulture);
}

public interface IToUnicodeWriter
{
    string Write(IReadOnlyDictionary<int, string> codes);
}

public sealed class ToUnicodeWriter : IToUnicodeWriter
{
    private const int BlockSize = 100;

    public string Write(IReadOnlyDictionary<int, string> codes)
    {
        ArgumentNullException.ThrowIfNull(codes);
        var builder = new StringBuilder();
        builder.Append("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n");
        builder.Append("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n");
        builder.Append("/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n");
        builder.Append("1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
        var entries = codes.OrderBy(pair => pair.Key).ToList();
        for (var start = 0; start < entries.Count; start += BlockSize)
        {
            var block = entries.Skip(start).Take(BlockSize).ToList();
            builder.Append(CultureInfo.InvariantCulture, $"{block.Count} beginbfchar\n");
            foreach (var (code, text) in block)
            {
                builder.Append('<').Append(code.ToString("X4", CultureInfo.InvariantCulture)).Append("> <");
                foreach (var unit in text) builder.Append(((int)unit).ToString("X4", CultureInfo.InvariantCulture));
                builder.Append(">\n");
            }
            builder.Append("endbfchar\n");
        }
        builder.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
        return builder.ToString();
    }
}
