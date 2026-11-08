using System.Globalization;
using System.Runtime.CompilerServices;
using PulseOffice.Documents.Layout.Inline;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich.Bidi;

namespace PulseOffice.Documents.Layout.Lines;

public sealed record LineDirections(byte BaseLevel, byte[] Levels, bool[] Starts, int Offset)
{
    public bool RightToLeft => (BaseLevel & 1) == 1;

    public byte LevelAt(int index) => Levels.Length == 0 ? BaseLevel : Levels[Math.Clamp(index - Offset, 0, Levels.Length - 1)];

    public bool StartsAt(int index) => index - Offset is var local && local >= 0 && local < Starts.Length && Starts[local];
}

public interface ILineBidi
{
    LineDirections? Resolve(ShapedParagraph paragraph, LineSpan span);

    IReadOnlyList<GlyphRun> Arrange(IReadOnlyList<GlyphRun> runs, int fixedPrefix, LineDirections directions, double width);
}

public sealed class LineBidi(IBidiAlgorithm algorithm, IBidiReordering reordering, IBidiMirroring mirroring) : ILineBidi
{
    private readonly ConditionalWeakTable<ShapedParagraph, Resolution> _paragraphs = new();

    private sealed record Resolution(BidiParagraph? Paragraph);

    public LineDirections? Resolve(ShapedParagraph paragraph, LineSpan span)
    {
        if (_paragraphs.GetValue(paragraph, Resolved).Paragraph is not { } bidi) return null;
        var levels = reordering.LineLevels(bidi, span.Start, span.End);
        var text = paragraph.Text;
        var starts = new bool[levels.Length];
        for (var i = 0; i < levels.Length; i++)
        {
            var index = span.Start + i;
            if (i == 0 || levels[i] != levels[i - 1]) starts[i] = true;
            else if ((levels[i] & 1) == 1 && !Attached(text, index) && !(Joining(text, index) && Joining(text, index - 1))) starts[i] = true;
        }
        return new LineDirections(bidi.BaseLevel, levels, starts, span.Start);
    }

    public IReadOnlyList<GlyphRun> Arrange(IReadOnlyList<GlyphRun> runs, int fixedPrefix, LineDirections directions, double width)
    {
        var placed = runs.ToArray();
        var segment = new List<int>();
        for (var index = fixedPrefix; index <= placed.Length; index++)
        {
            if (index < placed.Length && placed[index].Kind is GlyphRunKind.Text or GlyphRunKind.Object or GlyphRunKind.Citation)
            {
                segment.Add(index);
                continue;
            }
            Place(placed, segment, directions.RightToLeft);
            segment.Clear();
        }
        for (var index = 0; index < placed.Length; index++)
        {
            var run = placed[index];
            if (run.RightToLeft && run.Kind == GlyphRunKind.Text && Mirrored(run.Text) is { } mirrored) run = run with { Text = mirrored };
            placed[index] = directions.RightToLeft ? run with { X = width - run.X - run.Width } : run;
        }
        return placed;
    }

    private void Place(GlyphRun[] placed, List<int> segment, bool rightToLeft)
    {
        if (segment.Count < 2 && !rightToLeft) return;
        if (segment.Count == 0) return;
        var levels = segment.Select(index => placed[index].Level).ToArray();
        var order = reordering.VisualOrder(levels);
        if (rightToLeft) Array.Reverse(order);
        var x = placed[segment[0]].X;
        foreach (var position in order)
        {
            var index = segment[position];
            placed[index] = placed[index] with { X = x };
            x += placed[index].Width;
        }
    }

    private string? Mirrored(string text)
    {
        if (text.Length != 1) return null;
        var mirror = mirroring.Mirror(text[0]);
        return mirror == text[0] ? null : ((char)mirror).ToString();
    }

    private Resolution Resolved(ShapedParagraph paragraph) => new(Needed(paragraph) ? Paragraph(paragraph) : null);

    private BidiParagraph Paragraph(ShapedParagraph paragraph)
    {
        var embeddings = paragraph.Runs
            .Where(run => run.Style.RightToLeft == true && run.End > run.Start)
            .Select(run => new BidiEmbedding(run.Start, run.End, TextDirection.RightToLeft))
            .ToList();
        return algorithm.Resolve(paragraph.Text, paragraph.Direction ?? TextDirection.LeftToRight, embeddings);
    }

    private bool Needed(ShapedParagraph paragraph)
    {
        if (paragraph.Direction == TextDirection.RightToLeft) return true;
        if (paragraph.Runs.Any(run => run.Style.RightToLeft == true)) return true;
        foreach (var character in paragraph.Text)
        {
            if (character is (>= '֐' and <= 'ࣿ') or (>= 'יִ' and <= 'ﻼ') or '‏' or (>= '‪' and <= '‮') or (>= '⁦' and <= '⁩')) return true;
            if (character is '\uD802' or '\uD803' or '\uD83A' or '\uD83B') return true;
        }
        return false;
    }

    private bool Attached(string text, int index) =>
        index > 0 && index < text.Length && (char.IsLowSurrogate(text[index])
            || CharUnicodeInfo.GetUnicodeCategory(text[index]) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.SpacingCombiningMark
            || text[index] is '‌' or '‍');

    private bool Joining(string text, int index) =>
        index >= 0 && index < text.Length && text[index] is (>= '֐' and <= 'ࣿ') or (>= 'יִ' and <= '﷿') or (>= 'ﹰ' and <= 'ﻼ');
}
