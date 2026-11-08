using PulseOffice.Scripting;
using Xunit;

namespace PulseOffice.Scripting.Tests;

public class SourcePositionsTests
{
    private readonly SourcePositions _positions = new();

    [Fact]
    public void UnaColonnaOltreLaFineDellaRigaRestaSuQuellaRiga()
    {
        Assert.Equal(8, _positions.Offset("sheet.Wr\nfine", new ScriptPosition(1, 40)));
    }

    [Fact]
    public void UnaPosizioneNormaleCadeSulCarattereGiusto()
    {
        Assert.Equal(11, _positions.Offset("abc\nsheet.Wr", new ScriptPosition(2, 8)));
    }
}
