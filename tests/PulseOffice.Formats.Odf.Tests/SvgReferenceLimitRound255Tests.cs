using System.Text;
using PulseOffice.Formats;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class SvgReferenceLimitRound255Tests
{
    private readonly SvgSanitizer _sanitizer = new();

    private int Uses(string svg) =>
        _sanitizer.Sanitize(Encoding.UTF8.GetBytes(svg))!.Descendants().Count(element => element.Name.LocalName == "use");

    [Fact]
    public void Exponential_use_chains_are_dropped()
    {
        var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:x=\"http://www.w3.org/1999/xlink\"><g id=\"l0\"><rect width=\"1\" height=\"1\"/></g>");
        for (var level = 1; level <= 8; level++)
        {
            svg.Append($"<g id=\"l{level}\">");
            for (var copy = 0; copy < 10; copy++) svg.Append($"<use x:href=\"#l{level - 1}\"/>");
            svg.Append("</g>");
        }
        svg.Append("</svg>");

        Assert.Equal(0, Uses(svg.ToString()));
    }

    [Fact]
    public void Cycles_are_cut_once_and_plain_uses_kept()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"a\"><use href=\"#b\"/></g><g id=\"b\"><use href=\"#a\"/></g>"
            + "<g id=\"c\"><use href=\"#c\"/></g><rect id=\"r\" width=\"1\" height=\"1\"/><use href=\"#r\"/></svg>";

        Assert.Equal(2, Uses(svg));
    }
}
