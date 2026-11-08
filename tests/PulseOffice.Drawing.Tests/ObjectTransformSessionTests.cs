using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public class ObjectTransformSessionTests
{
    private static readonly IObjectManipulator Manipulator = new ObjectManipulator();
    private static readonly ObjectBox Box = new(100, 100, 200, 100);

    [Fact]
    public void IlCorpoSpostaSenzaRuotare()
    {
        var session = Manipulator.Begin(ObjectHandle.Body, Box, 30);

        session.Track(15, -5, 0, 0, constrain: false);

        Assert.Equal(Box with { X = 115, Y = 95 }, session.Preview);
        Assert.True(session.Moved(0.5));
        Assert.False(session.Rotated(0.01));
        Assert.Equal(30, session.Rotation);
    }

    [Fact]
    public void LaManigliaDiRotazioneRuotaSenzaSpostare()
    {
        var session = Manipulator.Begin(ObjectHandle.Rotate, Box, 0);

        session.Track(0, 0, Box.Right + 50, Box.CentreY, constrain: true);

        Assert.Equal(Box, session.Preview);
        Assert.False(session.Moved(0.001));
        Assert.True(session.Rotated(0.01));
        Assert.Equal(90, session.Rotation, 3);
    }

    [Fact]
    public void LAggancioSostituisceLAnteprimaENonIlPuntoDiPartenza()
    {
        var session = Manipulator.Begin(ObjectHandle.Right, Box, 0);

        session.Track(20, 0, 0, 0, constrain: false);
        session.Settle(session.Preview with { Width = 230 });

        Assert.Equal(230, session.Preview.Width);
        Assert.Equal(Box, session.Start);
    }

    [Fact]
    public void UnMovimentoSottoLaSogliaNonContaComeSpostamento()
    {
        var session = Manipulator.Begin(ObjectHandle.Body, Box, 0);

        session.Track(0.2, 0.2, 0, 0, constrain: false);

        Assert.False(session.Moved(0.5));
        Assert.True(session.Moved(0.001));
    }
}
