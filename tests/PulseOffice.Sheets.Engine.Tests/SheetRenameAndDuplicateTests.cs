using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SheetRenameAndDuplicateTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public void RinominareUnFoglioRiscriveLeFormuleEINomi()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var dati = workbook.Sheets[0];
        var altro = workbook.AddSheet("Riepilogo");

        engine.SetValue(new SheetCell(0, A("A1")), CellValue.Number(7));
        engine.SetFormula(new SheetCell(1, A("B1")), "=Sheet1.A1*2");
        workbook.DefineName(new DefinedName("Totale", "Sheet1.A1"));

        var renaming = new SheetRenaming();
        var plan = renaming.Plan(workbook, dati, "Dati 2024");
        renaming.Apply(engine, plan);

        Assert.Equal("Dati 2024", dati.Name);
        Assert.Contains("'Dati 2024'.A1", altro[A("B1")].Formula);
        Assert.Equal(14, engine.GetValue(new SheetCell(1, A("B1"))).AsNumber());
        Assert.Contains("'Dati 2024'", workbook.FindName("Totale")!.Expression);

        renaming.Revert(engine, plan);

        Assert.Equal("Sheet1", dati.Name);
        Assert.Equal("=Sheet1.A1*2", altro[A("B1")].Formula);
        Assert.Equal("Sheet1.A1", workbook.FindName("Totale")!.Expression);
    }

    [Fact]
    public void DuplicareUnFoglioCopiaTuttoConOggettiNuovi()
    {
        var (workbook, _) = fixture.CreateWorkbook();
        var source = workbook.Sheets[0];

        source.SetValue(A("A1"), CellValue.Number(1));
        source.Merges.Add(new CellRange(A("B2"), A("C3")));
        source.Comments[A("A1")] = new CellComment { Text = "nota" };
        source.Layout.HideRow(4);

        var shape = new PlacedObject
        {
            Id = "forma",
            Anchor = A("D4"),
            Content = new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle },
        };

        var connector = new PlacedObject
        {
            Id = "linea",
            Anchor = A("E5"),
            Content = new ConnectorObject { Id = "linea", Start = new ConnectorEndpoint("forma") },
        };

        source.Objects.AddRange([shape, connector]);

        var copy = new SheetDuplicator().Duplicate(new Workbook(), source, "Copia").Sheet;

        Assert.Equal(1, copy[A("A1")].Value.AsNumber());
        Assert.Single(copy.Merges.All);
        Assert.True(copy.Comments.ContainsKey(A("A1")));
        Assert.True(copy.Layout.IsRowHidden(4));
        Assert.Equal(2, copy.Objects.Count);
        Assert.DoesNotContain(copy.Objects, placed => placed.Id is "forma" or "linea");

        var copiedShape = copy.Objects.Single(placed => placed.Content is ShapeObject);
        var copiedConnector = (ConnectorObject)copy.Objects.Single(placed => placed.Content is ConnectorObject).Content;
        Assert.Equal(copiedShape.Id, copiedConnector.Start.ObjectId);

        copy.Layout.ShowRow(4);
        Assert.True(source.Layout.IsRowHidden(4));
    }

    [Fact]
    public void INomiDefinitiValidiRifiutanoRiferimentiECaratteriStrani()
    {
        var rules = new DefinedNameRules();

        Assert.True(rules.IsValid("Totale_2024"));
        Assert.False(rules.IsValid("A1"));
        Assert.False(rules.IsValid("1anno"));
        Assert.False(rules.IsValid("con spazio"));
        Assert.Equal("Vendite_nord", rules.Sanitised("Vendite nord"));
        Assert.True(rules.IsValid(rules.Sanitised("2024")));
    }
}
