using PulseOffice.Formats;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class LinkedFileSchemeTests
{
    private readonly LinkedFiles _links = new();

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("vbscript:msgbox")]
    [InlineData("ms-msdt:/id")]
    public void Script_and_inline_schemes_never_become_linked_paths(string href) =>
        Assert.Null(_links.ToPath(href));

    [Theory]
    [InlineData("https://example.com/a.png", "https://example.com/a.png")]
    [InlineData("pictures/a.png", "pictures/a.png")]
    [InlineData("/tmp/a.png", "/tmp/a.png")]
    public void File_and_web_links_keep_their_path(string href, string expected) =>
        Assert.Equal(expected, _links.ToPath(href));
}
