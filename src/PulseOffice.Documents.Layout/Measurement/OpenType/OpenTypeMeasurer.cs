using System.Collections.Concurrent;
using PulseOffice.Typography.OpenType.Metrics;

namespace PulseOffice.Documents.Layout.Measurement.OpenType;

public interface IVerticalMetrics
{
    FontMetrics Of(FontMetricsFace font, double size);
}

public sealed class HorizontalHeaderMetrics : IVerticalMetrics
{
    public FontMetrics Of(FontMetricsFace font, double size)
    {
        var scale = size / font.UnitsPerEm;
        return new FontMetrics(font.HheaAscent * scale, font.HheaDescent * scale, font.HheaLineGap * scale);
    }
}

public interface IAdvanceGrid
{
    double Snap(double advance);
}

public sealed class ExactAdvances : IAdvanceGrid
{
    public double Snap(double advance) => advance;
}

public sealed class OpenTypeMeasurer(IFontFileLocator locator, IVerticalMetrics vertical, IAdvanceGrid grid, IKerningApplier kerning, ITextMeasurer fallback) : ITextMeasurer
{
    public OpenTypeMeasurer(IFontFileLocator locator, IVerticalMetrics vertical, IAdvanceGrid grid, ITextMeasurer fallback)
        : this(locator, vertical, grid, new PairKerningApplier(), fallback)
    {
    }

    public FontMetrics Metrics(FontKey font)
    {
        var file = locator.Find(font.Family, font.Bold, font.Italic);
        return file is null ? fallback.Metrics(font) : vertical.Of(file, font.Size);
    }

    public IReadOnlyList<double> Advances(FontKey font, string text)
    {
        var file = locator.Find(font.Family, font.Bold, font.Italic);
        if (file is null) return fallback.Advances(font, text);
        var scale = font.Size / file.UnitsPerEm;
        var glyphs = new int[text.Length];
        var units = new int[text.Length];
        var visible = new bool[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character == '\u00AD' || char.IsLowSurrogate(character)) continue;
            var codePoint = char.IsHighSurrogate(character) && i + 1 < text.Length ? char.ConvertToUtf32(character, text[i + 1]) : character;
            glyphs[i] = file.GlyphOf(codePoint == '\t' ? ' ' : codePoint);
            units[i] = file.AdvanceOfGlyph(glyphs[i]);
            visible[i] = character != '\t';
        }
        if (font.Kerning) kerning.Apply(file, text, glyphs, units, visible);
        var advances = new double[text.Length];
        for (var i = 0; i < text.Length; i++) advances[i] = units[i] == 0 && !visible[i] ? 0 : grid.Snap(units[i] * scale);
        return advances;
    }
}

public interface IKerningApplier
{
    void Apply(FontMetricsFace font, string text, int[] glyphs, int[] units, bool[] visible);
}

public sealed class NoKerningApplier : IKerningApplier
{
    public void Apply(FontMetricsFace font, string text, int[] glyphs, int[] units, bool[] visible)
    {
    }
}

public sealed class PairKerningApplier : IKerningApplier
{
    public void Apply(FontMetricsFace font, string text, int[] glyphs, int[] units, bool[] visible)
    {
        var previous = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (!visible[i])
            {
                if (text[i] == '\t') previous = -1;
                continue;
            }
            if (previous >= 0) units[previous] += font.Kerning(glyphs[previous], glyphs[i]);
            previous = i;
        }
    }
}
