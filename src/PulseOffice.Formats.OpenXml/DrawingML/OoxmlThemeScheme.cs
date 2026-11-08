using PulseOffice.Presentations;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlThemeScheme(OoxmlValues values)
{
    public ColorScheme Colours(A.ColorScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        return new ColorScheme
        {
            Text = Get(scheme.Dark1Color, 0xFF000000),
            Background = Get(scheme.Light1Color, 0xFFFFFFFF),
            Text2 = Get(scheme.Dark2Color, 0xFF1F497D),
            Background2 = Get(scheme.Light2Color, 0xFFEEECE1),
            Accent1 = Get(scheme.Accent1Color, 0xFF4F81BD),
            Accent2 = Get(scheme.Accent2Color, 0xFFC0504D),
            Accent3 = Get(scheme.Accent3Color, 0xFF9BBB59),
            Accent4 = Get(scheme.Accent4Color, 0xFF8064A2),
            Accent5 = Get(scheme.Accent5Color, 0xFF4BACC6),
            Accent6 = Get(scheme.Accent6Color, 0xFFF79646),
            Hyperlink = Get(scheme.Hyperlink, 0xFF0000FF),
            FollowedHyperlink = Get(scheme.FollowedHyperlinkColor, 0xFF800080),
        };
    }

    private uint Get(A.Color2Type? colour, uint fallback) =>
        colour?.RgbColorModelHex?.Val?.Value is { } hex ? values.Color(hex)
        : colour?.SystemColor?.LastColor?.Value is { } last ? values.Color(last)
        : fallback;
}
