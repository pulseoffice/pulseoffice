using System.Collections.Concurrent;
using Avalonia.Media;

namespace PulseOffice.UI.Controls.Fonts;

public interface IDisplayFonts
{
    FontFamily Resolve(string? family);

    string? Effective(string? family);
}

public interface IFontAvailability
{
    bool Has(string family);
}

public sealed class AvaloniaFontAvailability(IRuntimeFonts runtime) : IFontAvailability
{
    public bool Has(string family)
    {
        if (runtime.Contains(family)) return true;
        try
        {
            return FontManager.Current.TryGetGlyphTypeface(new Typeface(runtime.Resolve(family)), out var glyphs)
                && glyphs.FamilyName.Equals(family, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }
}

public sealed class DisplayFonts(IRuntimeFonts runtime, IFontSubstitutes substitutes, IFontAvailability availability) : IDisplayFonts
{
    private readonly ConcurrentDictionary<string, string> _chosen = new(StringComparer.OrdinalIgnoreCase);

    public FontFamily Resolve(string? family) =>
        Effective(family) is { } chosen ? runtime.Resolve(chosen) : FontFamily.Default;

    public string? Effective(string? family)
    {
        if (string.IsNullOrWhiteSpace(family)) return null;
        return _chosen.GetOrAdd(family, Choose);
    }

    private string Choose(string family)
    {
        if (availability.Has(family)) return family;
        return substitutes.For(family).FirstOrDefault(availability.Has) ?? family;
    }
}
