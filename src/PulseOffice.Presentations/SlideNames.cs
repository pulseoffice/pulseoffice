using System.Text.RegularExpressions;

namespace PulseOffice.Presentations;

public static partial class SlideNames
{
    public static string PageName(Slide slide) => slide.Name ?? slide.Id;

    public static bool IsAutomatic(string pageName) => AutomaticName().IsMatch(pageName);

    public static string? NameFromPage(string pageName) => IsAutomatic(pageName) ? null : pageName;

    public static bool IsAvailable(Presentation deck, Slide slide, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) return false;
        if (IsAutomatic(trimmed) && !string.Equals(trimmed, slide.Id, StringComparison.Ordinal)) return false;

        return deck.Slides.Where(other => !ReferenceEquals(other, slide))
            .All(other => !string.Equals(PageName(other), trimmed, StringComparison.Ordinal));
    }

    [GeneratedRegex(@"^(slide|page)\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex AutomaticName();
}
