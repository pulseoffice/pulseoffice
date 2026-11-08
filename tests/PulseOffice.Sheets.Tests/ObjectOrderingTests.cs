using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class ObjectOrderingTests
{
    private static readonly IObjectOrdering Ordering = new ObjectOrdering();

    private static Worksheet SheetWith(params string[] names)
    {
        var sheet = new Worksheet("Foglio1");
        var order = 0;

        foreach (var name in names)
            sheet.Objects.Add(new PlacedObject
            {
                Id = name,
                Content = new ShapeObject { Id = name, Shape = ShapeKind.Rectangle },
                Anchor = CellAddress.First,
                ZOrder = order++,
            });

        return sheet;
    }

    private static string[] Names(IEnumerable<PlacedObject> objects) =>
        [.. objects.Select(placed => placed.Id)];

    private static PlacedObject Named(Worksheet sheet, string name) =>
        sheet.Objects.First(placed => placed.Id == name);

    [Fact]
    public void SenzaOrdinamentoSiDisegnaNellOrdineDiInserimento()
    {
        Assert.Equal(["a", "b", "c"], Names(Ordering.Drawn(SheetWith("a", "b", "c"))));
    }

    [Fact]
    public void LOrdineEsplicitoVinceSullInserimento()
    {
        var sheet = SheetWith("a", "b", "c");
        sheet.Objects[0] = sheet.Objects[0] with { ZOrder = 10 };

        Assert.Equal(["b", "c", "a"], Names(Ordering.Drawn(sheet)));
    }

    [Fact]
    public void PortareInPrimoPianoMetteInFondoAllElenco()
    {
        var sheet = SheetWith("a", "b", "c");

        var result = Ordering.Reordered(sheet, [Named(sheet, "a")], ObjectOrderMove.ToFront);

        Assert.Equal(["b", "c", "a"], Names(result));
    }

    [Fact]
    public void PortareInFondoMetteAllInizio()
    {
        var sheet = SheetWith("a", "b", "c");

        var result = Ordering.Reordered(sheet, [Named(sheet, "c")], ObjectOrderMove.ToBack);

        Assert.Equal(["c", "a", "b"], Names(result));
    }

    [Fact]
    public void PortareAvantiScambiaConIlSuccessivo()
    {
        var sheet = SheetWith("a", "b", "c");

        var result = Ordering.Reordered(sheet, [Named(sheet, "a")], ObjectOrderMove.Forward);

        Assert.Equal(["b", "a", "c"], Names(result));
    }

    [Fact]
    public void PortareIndietroScambiaConIlPrecedente()
    {
        var sheet = SheetWith("a", "b", "c");

        var result = Ordering.Reordered(sheet, [Named(sheet, "c")], ObjectOrderMove.Backward);

        Assert.Equal(["a", "c", "b"], Names(result));
    }

    [Fact]
    public void LOggettoInCimaNonVaOltre()
    {
        var sheet = SheetWith("a", "b");

        var result = Ordering.Reordered(sheet, [Named(sheet, "b")], ObjectOrderMove.Forward);

        Assert.Equal(["a", "b"], Names(result));
    }

    [Fact]
    public void PiuOggettiSiMuovonoInsiemeSenzaScavalcarsi()
    {
        var sheet = SheetWith("a", "b", "c", "d");

        var result = Ordering.Reordered(
            sheet, [Named(sheet, "a"), Named(sheet, "b")], ObjectOrderMove.Forward);

        Assert.Equal(["c", "a", "b", "d"], Names(result));
    }

    [Fact]
    public void DopoIlRiordinoGliIndiciSonoConsecutivi()
    {
        var sheet = SheetWith("a", "b", "c");

        var result = Ordering.Reordered(sheet, [Named(sheet, "a")], ObjectOrderMove.ToFront);

        Assert.Equal([0, 1, 2], result.Select(placed => placed.ZOrder));
    }

    [Fact]
    public void IlProssimoOrdineStaSopraATutti()
    {
        Assert.Equal(3, Ordering.NextOrder(SheetWith("a", "b", "c")));
        Assert.Equal(0, Ordering.NextOrder(new Worksheet("Vuoto")));
    }
}
