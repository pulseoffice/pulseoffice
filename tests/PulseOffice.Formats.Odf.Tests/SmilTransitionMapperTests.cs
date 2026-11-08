using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class SmilTransitionMapperTests
{
    private readonly SmilTransitionMapper _mapper = new();

    [Theory]
    [InlineData(TransitionDirection.Right, "leftToRight", false)]
    [InlineData(TransitionDirection.Left, "leftToRight", true)]
    [InlineData(TransitionDirection.Down, "topToBottom", false)]
    [InlineData(TransitionDirection.Up, "topToBottom", true)]
    public void LaTendinaUsaISottotipiSmilCheUnAltraSuiteConosce(TransitionDirection direction, string subtype, bool reverse)
    {
        var smil = _mapper.ToSmil(new SlideTransition { Kind = TransitionKind.Wipe, Direction = direction });

        Assert.Equal(new SmilTransition("barWipe", subtype, reverse), smil);
        var back = _mapper.FromSmil(smil.Type, smil.Subtype, smil.Reverse);
        Assert.Equal((TransitionKind.Wipe, direction), (back.Kind, back.Direction));
        Assert.Null(back.Native);
    }

    [Fact]
    public void LaDirezioneInversaDistingueScopriDaCopri()
    {
        Assert.Equal(TransitionKind.Uncover, _mapper.FromSmil("slideWipe", "fromRight", true).Kind);
        Assert.Equal(TransitionKind.Cover, _mapper.FromSmil("slideWipe", "fromRight", false).Kind);
    }

    [Fact]
    public void UnaTransizioneSenzaTipoNostroPortaIlSuoIdentificativoNativo()
    {
        var read = _mapper.FromSmil("irisWipe", "rectangle", true);

        Assert.Equal(new SmilTransition("irisWipe", "rectangle", true), _mapper.ToSmil(read));
        Assert.Equal(new SmilTransition("pushWipe", "fromRight", false), _mapper.ToSmil(read with { Kind = TransitionKind.Push, Direction = TransitionDirection.Left }));
    }
}
