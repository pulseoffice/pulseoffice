using System.Text;
using PulseOffice.Platform.Windows;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class WindowsClipboardFormatsTests
{
    private readonly WindowsClipboardFormats _formats = new();

    [Fact]
    public void UnInizioDiFrammentoSenzaFineNonFaCadereLaCopia()
    {
        var html = "<p>prova <!--StartFragment--> senza fine</p>";

        var encoded = _formats.EncodeHtml(html);

        Assert.Contains("senza fine", Encoding.UTF8.GetString(encoded), StringComparison.Ordinal);
    }

    [Fact]
    public void IlFrammentoTornaUgualeDopoIlGiro()
    {
        var html = "<p>àèì 😀</p>";

        var decoded = _formats.DecodeHtml(_formats.EncodeHtml(html));

        Assert.Contains(html, decoded, StringComparison.Ordinal);
    }
}
