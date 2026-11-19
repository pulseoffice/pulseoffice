using System.Globalization;
using System.Text;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Printing;
using PulseOffice.Text.Rich.Bidi;
using PulseOffice.Typography.Shaping;

namespace PulseOffice.Formats.Pdf;

public interface IPdfBidiText
{
    IReadOnlyList<PdfShownText> Show(string text, PrintFont font, IPdfFontSet fonts);
}

public sealed class PdfBidiText(IBidiAlgorithm algorithm, IBidiReordering reordering, IBidiMirroring mirroring) : IPdfBidiText
{
    public PdfBidiText() : this(new BidiAlgorithm(), new BidiReordering(), new BidiMirroring())
    {
    }

    public IReadOnlyList<PdfShownText> Show(string text, PrintFont font, IPdfFontSet fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        if (string.IsNullOrEmpty(text) || !text.Any(Strong)) return fonts.Show(text, font);
        var paragraph = algorithm.Resolve(text, null);
        if (paragraph.IsUniform) return fonts.Show(text, font);
        var levels = reordering.LineLevels(paragraph, 0, text.Length);
        var runs = Runs(levels);
        var order = reordering.VisualOrder([.. runs.Select(run => run.Level)]);
        var shown = new List<PdfShownText>();
        foreach (var index in order)
        {
            var (start, end, level) = runs[index];
            if ((level & 1) == 0) shown.AddRange(fonts.Show(text[start..end], font));
            else shown.AddRange(RightToLeft(text, start, end, font, fonts));
        }
        return shown;
    }

    private List<PdfShownText> RightToLeft(string text, int start, int end, PrintFont font, IPdfFontSet fonts)
    {
        var pieces = new List<IReadOnlyList<PdfShownText>>();
        var complex = fonts.ComplexSpans(text).Where(span => span.End > start && span.Start < end).ToList();
        var position = start;
        foreach (var span in complex)
        {
            var from = Math.Max(span.Start, start);
            var to = Math.Min(span.End, end);
            Graphemes(text, position, from, font, fonts, pieces);
            pieces.Add(fonts.Show(text, new ShapingSpan(from, to - from), font));
            position = to;
        }
        Graphemes(text, position, end, font, fonts, pieces);
        pieces.Reverse();
        return [.. pieces.SelectMany(piece => piece)];
    }

    private void Graphemes(string text, int start, int end, PrintFont font, IPdfFontSet fonts, List<IReadOnlyList<PdfShownText>> pieces)
    {
        var index = start;
        while (index < end)
        {
            var length = Math.Clamp(StringInfo.GetNextTextElementLength(text, index), 1, end - index);
            pieces.Add(fonts.Show(Mirrored(text.Substring(index, length)), font));
            index += length;
        }
    }

    private string Mirrored(string grapheme)
    {
        if (!Rune.TryGetRuneAt(grapheme, 0, out var rune)) return grapheme;
        var mirror = mirroring.Mirror(rune.Value);
        return mirror == rune.Value || mirror <= 0 ? grapheme : char.ConvertFromUtf32(mirror) + grapheme[rune.Utf16SequenceLength..];
    }

    private List<(int Start, int End, byte Level)> Runs(byte[] levels)
    {
        var runs = new List<(int Start, int End, byte Level)>();
        var start = 0;
        for (var index = 1; index <= levels.Length; index++)
        {
            if (index < levels.Length && levels[index] == levels[start]) continue;
            runs.Add((start, index, levels[start]));
            start = index;
        }
        return runs;
    }

    private bool Strong(char character) =>
        character is (>= '֐' and <= 'ࣿ') or (>= 'יִ' and <= '﷿') or (>= 'ﹰ' and <= '﻾')
            or '‏' or '‫' or '‮' or '⁧' or '\uD802' or '\uD803' or '\uD83A' or '\uD83B';
}
