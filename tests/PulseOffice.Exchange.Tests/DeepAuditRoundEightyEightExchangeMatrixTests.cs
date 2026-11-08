using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using PulseOffice.Charts;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Exchange.Tests;

public sealed class DeepAuditRoundEightyEightExchangeMatrixTests
{
    private static readonly Type[] Anchors = [typeof(ChartDefinition), typeof(ShapeObject), typeof(FormulaObject)];

    private readonly LoadedExchangeTypeCatalog _types = new();

    private readonly JsonExchangeSerializer _serializer;

    public DeepAuditRoundEightyEightExchangeMatrixTests()
    {
        Assert.NotEmpty(Anchors);
        _serializer = new(_types, new ExchangeContentGuard());
    }

    public static TheoryData<string> Kinds()
    {
        Assert.NotEmpty(Anchors);
        return [.. new LoadedExchangeTypeCatalog().DerivedFrom(typeof(EmbeddedObject)).Select(type => type.FullName!)];
    }

    private EmbeddedObject Populated(string kind)
    {
        var type = _types.DerivedFrom(typeof(EmbeddedObject)).Single(candidate => candidate.FullName == kind);
        return Assert.IsAssignableFrom<EmbeddedObject>(new PopulatedSamples(_types).Create(type));
    }

    private byte[] Packaged(EmbeddedObject value) =>
        _serializer.Write(new ExchangePackage
        {
            Id = "matrix",
            Origin = ModuleKind.Presentations,
            Content = new ObjectsContent([new ExchangedObject(value, new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)))]),
        });

    [Fact]
    public void LaMatriceCopreOgniTipoDiOggettoDelModello()
    {
        var kinds = Kinds().Select(row => (string)row.Data).ToList();
        foreach (var expected in new[] { "ChartDefinition", "ShapeObject", "ShapeGroupObject", "ConnectorObject", "FreeformObject", "FontworkObject", "RasterImageObject", "VectorGraphicObject", "Scene3DObject", "FormControlObject", "DataTableObject", "FormulaObject", "PackagedObject", "SignatureLineObject" })
            Assert.Contains(kinds, kind => kind.EndsWith("." + expected, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void OgniOggettoConTuttiIMembriPopolatiAttraversaLaFormaNativaSenzaPerdite(string kind)
    {
        var written = Packaged(Populated(kind));

        var read = _serializer.Read(written);

        Assert.NotNull(read);
        Assert.Equal(Encoding.UTF8.GetString(written), Encoding.UTF8.GetString(_serializer.Write(read)));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void UnPacchettoDiUnaVersionePrecedenteSenzaUnMembroFacoltativoSiIncollaAncora(string kind)
    {
        var value = Populated(kind);
        var json = JsonNode.Parse(Packaged(value))!;
        var members = ((JsonObject)json["Content"]!["Objects"]![0]!["Object"]!).Select(member => member.Key).Where(key => key != "$type").ToList();

        foreach (var member in members)
        {
            var older = JsonNode.Parse(json.ToJsonString())!;
            ((JsonObject)older["Content"]!["Objects"]![0]!["Object"]!).Remove(member);

            var read = _serializer.Read(Encoding.UTF8.GetBytes(older.ToJsonString()));

            if (Required(value.GetType(), member)) Assert.Null(read);
            else Assert.True(read is { Content: ObjectsContent { Objects.Count: 1 } }, $"{kind} without {member} no longer pastes");
        }
    }

    [Fact]
    public void UnPacchettoDiUnaVersioneSuccessivaConMembriSconosciutiSiIncollaAncora()
    {
        var json = JsonNode.Parse(Packaged(new ShapeObject { Id = "s", Shape = ShapeKind.Ellipse, Text = "x" }))!;
        json["FutureEnvelopeMember"] = 3;
        json["Content"]!["Objects"]![0]!["Object"]!["FutureMember"] = new JsonObject { ["Nested"] = "y" };

        var read = _serializer.Read(Encoding.UTF8.GetBytes(json.ToJsonString()));

        Assert.Equal("x", Assert.IsType<ShapeObject>(Assert.Single(Assert.IsType<ObjectsContent>(read!.Content).Objects).Object).Text);
    }

    [Fact]
    public void UnTipoDiOggettoSconosciutoDiUnaVersioneSuccessivaRicadeSulleRappresentazioniEstranee()
    {
        var json = JsonNode.Parse(Packaged(new ShapeObject { Id = "s", Shape = ShapeKind.Ellipse }))!;
        json["Content"]!["Objects"]![0]!["Object"]!["$type"] = "PulseOffice.Future.HologramObject";

        Assert.Null(_serializer.Read(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Fact]
    public void UnValoreDiEnumerazioneSconosciutoRicadeSulleRappresentazioniEstranee()
    {
        var json = JsonNode.Parse(Packaged(new ShapeObject { Id = "s", Shape = ShapeKind.Ellipse }))!;
        json["Content"]!["Objects"]![0]!["Object"]!["Shape"] = "HyperCube";

        Assert.Null(_serializer.Read(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Fact]
    public void UnIntervalloDiCelleConTuttiIMembriAttraversaLaFormaNativaEToleraMembriMancanti()
    {
        var populated = Assert.IsType<ExchangeCell>(new PopulatedSamples(_types).Create(typeof(ExchangeCell)));
        var line = populated.Borders!.Left! with { ColorHex = "#445566", WidthPoints = 1.25 };
        var cell = populated with { Borders = new ExchangeBorders { Left = line, Top = line, Right = line, Bottom = line } };
        var grid = new CellRangeContent { SheetName = "S", FirstRow = 2, FirstColumn = 3, RowCount = 1, ColumnCount = 2, Rows = [[cell with { ColorHex = "#112233", BackgroundHex = "#AABBCC" }, cell with { Formula = "=A1", ColorHex = null, BackgroundHex = null }]] };
        var written = _serializer.Write(new ExchangePackage { Id = "g", Origin = ModuleKind.Sheets, Content = grid });

        Assert.Equal(Encoding.UTF8.GetString(written), Encoding.UTF8.GetString(_serializer.Write(_serializer.Read(written)!)));
        foreach (var member in ((JsonObject)JsonNode.Parse(written)!["Content"]!["Rows"]![0]![0]!).Select(member => member.Key).ToList())
        {
            var older = JsonNode.Parse(written)!;
            ((JsonObject)older["Content"]!["Rows"]![0]![0]!).Remove(member);
            Assert.NotNull(_serializer.Read(Encoding.UTF8.GetBytes(older.ToJsonString())));
        }
    }

    [Fact]
    public void IValoriDiUnaTabellaDatiArrivanoConIlLoroTipoENonComeElementiJson()
    {
        var date = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Unspecified);
        var table = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot
            {
                Columns = [new DataColumn("A", DataValueKind.Text), new DataColumn("B", DataValueKind.Number), new DataColumn("C", DataValueKind.Boolean), new DataColumn("D", DataValueKind.Date), new DataColumn("E", DataValueKind.Number)],
                Rows = [["uno", 2.5, true, date, null], ["", -3d, false, date.AddDays(1), 7d]],
            },
        };

        var read = Assert.IsType<DataTableObject>(Assert.Single(Assert.IsType<ObjectsContent>(_serializer.Read(Packaged(table))!.Content).Objects).Object);

        Assert.Equal<object?>(["uno", 2.5, true, date, null], read.Snapshot.Rows[0]);
        Assert.Equal<object?>(["", -3d, false, date.AddDays(1), 7d], read.Snapshot.Rows[1]);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(2)]
    public void UnValoreDiEnumerazioneNumericoNonEntraNelModello(int value)
    {
        var json = JsonNode.Parse(Packaged(new ShapeObject { Id = "s", Shape = ShapeKind.Ellipse }))!;
        json["Content"]!["Objects"]![0]!["Object"]!["Shape"] = value;

        Assert.Null(_serializer.Read(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Fact]
    public void UnValoreTipizzatoMalformatoInUnaTabellaDatiNonFaCadereLaLettura()
    {
        var table = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot { Columns = [new DataColumn("D", DataValueKind.Date)], Rows = [[new DateTime(2026, 1, 1)]] },
        };
        var text = Encoding.UTF8.GetString(Packaged(table));
        foreach (var hostile in new[] { text.Replace("2026-01-01T00:00:00.0000000", "domani", StringComparison.Ordinal), text.Replace("\u0022DateTime\u0022", "\u0022Hologram\u0022", StringComparison.Ordinal).Replace("\"DateTime\"", "\"Hologram\"", StringComparison.Ordinal) })
            Assert.Null(_serializer.Read(Encoding.UTF8.GetBytes(hostile)));
    }

    private bool Required(Type type, string member)
    {
        var property = type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        if (property?.GetCustomAttribute<RequiredMemberAttribute>() is not null) return true;
        return type.GetConstructors()
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .First()
            .GetParameters()
            .Any(parameter => string.Equals(parameter.Name, member, StringComparison.OrdinalIgnoreCase) && !parameter.HasDefaultValue);
    }
}
