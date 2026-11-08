using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Packaging;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfShapesAuditTests
{
    private readonly XNamespace _draw = OdfPackage.Namespaces.Draw;
    private readonly XNamespace _style = OdfPackage.Namespaces.Style;
    private readonly XNamespace _fo = OdfPackage.Namespaces.Fo;
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private void Change(MemoryStream stream, Action<XDocument> change)
    {
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument content;
            using (var input = entry.Open()) content = XDocument.Load(input);
            change(content);
            entry.Delete();
            using var output = archive.CreateEntry("content.xml").Open();
            content.Save(output, SaveOptions.DisableFormatting);
        }
        stream.Position = 0;
    }

    private ShapeObject ReopenedShape(ShapeObject shape, Action<XDocument> change)
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = shape.Id,
            Content = shape,
            Anchor = new CellAddress(3, 2),
            Size = new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2)),
        });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        Change(stream, change);
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return Assert.IsType<ShapeObject>(Assert.Single(result.Value!.Sheets[0].Objects).Content);
    }

    private XElement Custom(XDocument content) => content.Descendants(_draw + "custom-shape").First();

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e308grad")]
    public void Un_angolo_dell_arco_non_finito_non_entra_nel_modello(string angle)
    {
        var shape = ReopenedShape(new ShapeObject { Id = "a1", Shape = ShapeKind.Rectangle }, content =>
        {
            var custom = Custom(content);
            custom.Name = _draw + "ellipse";
            custom.Elements(_draw + "enhanced-geometry").Remove();
            custom.SetAttributeValue(_draw + "kind", "arc");
            custom.SetAttributeValue(_draw + "start-angle", angle);
        });

        Assert.Equal(ShapeKind.Arc, shape.Shape);
        Assert.True(double.IsFinite(shape.ArcStartDegrees));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void Una_rotazione_non_finita_non_entra_nel_modello(string radians)
    {
        var shape = ReopenedShape(new ShapeObject { Id = "r1", Shape = ShapeKind.Rectangle }, content =>
        {
            var custom = Custom(content);
            custom.SetAttributeValue(OdfPackage.Namespaces.Svg + "x", null);
            custom.SetAttributeValue(OdfPackage.Namespaces.Svg + "y", null);
            custom.SetAttributeValue(_draw + "transform", $"rotate ({radians}) translate (1cm 1cm)");
        });

        Assert.True(double.IsFinite(shape.RotationDegrees));
    }

    [Theory]
    [InlineData("NaNpt")]
    [InlineData("Infinitypt")]
    [InlineData("1e300pt")]
    [InlineData("-5pt")]
    public void La_dimensione_di_un_tratto_di_testo_della_forma_ha_un_tetto(string size)
    {
        var shape = ReopenedShape(new ShapeObject
        {
            Id = "t1",
            Shape = ShapeKind.Rectangle,
            Text = "Ciao mondo",
            Runs = [new ShapeTextRun("Ciao ") { Bold = true, Size = 18 }, new ShapeTextRun("mondo")],
        }, content =>
        {
            foreach (var properties in content.Descendants(_style + "text-properties").Where(element => (string?)element.Attribute(_fo + "font-size") == "18pt"))
                properties.SetAttributeValue(_fo + "font-size", size);
        });

        Assert.Contains(shape.Runs ?? [], run => run.Bold);
        Assert.All(shape.Runs ?? [], run => Assert.True(run.Size is null or (>= 1 and <= 4000)));
    }

    [Theory]
    [InlineData("1e300cm 0")]
    [InlineData("NaNcm 0")]
    [InlineData("Infinitycm 0")]
    public void La_profondita_dell_estrusione_ha_un_tetto(string depth)
    {
        var shape = ReopenedShape(new ShapeObject { Id = "e1", Shape = ShapeKind.Rectangle }, content =>
        {
            var geometry = Custom(content).Element(_draw + "enhanced-geometry")!;
            geometry.SetAttributeValue(_draw + "extrusion", "true");
            geometry.SetAttributeValue(_draw + "extrusion-depth", depth);
        });

        Assert.Equal(Twip.FromCentimeters(1), shape.Style.Extrusion.Depth);
    }

    [Theory]
    [InlineData("NaNpt")]
    [InlineData("1e300pt")]
    [InlineData("0pt")]
    public void La_dimensione_del_testo_di_una_cella_di_tabella_ha_un_tetto(string size)
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        slide.Add(new ObjectElement
        {
            Id = "t1",
            Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(6000), new Twip(3000)),
            Content = new DataTableObject
            {
                Id = "t1",
                Query = new DataQuerySource { ConnectionId = "", Sql = "" },
                Snapshot = new DataSnapshot { Columns = [new DataColumn("A", DataValueKind.Text)], Rows = [["x"]] },
                Style = new DataTableStyle { OwnLook = true },
                CellFormats = [new TableCellFormat(0, 0, CellVerticalAlign.Center) { Look = new TableCellLook { Bold = true, FontSize = 14 } }],
            },
        });
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        Change(stream, content =>
        {
            foreach (var properties in content.Descendants(_style + "text-properties").Where(element => element.Attribute(_fo + "font-size") is not null))
                properties.SetAttributeValue(_fo + "font-size", size);
        });

        var back = new OdpReader(_themes, _masters).Read(stream);
        Assert.True(back.Succeeded);
        var table = back.Value!.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<DataTableObject>().Single();
        Assert.Contains(table.CellFormats, format => format.Look is not null);
        Assert.All(table.CellFormats, format => Assert.True(format.Look?.FontSize is null or (>= 1 and <= 4000)));
    }

    [Fact]
    public void Una_tabella_di_grafico_incorporata_con_celle_ripetute_a_dismisura_resta_entro_un_tetto()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        slide.Add(new ObjectElement
        {
            Id = "ch1",
            Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(6000), new Twip(3000)),
            Content = new PulseOffice.Charts.ChartDefinition
            {
                Id = "ch1", Type = PulseOffice.Charts.ChartType.Column, SourceSheet = "", SourceRange = "",
                InlineData = new PulseOffice.Charts.ChartData(["Gen"], [new PulseOffice.Charts.ChartSeries("Nord", [10])]),
            },
        });
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        var table = OdfPackage.Namespaces.Table;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.Entries.Single(candidate => candidate.FullName.EndsWith("content.xml", StringComparison.Ordinal) && candidate.FullName.Contains('/'));
            var name = entry.FullName;
            XDocument chart;
            using (var input = entry.Open()) chart = XDocument.Load(input);
            var rows = chart.Descendants(table + "table-rows").Single();
            rows.RemoveNodes();
            for (var index = 0; index < 2000; index++)
                rows.Add(new XElement(table + "table-row", new XElement(table + "table-cell", new XAttribute(table + "number-columns-repeated", "1024"))));
            entry.Delete();
            using var output = archive.CreateEntry(name).Open();
            chart.Save(output, SaveOptions.DisableFormatting);
        }
        stream.Position = 0;

        var back = new OdpReader(_themes, _masters).Read(stream);
        Assert.True(back.Succeeded);
        var data = back.Value!.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<PulseOffice.Charts.ChartDefinition>().Single().InlineData!;
        Assert.InRange(data.Series.Sum(series => (long)series.Values.Count), 1, 1 << 20);
    }

    [Theory]
    [InlineData(1, 4, 1000)]
    [InlineData(400, 1, 1)]
    [InlineData(40, 40, 1000)]
    public void Righe_e_celle_ripetute_a_dismisura_in_una_tabella_di_Deck_restano_entro_un_tetto(int rowElements, int cellElements, int cellRepeat)
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        slide.Add(new ObjectElement
        {
            Id = "t1",
            Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(6000), new Twip(3000)),
            Content = new DataTableObject
            {
                Id = "t1",
                Query = new DataQuerySource { ConnectionId = "", Sql = "" },
                Snapshot = new DataSnapshot { Columns = [new DataColumn("A", DataValueKind.Text)], Rows = [["x"]] },
            },
        });
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        var table = OdfPackage.Namespaces.Table;
        Change(stream, content =>
        {
            var row = content.Descendants(table + "table-row").First();
            var parent = row.Parent!;
            foreach (var existing in parent.Elements(table + "table-row").ToList()) existing.Remove();
            for (var index = 0; index < rowElements; index++)
                parent.Add(new XElement(table + "table-row", new XAttribute(table + "number-rows-repeated", "1000"),
                    Enumerable.Range(0, cellElements).Select(_ => new XElement(table + "table-cell",
                        new XAttribute(table + "number-columns-repeated", cellRepeat.ToString(System.Globalization.CultureInfo.InvariantCulture))))));
        });

        var back = new OdpReader(_themes, _masters).Read(stream);
        Assert.True(back.Succeeded);
        var read = back.Value!.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<DataTableObject>().Single();
        Assert.InRange(read.Snapshot.Columns.Count, 1, 1000);
        Assert.InRange(read.Snapshot.Rows.Count, 1, 1000);
        Assert.InRange(read.Snapshot.Rows.Sum(values => (long)values.Count), 1, 100_000);
        Assert.InRange(read.Layout.ColumnFractions.Count, 0, 1000);
    }

    [Fact]
    public void Gruppi_di_righe_annidati_a_dismisura_in_una_tabella_di_Deck_non_fermano_la_lettura()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        slide.Add(new ObjectElement
        {
            Id = "t1",
            Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(6000), new Twip(3000)),
            Content = new DataTableObject
            {
                Id = "t1",
                Query = new DataQuerySource { ConnectionId = "", Sql = "" },
                Snapshot = new DataSnapshot { Columns = [new DataColumn("A", DataValueKind.Text)], Rows = [["x"]] },
            },
        });
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        var table = OdfPackage.Namespaces.Table;
        Change(stream, content =>
        {
            var row = content.Descendants(table + "table-row").First();
            var parent = row.Parent!;
            row.Remove();
            XElement innermost = row;
            for (var level = 0; level < 100_000; level++) innermost = new XElement(table + "table-row-group", innermost);
            parent.Add(innermost);
        });

        var back = new OdpReader(_themes, _masters).Read(stream);
        Assert.True(back.Succeeded);
    }

    [Fact]
    public void Gruppi_di_forme_annidati_a_dismisura_in_un_foglio_non_fermano_la_lettura()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "g1",
            Content = new ShapeObject { Id = "g1", Shape = ShapeKind.Rectangle },
            Anchor = new CellAddress(3, 2),
            Size = new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2)),
        });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        Change(stream, content =>
        {
            var custom = Custom(content);
            var parent = custom.Parent!;
            custom.Remove();
            XElement innermost = custom;
            for (var level = 0; level < 100_000; level++) innermost = new XElement(_draw + "g", innermost);
            parent.Add(innermost);
        });

        Assert.True(new OdsReader().Read(stream).Succeeded);
    }
}
