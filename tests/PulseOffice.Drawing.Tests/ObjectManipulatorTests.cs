using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public class ObjectManipulatorTests
{
    private static readonly IObjectManipulator Manipulator = new ObjectManipulator();
    private static readonly ObjectBox Box = new(100, 100, 200, 100);

    [Fact]
    public void IlCentroColpisceIlCorpo()
    {
        Assert.Equal(ObjectHandle.Body, Manipulator.HandleAt(Box, 0, 200, 150));
    }

    [Fact]
    public void FuoriDalRiquadroNonSiColpisceNulla()
    {
        Assert.Equal(ObjectHandle.None, Manipulator.HandleAt(Box, 0, 500, 500));
    }

    [Fact]
    public void GliAngoliColpisconoLeManiglieCorrispondenti()
    {
        Assert.Equal(ObjectHandle.TopLeft, Manipulator.HandleAt(Box, 0, 100, 100));
        Assert.Equal(ObjectHandle.BottomRight, Manipulator.HandleAt(Box, 0, 300, 200));
    }

    [Fact]
    public void LaManigliaDiRotazioneStaSopraIlBordoSuperiore()
    {
        Assert.Equal(
            ObjectHandle.Rotate,
            Manipulator.HandleAt(Box, 0, Box.CentreX, Box.Y - Manipulator.RotationDistance));
    }

    [Fact]
    public void LeManiglieSeguonoLaRotazioneDellOggetto()
    {
        Assert.Equal(ObjectHandle.None, Manipulator.HandleAt(Box, 90, 100, 100));
        Assert.Equal(ObjectHandle.Body, Manipulator.HandleAt(Box, 90, 200, 150));
    }

    [Fact]
    public void TrascinareIlCorpoSpostaSenzaRidimensionare()
    {
        var moved = Manipulator.Resize(Box, ObjectHandle.Body, 10, -5, keepAspect: false);

        Assert.Equal(new ObjectBox(110, 95, 200, 100), moved);
    }

    [Fact]
    public void IlBordoDestroCambiaSoloLaLarghezza()
    {
        var resized = Manipulator.Resize(Box, ObjectHandle.Right, 50, 999, keepAspect: false);

        Assert.Equal(new ObjectBox(100, 100, 250, 100), resized);
    }

    [Fact]
    public void IlBordoSinistroMuoveLOrigineELaLarghezza()
    {
        var resized = Manipulator.Resize(Box, ObjectHandle.Left, 20, 0, keepAspect: false);

        Assert.Equal(new ObjectBox(120, 100, 180, 100), resized);
    }

    [Fact]
    public void UnOggettoNonScendeSottoIlLatoMinimo()
    {
        var resized = Manipulator.Resize(Box, ObjectHandle.Right, -1000, 0, keepAspect: false);

        Assert.True(resized.Width >= 8);
        Assert.Equal(100, resized.X);
    }

    [Fact]
    public void LeProporzioniSiConservanoSugliAngoli()
    {
        var resized = Manipulator.Resize(Box, ObjectHandle.BottomRight, 100, 0, keepAspect: true);

        Assert.Equal(Box.Width / Box.Height, resized.Width / resized.Height, 6);
    }

    [Fact]
    public void LaRotazioneSiMisuraDalCentro()
    {
        Assert.Equal(0, Manipulator.Rotation(Box, Box.CentreX, Box.Y - 50, snap: false), 6);
        Assert.Equal(90, Manipulator.Rotation(Box, Box.Right + 50, Box.CentreY, snap: false), 6);
    }

    [Fact]
    public void LAggancioPortaAiQuindiciGradi()
    {
        var snapped = Manipulator.Rotation(Box, Box.CentreX + 10, Box.Y - 50, snap: true);

        Assert.Equal(0, snapped % 15, 6);
    }

    [Fact]
    public void LeManiglieSonoNoveComprendendoLaRotazione()
    {
        Assert.Equal(9, Manipulator.Handles(Box).Count);
    }
}

public class ArcFlattenerTests
{
    private static readonly IArcFlattener Flattener = new ArcFlattener();
    private static readonly IShapeGeometry Geometry = new ShapeGeometry();

    [Fact]
    public void GliArchiDiventanoCubiche()
    {
        var flattened = Flattener.Flatten(Geometry.Outline(ShapeKind.RoundedRectangle));

        Assert.DoesNotContain(flattened.Figures[0].Segments, segment => segment is ArcSegment);
        Assert.Contains(flattened.Figures[0].Segments, segment => segment is CubicSegment);
    }

    [Fact]
    public void IlNumeroDiSegmentiNonCambia()
    {
        var outline = Geometry.Outline(ShapeKind.RoundedRectangle);
        var flattened = Flattener.Flatten(outline);

        Assert.Equal(outline.Figures[0].Segments.Count, flattened.Figures[0].Segments.Count);
    }

    [Fact]
    public void IPuntiDiArrivoRestanoGliStessi()
    {
        var outline = Geometry.Outline(ShapeKind.RoundedRectangle);
        var flattened = Flattener.Flatten(outline);

        var arcs = outline.Figures[0].Segments.OfType<ArcSegment>().ToList();
        var cubics = flattened.Figures[0].Segments.OfType<CubicSegment>().ToList();

        Assert.Equal(arcs.Count, cubics.Count);

        for (var index = 0; index < arcs.Count; index++)
            Assert.Equal(arcs[index].To, cubics[index].To);
    }

    [Fact]
    public void IPuntiDiControlloRestanoNelRiquadro()
    {
        var flattened = Flattener.Flatten(Geometry.Outline(ShapeKind.RoundedRectangle));

        foreach (var cubic in flattened.Figures[0].Segments.OfType<CubicSegment>())
        {
            Assert.InRange(cubic.First.X, -0.001, 1.001);
            Assert.InRange(cubic.First.Y, -0.001, 1.001);
            Assert.InRange(cubic.Second.X, -0.001, 1.001);
            Assert.InRange(cubic.Second.Y, -0.001, 1.001);
        }
    }

    [Fact]
    public void UnaFormaSenzaArchiNonCambia()
    {
        var outline = Geometry.Outline(ShapeKind.Triangle);

        Assert.Equal(
            outline.Figures[0].Segments.Count,
            Flattener.Flatten(outline).Figures[0].Segments.Count);
    }
}

public class ArcDirectionTests
{
    private static readonly IArcFlattener Flattener = new ArcFlattener();

    private static CubicSegment Flatten(ShapePoint from, ShapePoint to, bool clockwise) =>
        Assert.IsType<CubicSegment>(Flattener.Flatten(new ShapeOutline(
        [
            new ShapeFigure([new MoveSegment(from), new ArcSegment(to, 1, 1, clockwise)]),
        ])).Figures[0].Segments[1]);

    [Fact]
    public void UnArcoOrarioPartTangenteVerticale()
    {
        var cubic = Flatten(new ShapePoint(0, 1), new ShapePoint(1, 0), clockwise: true);

        Assert.Equal(0, cubic.First.X, 6);
    }

    [Fact]
    public void UnArcoAntiorarioPartTangenteOrizzontale()
    {
        var cubic = Flatten(new ShapePoint(0, 1), new ShapePoint(1, 0), clockwise: false);

        Assert.Equal(1, cubic.First.Y, 6);
    }
}
