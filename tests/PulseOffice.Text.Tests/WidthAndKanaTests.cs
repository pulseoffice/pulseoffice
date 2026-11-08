using Xunit;

namespace PulseOffice.Text.Tests;

public sealed class WidthAndKanaTests
{
    private readonly TextCases _cases = new();

    [Fact]
    public void LarghezzaEKanaSiConvertonoSenzaCambiareLaLunghezza()
    {
        Assert.Equal("ＡＢｃ１　！", _cases[TextCaseKind.FullWidth].Apply("ABc1 !"));
        Assert.Equal("ABc1 !", _cases[TextCaseKind.HalfWidth].Apply("ＡＢｃ１　！"));
        Assert.Equal("カタカナ", _cases[TextCaseKind.Katakana].Apply("かたかな"));
        Assert.Equal("ひらがな", _cases[TextCaseKind.Hiragana].Apply("ヒラガナ"));
    }
}
