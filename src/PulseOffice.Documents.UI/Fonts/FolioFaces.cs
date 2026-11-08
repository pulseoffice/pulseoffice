using System.Collections.Concurrent;
using Avalonia.Media;
using PulseOffice.Documents.Layout.Measurement.OpenType;
using PulseOffice.Typography.OpenType.Metrics;
using PulseOffice.UI.Controls.Fonts;

namespace PulseOffice.Documents.UI.Fonts;

public sealed record ResolvedFace(FontMetricsFace Metrics, GlyphTypeface Typeface);

public interface IFolioFaces
{
    ResolvedFace? Resolve(string family, bool bold, bool italic);
}

public sealed class PlatformFaces(IRuntimeFonts fonts, IFontMetricsReader reader, IFontSubstitutes substitutes) : IFolioFaces
{
    private readonly ConcurrentDictionary<(string Family, bool Bold, bool Italic), ResolvedFace?> _faces = new();
    private readonly ConcurrentDictionary<GlyphTypeface, IReadOnlyList<FontMetricsFace>> _files = new(ReferenceEqualityComparer.Instance);

    public ResolvedFace? Resolve(string family, bool bold, bool italic) =>
        _faces.GetOrAdd((family.ToLowerInvariant(), bold, italic), key => Choose(family, key.Bold, key.Italic));

    private ResolvedFace? Choose(string family, bool bold, bool italic)
    {
        if (Load(family, bold, italic, exact: true) is { } found) return found;
        foreach (var substitute in substitutes.For(family))
        {
            if (Load(substitute, bold, italic, exact: true) is { } replaced) return replaced;
        }
        return Load(family, bold, italic, exact: false);
    }

    private ResolvedFace? Load(string family, bool bold, bool italic, bool exact)
    {
        try
        {
            var typeface = new Typeface(fonts.Resolve(family), italic ? FontStyle.Italic : FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal);
            if (!FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphs)) return null;
            if (exact && !Named(glyphs, family)) return null;
            var faces = _files.GetOrAdd(glyphs, Read);
            if (faces.Count == 0) return null;
            var wantBold = glyphs.Weight >= FontWeight.SemiBold;
            var wantItalic = glyphs.Style != FontStyle.Normal;
            var named = faces.Where(face => face.Family.Equals(glyphs.FamilyName, StringComparison.OrdinalIgnoreCase)).ToList();
            var pool = named.Count > 0 ? named : [.. faces];
            var chosen = pool.FirstOrDefault(face => face.Bold == wantBold && face.Italic == wantItalic)
                ?? pool.FirstOrDefault(face => face.Bold == wantBold)
                ?? pool[0];
            return new ResolvedFace(chosen, glyphs);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or InvalidDataException or IOException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    private bool Named(GlyphTypeface glyphs, string family) =>
        glyphs.FamilyName.Equals(family, StringComparison.OrdinalIgnoreCase)
        || _files.GetOrAdd(glyphs, Read).Any(face => face.Family.Equals(family, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<FontMetricsFace> Read(GlyphTypeface glyphs)
    {
        if (!glyphs.PlatformTypeface.TryGetStream(out var stream)) return [];
        using (stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return reader.Read(buffer.ToArray());
        }
    }
}

public sealed class FolioFontLocator(IFolioFaces faces) : IFontFileLocator
{
    public FontMetricsFace? Find(string family, bool bold, bool italic) => faces.Resolve(family, bold, italic)?.Metrics;
}
