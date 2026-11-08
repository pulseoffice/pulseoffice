using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public class ObjectSnappingSessionTests
{
    private readonly IObjectManipulator _manipulator = new ObjectManipulator();
    private readonly IObjectSnapping _snapping = new ObjectSnapping();
    private readonly ObjectBox _box = new(100, 100, 40, 20);

    [Fact]
    public void LoSpostamentoSiAgganciaAllaLineaPiuVicina()
    {
        var session = _manipulator.Begin(ObjectHandle.Body, _box, 0);
        session.Track(3, 0, 0, 0, constrain: false);

        var snapped = _snapping.Snap(session, new SnapLines([100], []), 8);

        Assert.Equal(100, snapped.X);
        Assert.Equal(100, snapped.Y);
    }

    [Fact]
    public void IlRidimensionamentoAgganciaSoloIlLatoTrascinato()
    {
        var session = _manipulator.Begin(ObjectHandle.Right, _box, 0);
        session.Track(17, 0, 0, 0, constrain: false);

        var snapped = _snapping.Snap(session, new SnapLines([100, 160], []), 8);

        Assert.Equal(100, snapped.X);
        Assert.Equal(160, snapped.Right);
    }

    [Fact]
    public void LaRotazioneNonSiAggancia()
    {
        var session = _manipulator.Begin(ObjectHandle.Rotate, _box, 0);
        session.Track(0, 0, 200, 200, constrain: false);

        Assert.Equal(session.Preview, _snapping.Snap(session, new SnapLines([0, 50], [0, 50]), 8));
    }

    [Fact]
    public void IlReticoloCopreIlRiquadroAPassoCostante()
    {
        var lattice = _snapping.Lattice(10, 0, 40, 20, 10);

        Assert.Equal([10.0, 20, 30, 40], lattice.Verticals);
        Assert.Equal([0.0, 10, 20], lattice.Horizontals);
    }
}
