using PulseOffice.Charts;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Exchange.Tests;

public sealed class ExchangeSerializerTests
{
    private readonly JsonExchangeSerializer _serializer = new(new LoadedExchangeTypeCatalog(), new ExchangeContentGuard());

    public static TheoryData<string> Kinds() => [.. Samples().Keys];

    private static Dictionary<string, EmbeddedObject> Samples()
    {
        var shape = new ShapeObject
        {
            Id = "s1",
            Shape = ShapeKind.Star5,
            Text = "Stella",
            RotationDegrees = 15,
            Style = ShapeStyle.Default with { Fill = ShapeFill.Default with { ColorHex = "#DC2626", Opacity = 0.5 } },
        };
        return new Dictionary<string, EmbeddedObject>(StringComparer.Ordinal)
        {
            ["chart"] = new ChartDefinition
            {
                Id = "c1",
                Type = ChartType.Column,
                SourceSheet = "Foglio1",
                SourceRange = "A1:B3",
                ChartTitle = "Vendite",
                InlineData = new ChartData(["Gen", "Feb"], [new ChartSeries("Serie", [1, 2]) { ColorHex = "#16A34A" }]),
            },
            ["shape"] = shape,
            ["group"] = new ShapeGroupObject { Id = "g1", Children = [new GroupChild(shape, 0, 0, 0.5, 0.5), new GroupChild(shape with { Id = "s2" }, 0.5, 0.5, 0.5, 0.5)] },
            ["image"] = new RasterImageObject { Id = "i1", Data = new byte[] { 1, 2, 3, 4 }, MediaType = "image/png", PixelWidth = 2, PixelHeight = 2, Crop = (0.1, 0, 0, 0.2), Filter = ImageFilter.Sepia },
            ["vector"] = new VectorGraphicObject { Id = "v1", Asset = new VectorAsset { Id = "a", Name = "Icona", SourceKind = default, SvgContent = "<svg/>" }, RecolorHex = "#000000" },
            ["freeform"] = new FreeformObject { Id = "f1", Path = new FreeformPath([new PathFigure([new PathNode(new ShapePoint(0, 0)), new PathNode(new ShapePoint(1, 1)) { Before = new ShapePoint(0.5, 0) }]) { Closed = true }]) },
            ["connector"] = new ConnectorObject { Id = "k1", Start = new ConnectorEndpoint("s1") },
            ["fontwork"] = new FontworkObject { Id = "w1", Text = "Titolo" },
            ["formula"] = new FormulaObject { Id = "m1", Expression = "a^2 + b^2" },
            ["table"] = new DataTableObject
            {
                Id = "t1",
                Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
                Snapshot = new DataSnapshot { Columns = [new DataColumn("A", DataValueKind.Text)], Rows = [["uno"], ["due"]] },
            },
            ["package"] = new PackagedObject { Id = "p1", FileName = "dati.ods", MediaType = "application/vnd.oasis.opendocument.spreadsheet", Data = new byte[] { 9, 8, 7 } },
            ["control"] = new FormControlObject { Id = "x1", ControlKind = FormControlKind.CheckBox },
            ["signature"] = new SignatureLineObject { Id = "l1" },
            ["dimension"] = DimensionSample(),
            ["scene"] = new PulseOffice.Drawing.Scenes.Scene3DObject { Id = "d1", Markup = "<dr3d:scene xmlns:dr3d=\"urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0\"/>", Styles = ["<style:style style:name=\"gr1\"/>"] },
        };
    }

    private static ShapeObject DimensionSample() => new()
    {
        Id = "q1",
        Shape = ShapeKind.Line,
        RotationDegrees = 30,
        Dimension = new DimensionLine
        {
            Start = new ShapePoint(0, 0.8),
            End = new ShapePoint(1, 0.8),
            LineDistance = Twip.FromCentimeters(-0.5),
            Placing = DimensionPlacing.Below,
            Unit = "cm",
            ShowUnit = true,
            DecimalPlaces = 1,
            Label = "Base",
        },
    };

    [Fact]
    public void Una_quota_e_una_scena_3d_attraversano_la_forma_nativa_con_tutti_i_dati()
    {
        var scene = (PulseOffice.Drawing.Scenes.Scene3DObject)Samples()["scene"];
        var package = new ExchangePackage
        {
            Id = "q",
            Origin = ModuleKind.Documents,
            Content = new ObjectsContent([new ExchangedObject(DimensionSample(), DocumentSize.Empty), new ExchangedObject(scene, DocumentSize.Empty)]),
        };

        var objects = Assert.IsType<ObjectsContent>(_serializer.Read(_serializer.Write(package))!.Content).Objects;

        Assert.Equal(DimensionSample().Dimension, Assert.IsType<ShapeObject>(objects[0].Object).Dimension);
        Assert.Equal(scene, objects[1].Object);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Ogni_tipo_di_oggetto_attraversa_la_forma_nativa_intatto(string kind)
    {
        var original = Samples()[kind];
        var package = new ExchangePackage
        {
            Id = "abc",
            Origin = ModuleKind.Presentations,
            Content = new ObjectsContent([new ExchangedObject(original with { Name = "Nome", Title = "Titolo" }, new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(3)))]),
        };

        var written = _serializer.Write(package);
        var read = _serializer.Read(written);

        Assert.NotNull(read);
        Assert.Equal("abc", read.Id);
        Assert.Equal(ModuleKind.Presentations, read.Origin);
        var exchanged = Assert.Single(Assert.IsType<ObjectsContent>(read.Content).Objects);
        Assert.IsType(original.GetType(), exchanged.Object);
        Assert.Equal("Nome", exchanged.Object.Name);
        Assert.Equal(Twip.FromCentimeters(5), exchanged.Size.Width);
        Assert.Equal(written, _serializer.Write(read));
    }

    [Fact]
    public void Una_porzione_di_foglio_attraversa_la_forma_nativa_con_formule_e_formati()
    {
        var grid = new CellRangeContent
        {
            SheetName = "Conti",
            RowCount = 1,
            ColumnCount = 2,
            Rows = [[new ExchangeCell { Kind = ExchangeCellKind.Number, Number = 3, Text = "3,00", Bold = true }, new ExchangeCell { Kind = ExchangeCellKind.Number, Number = 6, Text = "6", Formula = "=A1*2", BackgroundHex = "#FFFF00" }]],
        };

        var read = _serializer.Read(_serializer.Write(new ExchangePackage { Id = "g", Origin = ModuleKind.Sheets, Content = grid }));

        var cells = Assert.IsType<CellRangeContent>(read!.Content);
        Assert.Equal("=A1*2", cells.At(0, 1).Formula);
        Assert.True(cells.At(0, 0).Bold);
        Assert.Equal("#FFFF00", cells.At(0, 1).BackgroundHex);
    }

    [Fact]
    public void Dati_estranei_o_rotti_non_diventano_un_pacchetto()
    {
        Assert.Null(_serializer.Read(System.Text.Encoding.UTF8.GetBytes("{\"Version\":99,\"Id\":\"x\"}")));
        Assert.Null(_serializer.Read(System.Text.Encoding.UTF8.GetBytes("non json")));
        Assert.Null(_serializer.Read(ReadOnlyMemory<byte>.Empty));
    }
}
