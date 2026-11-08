using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Text.Rich.Tests;

public sealed class CaretRowNavigationTests
{
    private readonly CaretRowNavigation _navigation = new();

    private CaretRow Row(int first, int last, double indent = 0) =>
        new(Enumerable.Range(first, last - first + 1).Select(index => new CaretStop(index, indent + (index - first) * 10)).ToList());

    private readonly IReadOnlyList<CaretRow> _rows;

    public CaretRowNavigationTests() => _rows = [Row(0, 9, 30), Row(10, 13), Row(14, 25)];

    [Fact]
    public void GiuMantieneLaColonnaSullaRigaSuccessiva()
    {
        var target = _navigation.Move(_rows, 2, null, CaretMove.Down, 1);
        Assert.Equal(13, target.Index);
        Assert.Equal(50, target.PreferredX);
    }

    [Fact]
    public void LaColonnaPreferitaSopravviveAUnaRigaCorta()
    {
        var first = _navigation.Move(_rows, 7, null, CaretMove.Down, 1);
        Assert.Equal(13, first.Index);
        var second = _navigation.Move(_rows, first.Index, first.PreferredX, CaretMove.Down, 1);
        Assert.Equal(24, second.Index);
    }

    [Fact]
    public void SuDallaPrimaRigaVaAllInizio()
    {
        Assert.Equal(0, _navigation.Move(_rows, 4, null, CaretMove.Up, 1).Index);
        Assert.Equal(25, _navigation.Move(_rows, 20, null, CaretMove.Down, 1).Index);
    }

    [Fact]
    public void InizioEFineDiRigaSonoQuelliDellaRigaImpaginata()
    {
        Assert.Equal(10, _navigation.Move(_rows, 12, null, CaretMove.RowStart, 1).Index);
        Assert.Equal(13, _navigation.Move(_rows, 11, null, CaretMove.RowEnd, 1).Index);
        Assert.Null(_navigation.Move(_rows, 11, 40, CaretMove.RowEnd, 1).PreferredX);
    }

    [Fact]
    public void UnIndiceAlConfineAppartieneAllaRigaDopo()
    {
        var rows = new[] { Row(0, 5), Row(5, 9) };
        Assert.Equal(5, _navigation.Move(rows, 5, null, CaretMove.RowStart, 1).Index);
        Assert.Equal(0, _navigation.Move(rows, 5, null, CaretMove.Up, 1).Index);
    }

    [Fact]
    public void PaginaGiuArrivaAllUltimaRigaConLaColonna()
    {
        var target = _navigation.Move(_rows, 1, null, CaretMove.PageDown, 10);
        Assert.Equal(18, target.Index);
        Assert.Equal(25, _navigation.Move(_rows, target.Index, target.PreferredX, CaretMove.PageDown, 10).Index);
    }

    [Fact]
    public void FineSuUnaRigaSpezzataRestaSullaRiga()
    {
        IReadOnlyList<CaretRow> wrapped = [Row(0, 6), Row(6, 12)];
        var end = _navigation.Move(wrapped, 2, null, CaretMove.RowEnd, 1);
        Assert.Equal(5, end.Index);
        Assert.Equal(5, _navigation.Move(wrapped, end.Index, null, CaretMove.RowEnd, 1).Index);
    }

    [Fact]
    public void RowEndBeforeAWrappedEmojiDoesNotSplitTheSurrogatePair()
    {
        var first = new CaretRow([new CaretStop(0, 0), new CaretStop(1, 10), new CaretStop(3, 20), new CaretStop(5, 30)]);
        var second = new CaretRow([new CaretStop(5, 0), new CaretStop(6, 10)]);

        var target = new CaretRowNavigation().Move([first, second], 0, null, CaretMove.RowEnd, 1);

        Assert.Equal(3, target.Index);
    }
}
