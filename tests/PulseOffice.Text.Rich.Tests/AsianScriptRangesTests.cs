using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Text.Rich.Tests;

public sealed class AsianScriptRangesTests
{
    private readonly AsianScriptRanges _ranges = new();

    [Fact]
    public void IlTestoMistoSiDivideFraLatinoECjk()
    {
        var spans = _ranges.Split("ab 漢字かな 한글 cd", 0, 13);

        Assert.Equal([new ScriptSpan(0, 3, false), new ScriptSpan(3, 8, true), new ScriptSpan(11, 2, false)], spans);
    }

    [Fact]
    public void IlTestoSoloLatinoRestaUnSoloPezzoNelLimiteDato()
    {
        Assert.Equal([new ScriptSpan(2, 3, false)], _ranges.Split("abcdefg", 2, 3));
    }
}
