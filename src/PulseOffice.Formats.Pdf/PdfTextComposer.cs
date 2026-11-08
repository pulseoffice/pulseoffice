using System.Globalization;
using System.Text;
using PulseOffice.Formats.Pdf.Fonts;

namespace PulseOffice.Formats.Pdf;

public sealed class PdfTextComposer
{
    private readonly List<Piece> _pieces = [];
    private double _adjustment;

    public bool IsEmpty => _pieces.Count == 0;

    public IPdfFontResource? FirstFont => _pieces.Count == 0 ? null : _pieces[0].Font;

    public void Adjust(double thousandths)
    {
        if (double.IsFinite(thousandths)) _adjustment += thousandths;
    }

    public void Add(PdfShownText shown)
    {
        ArgumentNullException.ThrowIfNull(shown);
        if (shown.Glyphs.Count == 0) return;

        var marked = shown.ActualText is { Length: > 0 };
        var first = true;
        var index = 0;
        while (index < shown.Glyphs.Count)
        {
            var rise = shown.Glyphs[index].Rise;
            var end = index;
            while (end < shown.Glyphs.Count && Math.Abs(shown.Glyphs[end].Rise - rise) < 0.01) end++;

            var piece = Target(shown.Font, rise, marked, first && marked ? shown.ActualText : null);
            if (Math.Abs(_adjustment) >= 0.01)
            {
                piece.Flush();
                piece.Array.Append(Math.Round(_adjustment, 3).ToString("0.###", CultureInfo.InvariantCulture)).Append(' ');
            }
            _adjustment = 0;
            for (var glyph = index; glyph < end; glyph++) piece.Pending.Add(shown.Glyphs[glyph]);

            first = false;
            index = end;
        }

        if (marked) _pieces[^1].Closes = true;
    }

    public string Compose(double size, Func<IPdfFontResource, string> select)
    {
        ArgumentNullException.ThrowIfNull(select);
        var content = new StringBuilder();
        IPdfFontResource? font = null;
        var rise = 0.0;
        foreach (var piece in _pieces)
        {
            if (piece.ActualText is { } actual) content.Append("/Span << /ActualText ").Append(Hex(actual)).Append(" >> BDC ");
            if (!ReferenceEquals(font, piece.Font))
            {
                content.Append(select(piece.Font));
                font = piece.Font;
            }
            if (Math.Abs(piece.Rise - rise) >= 0.01)
            {
                content.Append(Number(piece.Rise * size / 1000)).Append(" Ts ");
                rise = piece.Rise;
            }
            piece.Flush();
            content.Append('[').Append(piece.Array.ToString().TrimEnd()).Append("] TJ ");
            if (piece.Closes) content.Append("EMC ");
        }
        if (Math.Abs(rise) >= 0.01) content.Append("0 Ts ");
        return content.ToString();
    }

    private Piece Target(IPdfFontResource font, double rise, bool marked, string? opens)
    {
        if (opens is null && _pieces.Count > 0)
        {
            var last = _pieces[^1];
            if (ReferenceEquals(last.Font, font) && Math.Abs(last.Rise - rise) < 0.01 && last.Marked == marked && !last.Closes) return last;
        }

        var piece = new Piece(font, rise, marked, opens);
        _pieces.Add(piece);
        return piece;
    }

    private string Hex(string text)
    {
        var hex = new StringBuilder("<FEFF");
        foreach (var unit in text) hex.Append(((int)unit).ToString("X4", CultureInfo.InvariantCulture));
        return hex.Append('>').ToString();
    }

    private string Number(double value) =>
        (double.IsFinite(value) ? Math.Round(value, 4) + 0.0 : 0).ToString("0.####", CultureInfo.InvariantCulture);

    private sealed class Piece(IPdfFontResource font, double rise, bool marked, string? actualText)
    {
        public IPdfFontResource Font { get; } = font;

        public double Rise { get; } = rise;

        public bool Marked { get; } = marked;

        public string? ActualText { get; } = actualText;

        public bool Closes { get; set; }

        public StringBuilder Array { get; } = new();

        public List<PdfGlyph> Pending { get; } = [];

        public void Flush()
        {
            if (Pending.Count == 0) return;
            Array.Append(Font.Show(Pending)).Append(' ');
            Pending.Clear();
        }
    }
}
