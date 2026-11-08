using PulseOffice.Testing;
using Avalonia.Threading;
using PulseOffice.Charts;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Scenes;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Settings;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class ExchangeMatrixTests : IDisposable
{
    private static readonly ModuleKind[] Modules = [ModuleKind.Sheets, ModuleKind.Documents, ModuleKind.Presentations];

    private readonly UiThreadSession _session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-exchange-matrix-" + Guid.NewGuid().ToString("N"));

    public ExchangeMatrixTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private static readonly byte[] Pixel = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    private static ShapeObject ListShape(string id) => new()
    {
        Id = id,
        Shape = ShapeKind.Rectangle,
        Text = "Uno\nDue" + ShapeTextRun.LineBreak + "a capo\nTre",
        Runs = [new ShapeTextRun("Uno\nDue" + ShapeTextRun.LineBreak + "a capo\nTre") { Bold = true, ColorHex = "#2563EB" }],
        Lists =
        [
            new ShapeListItem { Numbered = true, NumberFormat = default },
            new ShapeListItem { Level = 1, Symbol = new BulletSymbol("•") { Picture = new ListBulletPicture(Pixel, "image/png", 6, 6) } },
            null,
        ],
    };

    private static EmbeddedObject Sample(string kind, string id) => kind switch
    {
        "chart" => new ChartDefinition
        {
            Id = id,
            Type = ChartType.Line,
            SourceSheet = "Sheet1",
            SourceRange = "A1:B3",
            ChartTitle = "Vendite",
            InlineData = new ChartData(["Gen", "Feb"], [new ChartSeries("Vendite", [12, 30])]),
        },
        "shape-lists" => ListShape(id),
        "image-linked" => new RasterImageObject { Id = id, Data = Pixel, MediaType = "image/png", PixelWidth = 1, PixelHeight = 1, LinkPath = "/tmp/missing-picture.png", Mode = EmbedMode.Linked },
        "group-links" => new ShapeGroupObject
        {
            Id = id,
            Children =
            [
                new GroupChild(ListShape(id + "a"), 0, 0, 0.5, 1) { Hyperlink = "https://example.org/a" },
                new GroupChild(new ShapeGroupObject { Id = id + "n", Children = [new GroupChild(ListShape(id + "b"), 0, 0, 1, 1) { Hyperlink = "#Slide 2" }] }, 0.5, 0, 0.5, 1),
            ],
        },
        "connector" => new ConnectorObject { Id = id, Start = new ConnectorEndpoint("elsewhere") },
        "freeform" => new FreeformObject { Id = id, Path = new FreeformPath([new PathFigure([new PathNode(new ShapePoint(0, 0)), new PathNode(new ShapePoint(1, 1)) { Before = new ShapePoint(0.5, 0) }]) { Closed = true }]) },
        "fontwork" => new FontworkObject { Id = id, Text = "Titolo" },
        "dimension" => new ShapeObject
        {
            Id = id,
            Shape = ShapeKind.Line,
            Dimension = new DimensionLine { Start = new ShapePoint(0, 0.8), End = new ShapePoint(1, 0.8), Unit = "cm", ShowUnit = true, DecimalPlaces = 1 },
        },
        "scene" => new Scene3DObject { Id = id, Markup = "<dr3d:scene xmlns:dr3d=\"urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0\"/>", Styles = [] },
        "control" => new FormControlObject { Id = id, ControlKind = FormControlKind.CheckBox },
        "table" => new DataTableObject
        {
            Id = id,
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot { Columns = [new DataColumn("Voce", DataValueKind.Text), new DataColumn("Importo", DataValueKind.Number)], Rows = [["Ricavi", 2.5], ["Costi", 120d]] },
        },
        "formula" => new FormulaObject { Id = id, Expression = "a^2 + b^2 = c^2" },
        "package" => new PackagedObject { Id = id, FileName = "dati.bin", MediaType = "application/octet-stream", Data = new byte[] { 9, 8, 7 } },
        "signature" => new SignatureLineObject { Id = id },
        _ => new VectorGraphicObject { Id = id, Asset = new VectorAsset { Id = "a", Name = "Icona", SourceKind = default, SvgContent = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1 1\"><rect width=\"1\" height=\"1\"/></svg>" } },
    };

    public static TheoryData<string, ModuleKind> Crossings()
    {
        var data = new TheoryData<string, ModuleKind>();
        foreach (var kind in new[] { "chart", "shape-lists", "image-linked", "group-links", "connector", "freeform", "fontwork", "dimension", "scene", "control", "table", "formula", "package", "signature", "vector" })
            foreach (var from in Modules)
                data.Add(kind, from);
        return data;
    }

    private static async Task<ExchangePackage> Captured(ExchangeSuite suite, string kind, ModuleKind from)
    {
        suite.Place(from, Sample(kind, "origine"));
        Assert.True(await suite.Copy(from), $"{kind} cannot be copied from {from}");
        return suite.Exchange.Last!;
    }

    private static bool Received(ExchangeSuite suite, ModuleKind to, ExchangePackage package)
    {
        suite.Activate(to);
        if (to == ModuleKind.Presentations) suite.Deck.Select([]);
        var received = suite.Exchange.Receive(to, package);
        Dispatcher.UIThread.RunJobs();
        return received;
    }

    private static void PasteEveryFlavourAlone(ExchangeSuite suite, ExchangePackage package, string label)
    {
        foreach (var representation in package.Representations)
        {
            var alone = new ExchangePackage { Id = Guid.NewGuid().ToString("N"), Representations = [representation] };
            foreach (var to in Modules)
            {
                suite.Activate(to);
                var modes = suite.Exchange.Modes(to, alone);
                if (representation.Flavor == ExchangeFlavor.Png) Assert.Contains(ExchangePasteMode.Image, modes);
                if (representation.Flavor == ExchangeFlavor.Text && representation.Data.Length > 0) Assert.NotEmpty(modes);
                foreach (var mode in modes)
                {
                    var received = suite.Exchange.Receive(to, alone, mode);
                    Dispatcher.UIThread.RunJobs();
                    if (mode == ExchangePasteMode.Image) Assert.True(received, $"{label}: {representation.Flavor} as {mode} into {to}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Crossings))]
    public Task Ogni_tipo_di_oggetto_passa_in_ogni_modulo_in_forma_nativa_e_con_ogni_rappresentazione_da_sola(string kind, ModuleKind from) => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        var package = await Captured(suite, kind, from);
        Assert.IsType<ObjectsContent>(package.Content);
        var original = Assert.Single(((ObjectsContent)package.Content!).Objects).Object;

        foreach (var to in Modules)
        {
            var before = suite.Objects(to).Count;
            Assert.True(await suite.Paste(to), $"{kind} from {from} not pasted into {to}");
            var pasted = suite.Objects(to).Skip(before).ToList();
            Assert.Contains(pasted, item => item.GetType() == original.GetType());
        }

        var native = new ExchangePackage { Id = Guid.NewGuid().ToString("N"), Origin = from, Content = package.Content };
        var serializer = suite.Service<IExchangeSerializer>();
        var described = serializer.Read(serializer.Write(native));
        Assert.NotNull(described);
        foreach (var to in Modules)
        {
            var before = suite.Objects(to).Count;
            suite.Activate(to);
            Assert.True(suite.Exchange.Receive(to, described!, ExchangePasteMode.Objects) || to == from, $"{kind} described natively from {from} not pasted into {to}");
            Dispatcher.UIThread.RunJobs();
            Assert.True(suite.Objects(to).Count >= before);
        }

        PasteEveryFlavourAlone(suite, package, $"{kind} from {from}");
    }, CancellationToken.None);

    [Fact]
    public Task Celle_con_formule_stili_unioni_convalide_e_commenti_passano_in_ogni_modulo_con_ogni_rappresentazione() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        var context = suite.Ledger;
        var sheet = suite.Sheet;
        sheet[new CellAddress(0, 0)] = new Cell { Value = CellValue.Text("Mese") };
        sheet[new CellAddress(1, 0)] = new Cell { Value = CellValue.Number(12) };
        sheet[new CellAddress(1, 1)] = new Cell { Value = CellValue.Number(30), Formula = "=A2+18" };
        var bold = context.Workbook!.Styles.Intern(PulseOffice.Sheets.Styling.CellStyle.Default with { Font = PulseOffice.Sheets.Styling.CellFont.Default with { Bold = true } });
        sheet[new CellAddress(0, 0)] = sheet[new CellAddress(0, 0)] with { StyleId = bold, ExplicitStyle = true };
        sheet.Merges.Add(new CellRange(new CellAddress(0, 0), new CellAddress(0, 1)));
        sheet.Validations.Add(new DataValidation { Range = new CellRange(new CellAddress(1, 0), new CellAddress(1, 0)), Kind = ValidationKind.List, ListValues = ["12", "30"] });
        sheet.Comments[new CellAddress(1, 1)] = new CellComment { Text = "Nota" };
        context.SelectObject(null);
        context.SelectRange(new CellRange(new CellAddress(0, 0), new CellAddress(1, 1)));
        Dispatcher.UIThread.RunJobs();

        Assert.True(await suite.Copy(ModuleKind.Sheets));
        var package = suite.Exchange.Last!;
        Assert.IsType<CellRangeContent>(package.Content);

        foreach (var to in Modules)
        {
            if (to == ModuleKind.Sheets) context.SelectRange(new CellRange(new CellAddress(10, 0), new CellAddress(10, 0)));
            if (to == ModuleKind.Presentations) suite.Deck.Select([]);
            Assert.True(await suite.Paste(to), $"cells not pasted into {to}");
        }
        Assert.Equal("=A12+18", suite.Sheet[new CellAddress(11, 1)].Formula);
        Assert.NotNull(suite.Sheet.Merges.At(new CellAddress(10, 1)));

        PasteEveryFlavourAlone(suite, package, "cells");
    }, CancellationToken.None);

    [Fact]
    public Task Il_testo_di_Folio_e_una_casella_di_Deck_passano_in_ogni_modulo_con_ogni_rappresentazione() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        var editor = suite.Folio;
        editor.Type("Prima riga");
        editor.SelectAll();
        editor.Toggle(PulseOffice.Documents.UI.Editing.CharacterToggle.Bold);
        Assert.True(await suite.Copy(ModuleKind.Documents));
        var folio = suite.Exchange.Last!;
        foreach (var to in Modules)
        {
            if (to == ModuleKind.Presentations) suite.Deck.Select([]);
            Assert.True(await suite.Paste(to), $"Folio text not pasted into {to}");
        }
        PasteEveryFlavourAlone(suite, folio, "Folio text");

        var target = suite.Deck;
        var box = new PulseOffice.Presentations.TextElement
        {
            Id = "casella",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(8), Twip.FromCentimeters(3)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("Nota") { Runs = [new TextRun("Nota") { Bold = true }] }]),
        };
        target.Actions.InsertElement(target.CurrentSlide()!, box);
        target.Select([box]);
        Dispatcher.UIThread.RunJobs();
        Assert.True(await suite.Copy(ModuleKind.Presentations));
        var deck = suite.Exchange.Last!;
        foreach (var to in Modules) Assert.True(await suite.Paste(to), $"Deck text box not pasted into {to}");
        PasteEveryFlavourAlone(suite, deck, "Deck text box");
    }, CancellationToken.None);

    [Theory]
    [InlineData("chart")]
    [InlineData("group-links")]
    [InlineData("table")]
    public Task Un_pacchetto_nativo_di_una_versione_precedente_si_incolla_o_ricade_sulle_altre_rappresentazioni(string kind) => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        var package = await Captured(suite, kind, ModuleKind.Presentations);
        var serializer = suite.Service<IExchangeSerializer>();
        var json = System.Text.Json.Nodes.JsonNode.Parse(serializer.Write(package with { Origin = ModuleKind.Sheets }))!;
        json["Id"] = "older-build";
        var obj = (System.Text.Json.Nodes.JsonObject)json["Content"]!["Objects"]![0]!["Object"]!;
        foreach (var optional in new[] { "Name", "Title", "Description", "Source", "Mode" }) obj.Remove(optional);
        var older = new ExchangeRepresentation(ExchangeFlavor.Native, System.Text.Encoding.UTF8.GetBytes(json.ToJsonString()));

        var recognized = suite.Exchange.Recognize([older, .. package.Representations]);

        Assert.IsType<ObjectsContent>(recognized.Content);
        foreach (var to in Modules) Assert.True(Received(suite, to, recognized), $"older {kind} not pasted into {to}");

        obj["$type"] = "PulseOffice.Future.Hologram";
        var unknown = suite.Exchange.Recognize([new ExchangeRepresentation(ExchangeFlavor.Native, System.Text.Encoding.UTF8.GetBytes(json.ToJsonString())), .. package.Representations]);
        Assert.Null(unknown.Content);
        foreach (var to in Modules) Assert.True(Received(suite, to, unknown), $"fallback for {kind} not pasted into {to}");
    }, CancellationToken.None);
}
