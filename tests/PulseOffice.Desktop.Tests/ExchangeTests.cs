using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Charts;
using PulseOffice.Desktop.Composition;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Exchange;
using PulseOffice.Formats;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using PulseOffice.Settings;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.UI;
using PulseOffice.Sheets.Values;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class ExchangeTests : IDisposable
{
    private readonly UiThreadSession _session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-exchange-" + Guid.NewGuid().ToString("N"));

    public ExchangeTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private static byte[] Png()
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(40, 30), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext()) context.FillRectangle(Brushes.OrangeRed, new Rect(0, 0, 40, 30));
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    private static ShapeObject Star(string id) => new()
    {
        Id = id,
        Shape = ShapeKind.Star5,
        Text = "Stella",
        Runs = [new ShapeTextRun("Ste") { Bold = true }, new ShapeTextRun("lla") { Italic = true, ColorHex = "#2563EB" }],
        Style = ShapeStyle.Default with { Fill = ShapeFill.Default with { ColorHex = "#DC2626" } },
    };

    private static void AssertStarRuns(ShapeObject shape)
    {
        var runs = shape.Runs ?? [];
        Assert.Contains(runs, run => run.Bold && run.Text.Contains("Ste", StringComparison.Ordinal));
        Assert.Contains(runs, run => run.Italic && run.ColorHex == "#2563EB");
    }

    private static void FillCells(ExchangeSuite suite)
    {
        var context = suite.Ledger;
        var sheet = suite.Sheet;
        sheet[new CellAddress(0, 0)] = new Cell { Value = CellValue.Text("Mese") };
        sheet[new CellAddress(0, 1)] = new Cell { Value = CellValue.Text("Vendite") };
        sheet[new CellAddress(1, 0)] = new Cell { Value = CellValue.Text("Gen") };
        sheet[new CellAddress(1, 1)] = new Cell { Value = CellValue.Number(12) };
        sheet[new CellAddress(2, 0)] = new Cell { Value = CellValue.Text("Feb") };
        sheet[new CellAddress(2, 1)] = new Cell { Value = CellValue.Number(30), Formula = "=B2+18" };
        var bold = context.Workbook!.Styles.Intern(PulseOffice.Sheets.Styling.CellStyle.Default with { Font = PulseOffice.Sheets.Styling.CellFont.Default with { Bold = true } });
        sheet[new CellAddress(0, 0)] = sheet[new CellAddress(0, 0)] with { StyleId = bold, ExplicitStyle = true };
        context.SelectObject(null);
        context.SelectRange(new CellRange(new CellAddress(0, 0), new CellAddress(2, 1)));
        Dispatcher.UIThread.RunJobs();
    }

    public static TheoryData<string, ModuleKind, ModuleKind> ObjectCrossings()
    {
        var data = new TheoryData<string, ModuleKind, ModuleKind>();
        foreach (var kind in new[] { "chart", "shape", "image", "formula", "group" })
            foreach (var from in new[] { ModuleKind.Sheets, ModuleKind.Documents, ModuleKind.Presentations })
                foreach (var to in new[] { ModuleKind.Sheets, ModuleKind.Documents, ModuleKind.Presentations })
                    data.Add(kind, from, to);
        return data;
    }

    private static EmbeddedObject Sample(string kind, string id) => kind switch
    {
        "chart" => new ChartDefinition
        {
            Id = id,
            Type = ChartType.Column,
            SourceSheet = "Sheet1",
            SourceRange = "A1:B3",
            ChartTitle = "Vendite",
            InlineData = new ChartData(["Gen", "Feb"], [new ChartSeries("Vendite", [12, 30])]),
        },
        "shape" => Star(id),
        "image" => new RasterImageObject { Id = id, Data = Png(), MediaType = "image/png", PixelWidth = 40, PixelHeight = 30 },
        "formula" => new FormulaObject { Id = id, Expression = "a^2 + b^2 = c^2" },
        _ => new ShapeGroupObject { Id = id, Children = [new PulseOffice.Drawing.Shapes.GroupChild(Star(id + "a"), 0, 0, 0.5, 1), new PulseOffice.Drawing.Shapes.GroupChild(Star(id + "b"), 0.5, 0, 0.5, 1)] },
    };


    [Theory]
    [InlineData("chart", ModuleKind.Sheets)]
    [InlineData("shape", ModuleKind.Sheets)]
    [InlineData("group", ModuleKind.Sheets)]
    [InlineData("chart", ModuleKind.Documents)]
    [InlineData("shape", ModuleKind.Documents)]
    public Task Gli_oggetti_copiati_da_Ledger_e_Folio_portano_anche_un_PNG_per_le_altre_app(string kind, ModuleKind from) => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        suite.Place(from, Sample(kind, "figura"));
        Assert.True(await suite.Copy(from));

        var png = suite.Exchange.Last!.BytesOf(ExchangeFlavor.Png);
        Assert.NotNull(png);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png![..4]);
        using var decoded = new Bitmap(new MemoryStream(png));
        Assert.True(decoded.PixelSize.Width > 100);
    }, CancellationToken.None);

    [Theory]
    [InlineData(ModuleKind.Sheets)]
    [InlineData(ModuleKind.Documents)]
    public Task Una_casella_di_testo_di_Deck_arriva_come_oggetto(ModuleKind to) => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        var target = suite.Deck;
        var slide = target.CurrentSlide()!;
        var box = new TextElement
        {
            Id = "casella",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(8), Twip.FromCentimeters(3)),
            Text = new PulseOffice.Text.Rich.RichText(PulseOffice.Text.Rich.PlaceholderRole.Free,
                [new PulseOffice.Text.Rich.TextParagraph("Nota a margine") { FontSize = 24, Bold = true, Runs = [new PulseOffice.Text.Rich.TextRun("Nota a margine") { FontSize = 24, Bold = true, Color = 0xFFDC2626 }] }]),
        };
        target.Actions.InsertElement(slide, box);
        target.Select([box]);
        Dispatcher.UIThread.RunJobs();
        Assert.True(await suite.Copy(ModuleKind.Presentations));
        Assert.Equal(ExchangePasteMode.Objects, (await suite.Exchange.ModesAsync(to))[0]);

        Assert.True(await suite.Paste(to));
        var shape = Assert.IsType<ShapeObject>(Assert.Single(suite.Objects(to)));
        Assert.Equal("Nota a margine", shape.Text);
        Assert.Equal(24, shape.Style.Text.Size);
        Assert.True(shape.Style.Text.Bold);
        Assert.Equal("#DC2626", shape.Style.Text.ColorHex);
        Assert.False(shape.Style.Fill.IsVisible);
    }, CancellationToken.None);

    [Fact]
    public Task La_selezione_di_Deck_va_negli_appunti_anche_come_ODP_e_PPTX() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        suite.PlaceInDeck(Star("stella"));
        Assert.True(await suite.Copy(ModuleKind.Presentations));
        var package = suite.Exchange.Last!;

        foreach (var (flavor, mediaType, name) in new[]
                 {
                     (ExchangeFlavor.OpenDocument, ExchangeMediaTypes.Presentation, "selezione.odp"),
                     (ExchangeFlavor.OpenXml, ExchangeMediaTypes.Slides, "selezione.pptx"),
                 })
        {
            var representation = package.Find(flavor, mediaType);
            Assert.NotNull(representation);
            var read = suite.Service<IPresentationFormatRegistry>().Read(name, new MemoryStream(representation!.Data.ToArray()));
            Assert.True(read.Succeeded, name);
            var slide = Assert.Single(read.Value!.Slides);
            Assert.Contains("Stella", string.Join("|", slide.Elements.Select(element => element is ObjectElement { Content: ShapeObject shape } ? shape.Text : element.ToString())), StringComparison.Ordinal);
            Assert.Single(slide.Elements);
        }
    }, CancellationToken.None);

    [Theory]
    [MemberData(nameof(ObjectCrossings))]
    public Task Ogni_oggetto_passa_fra_ogni_coppia_di_moduli_restando_modificabile(string kind, ModuleKind from, ModuleKind to) => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        var original = Sample(kind, "origine");
        suite.Place(from, original);
        Assert.True(await suite.Copy(from));

        var before = suite.Objects(to).Count;
        Assert.True(await suite.Paste(to));

        var after = suite.Objects(to);
        Assert.Equal(before + 1, after.Count);
        var pasted = after.Last(item => item.GetType() == original.GetType());
        switch (pasted)
        {
            case ChartDefinition chart:
                Assert.Equal(ChartType.Column, chart.Type);
                Assert.Equal("Vendite", chart.ChartTitle);
                Assert.Equal([12d, 30d], chart.InlineData!.Series[0].Values);
                break;
            case ShapeObject shape:
                Assert.Equal(ShapeKind.Star5, shape.Shape);
                Assert.Equal("Stella", shape.Text);
                Assert.Equal("#DC2626", shape.Style.Fill.ColorHex);
                AssertStarRuns(shape);
                break;
            case RasterImageObject image:
                Assert.Equal(40, image.PixelWidth);
                Assert.Equal(((RasterImageObject)original).Data.ToArray(), image.Data.ToArray());
                break;
            case FormulaObject formula:
                Assert.Equal("a^2 + b^2 = c^2", formula.Expression);
                break;
            case ShapeGroupObject group:
                Assert.Equal(2, group.Children.Count);
                Assert.All(group.Children, child => AssertStarRuns(Assert.IsType<ShapeObject>(child.Content)));
                break;
        }

        suite.Undo(to);
        Assert.Equal(before, suite.Objects(to).Count);
    }, CancellationToken.None);

    [Theory]
    [InlineData(ModuleKind.Sheets)]
    [InlineData(ModuleKind.Documents)]
    [InlineData(ModuleKind.Presentations)]
    public Task Un_documento_in_sola_lettura_non_riceve_nulla(ModuleKind to) => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        var from = to == ModuleKind.Presentations ? ModuleKind.Documents : ModuleKind.Presentations;
        suite.Place(from, Star("sola"));
        Assert.True(await suite.Copy(from));
        var before = suite.Objects(to).Count;
        suite.Document(to).IsReadOnly = true;

        Assert.False(await suite.Paste(to));
        Assert.Equal(before, suite.Objects(to).Count);
        Assert.False(suite.Exchange.CanPaste(to));
    }, CancellationToken.None);

    [Fact]
    public Task Una_porzione_di_foglio_in_Folio_diventa_tabella_o_oggetto_foglio() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        FillCells(suite);
        Assert.True(await suite.Copy(ModuleKind.Sheets));
        Assert.Equal([ExchangePasteMode.Table, ExchangePasteMode.SheetObject, ExchangePasteMode.UnformattedText], await suite.Exchange.ModesAsync(ModuleKind.Documents));

        Assert.True(await suite.Paste(ModuleKind.Documents));
        var table = Assert.Single(suite.Folio.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.TableBlock>());
        Assert.Equal(3, table.Rows.Count);
        Assert.Contains("Vendite", Text(table), StringComparison.Ordinal);
        Assert.Contains("30", Text(table), StringComparison.Ordinal);
        suite.Undo(ModuleKind.Documents);
        Assert.Empty(suite.Folio.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.TableBlock>());

        Assert.True(await suite.Paste(ModuleKind.Documents, ExchangePasteMode.SheetObject));
        var sheet = Assert.IsType<PackagedObject>(Assert.Single(suite.FolioObjects()));
        Assert.Equal(ExchangeMediaTypes.Spreadsheet, sheet.MediaType);
        AssertCellPreview(sheet);
        var workbook = suite.Service<ISpreadsheetFormatRegistry>().Read("porzione.ods", new MemoryStream(sheet.Data.ToArray()));
        Assert.True(workbook.Succeeded);
        Assert.Equal("=B2+18", workbook.Value!.Sheets[0][new CellAddress(2, 1)].Formula);
    }, CancellationToken.None);

    [Fact]
    public Task Una_porzione_di_foglio_in_Deck_diventa_tabella_o_oggetto_foglio() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        FillCells(suite);
        Assert.True(await suite.Copy(ModuleKind.Sheets));

        Assert.True(await suite.Paste(ModuleKind.Presentations));
        var table = Assert.IsType<DataTableObject>(Assert.Single(suite.DeckObjects()));
        Assert.Equal(["Mese", "Vendite"], table.Snapshot.Columns.Select(column => column.Name));
        Assert.Equal(30d, Convert.ToDouble(table.Snapshot.Rows[1][1], System.Globalization.CultureInfo.InvariantCulture));
        suite.Undo(ModuleKind.Presentations);
        Assert.Empty(suite.DeckObjects());

        Assert.True(await suite.Paste(ModuleKind.Presentations, ExchangePasteMode.SheetObject));
        AssertCellPreview(Assert.IsType<PackagedObject>(Assert.Single(suite.DeckObjects())));
    }, CancellationToken.None);

    private static void AssertCellPreview(PackagedObject sheet)
    {
        Assert.True(sheet.HasReplacement);
        using var picture = new Bitmap(new MemoryStream(sheet.Replacement!.Value.ToArray()));
        Assert.True(picture.PixelSize.Width > picture.PixelSize.Height);
    }

    [Fact]
    public Task Una_porzione_di_foglio_in_un_altra_cartella_porta_formule_e_stili() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        FillCells(suite);
        var source = suite.Ledger.Workbook!;
        Assert.True(await suite.Copy(ModuleKind.Sheets));
        var package = suite.Exchange.Last!;
        var other = new Workbook();
        other.AddSheet("Altro");
        other.Styles.Intern(PulseOffice.Sheets.Styling.CellStyle.Default with { BackgroundHex = "#00FF00" });
        var native = Assert.IsType<PulseOffice.Sheets.UI.Exchange.LedgerCellsNative>(package.Native);
        Assert.Same(source, native.Workbook);

        suite.Ledger.SelectRange(new CellRange(new CellAddress(10, 3), new CellAddress(10, 3)));
        Assert.True(await suite.Paste(ModuleKind.Sheets));
        Assert.Equal("=E12+18", suite.Sheet[new CellAddress(12, 4)].Formula);
        Assert.True(source.Styles.Get(suite.Sheet.StyleIdAt(new CellAddress(10, 3))).Font.Bold);

        var foreign = package with { Native = null, Origin = null };
        suite.Ledger.SelectRange(new CellRange(new CellAddress(20, 0), new CellAddress(20, 0)));
        Assert.True(suite.Exchange.Receive(ModuleKind.Sheets, foreign));
        Assert.Equal("=B22+18", suite.Sheet[new CellAddress(22, 1)].Formula);
        Assert.True(source.Styles.Get(suite.Sheet.StyleIdAt(new CellAddress(20, 0))).Font.Bold);
        suite.Undo(ModuleKind.Sheets);
        Assert.True(suite.Sheet[new CellAddress(22, 1)].Value.IsEmpty);
    }, CancellationToken.None);

    [Fact]
    public Task Il_testo_di_Folio_arriva_formattato_in_Deck_e_come_celle_in_Ledger() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        var editor = suite.Folio;
        editor.Type("Rosso");
        editor.SelectAll();
        editor.Toggle(PulseOffice.Documents.UI.Editing.CharacterToggle.Bold);
        Assert.True(await suite.Copy(ModuleKind.Documents));

        suite.Deck.Select([]);
        Assert.True(await suite.Paste(ModuleKind.Presentations));
        var pasted = suite.Deck.CurrentSlide()!.Elements.OfType<TextElement>().Single(element => element.Text.PlainText.Contains("Rosso", StringComparison.Ordinal));
        Assert.Contains(pasted.Text.Paragraphs.SelectMany(paragraph => paragraph.Runs), run => run.Bold);

        suite.Ledger.SelectRange(new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)));
        Assert.True(await suite.Paste(ModuleKind.Sheets));
        Assert.Equal("Rosso", suite.Sheet[new CellAddress(0, 0)].Value.ToString());
    }, CancellationToken.None);

    [Fact]
    public Task Una_tabella_di_Folio_diventa_tabella_in_Deck_e_celle_in_Ledger() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        var editor = suite.Folio;
        editor.Insert((writer, at) => writer.InsertTable(editor.Document, editor.History, at, 2, 2), "insert-table");
        var table = editor.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.TableBlock>().Single();
        foreach (var (row, column, text) in new[] { (0, 0, "Voce"), (0, 1, "Importo"), (1, 0, "Ricavi"), (1, 1, "120") })
        {
            var paragraph = (PulseOffice.Documents.Blocks.ParagraphBlock)table.Rows[row].Cells[column].Content.Blocks[0];
            editor.Select(new PulseOffice.Documents.Editing.DocumentPosition(paragraph.Id, 0), new PulseOffice.Documents.Editing.DocumentPosition(paragraph.Id, 0));
            editor.Type(text);
        }
        editor.SelectAll();
        Assert.True(await suite.Copy(ModuleKind.Documents));

        suite.Deck.Select([]);
        Assert.Equal(ExchangePasteMode.Table, (await suite.Exchange.ModesAsync(ModuleKind.Presentations))[0]);
        Assert.True(await suite.Paste(ModuleKind.Presentations));
        var pasted = Assert.IsType<DataTableObject>(Assert.Single(suite.DeckObjects()));
        Assert.Equal(["Voce", "Importo"], pasted.Snapshot.Columns.Select(column => column.Name));
        Assert.Equal(DataValueKind.Number, pasted.Snapshot.Columns[1].Kind);
        Assert.Equal("Ricavi", pasted.Snapshot.Rows[0][0]);

        suite.Ledger.SelectRange(new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)));
        Assert.True(await suite.Paste(ModuleKind.Sheets));
        Assert.Equal("Voce", suite.Sheet[new CellAddress(0, 0)].Value.ToString());
        Assert.Equal(CellValue.Number(120), suite.Sheet[new CellAddress(1, 1)].Value);
    }, CancellationToken.None);

    [Fact]
    public Task Taglia_toglie_dalla_sorgente_in_un_passo_annullabile() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        suite.PlaceInDeck(Star("taglio"));
        var count = suite.DeckObjects().Count;
        Activate(suite, ModuleKind.Presentations);
        Assert.NotNull(await suite.Exchange.CutAsync(ModuleKind.Presentations));
        Assert.Equal(count - 1, suite.DeckObjects().Count);
        Assert.True(await suite.Paste(ModuleKind.Sheets));
        Assert.Contains(suite.LedgerObjects(), item => item is ShapeObject { Text: "Stella" });
        suite.Undo(ModuleKind.Presentations);
        Assert.Equal(count, suite.DeckObjects().Count);
    }, CancellationToken.None);

    [Fact]
    public Task I_documenti_risultanti_attraversano_ODF_e_OOXML() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        suite.PlaceInLedger(Sample("chart", "grafico"));
        Assert.True(await suite.Copy(ModuleKind.Sheets));
        Assert.True(await suite.Paste(ModuleKind.Documents));
        Assert.True(await suite.Paste(ModuleKind.Presentations));
        suite.PlaceInDeck(Star("forma"));
        Assert.True(await suite.Copy(ModuleKind.Presentations));
        Assert.True(await suite.Paste(ModuleKind.Documents));
        Assert.True(await suite.Paste(ModuleKind.Sheets));
        FillCells(suite);
        Assert.True(await suite.Copy(ModuleKind.Sheets));
        Assert.True(await suite.Paste(ModuleKind.Presentations));
        Assert.True(await suite.Paste(ModuleKind.Documents, ExchangePasteMode.SheetObject));

        var texts = suite.Service<ITextDocumentFormatRegistry>();
        foreach (var name in new[] { "risultato.odt", "risultato.docx" })
        {
            var document = RoundTrip(suite.Folio.Document, (model, stream) => texts.WriterFor(name)!.Write(model, stream), stream => texts.Read(name, stream).Value!);
            var kinds = document.Objects.All.Select(item => item.Content).OfType<EmbeddedContent>().Select(content => content.Value.GetType()).ToList();
            Assert.Contains(typeof(ChartDefinition), kinds);
            Assert.Contains(typeof(ShapeObject), kinds);
            Assert.Contains(typeof(PackagedObject), kinds);
        }

        var decks = suite.Service<IPresentationFormatRegistry>();
        foreach (var name in new[] { "risultato.odp", "risultato.pptx" })
        {
            var deck = RoundTrip(suite.Deck.Deck, (model, stream) => decks.WriterFor(name)!.Write(model, stream), stream => decks.Read(name, stream).Value!);
            var kinds = deck.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content.GetType()).ToList();
            Assert.Contains(typeof(ChartDefinition), kinds);
            Assert.Contains(typeof(ShapeObject), kinds);
            Assert.Contains(typeof(DataTableObject), kinds);
        }

        var sheets = suite.Service<ISpreadsheetFormatRegistry>();
        foreach (var name in new[] { "risultato.ods", "risultato.xlsx" })
        {
            var workbook = RoundTrip(suite.Ledger.Workbook!, (model, stream) => sheets.WriterFor(name)!.Write(model, stream), stream => sheets.Read(name, stream).Value!);
            Assert.Contains(workbook.Sheets[0].Objects, placed => placed.Content is ShapeObject);
        }
    }, CancellationToken.None);

    [Fact]
    public Task Lasciare_cadere_un_pacchetto_lo_incolla_nel_documento_attivo() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        suite.PlaceInDeck(Star("trascinata"));
        var package = suite.Exchange.Capture(ModuleKind.Presentations)!;
        var transfer = suite.Service<PulseOffice.UI.Controls.Services.IExchangeDataTransfers>().Write(suite.Exchange.Publishable(package));
        suite.Activate(ModuleKind.Documents);

        Assert.True(await suite.Service<IExchangeDragDrop>().DropAsync(transfer));
        Assert.Contains(suite.FolioObjects(), item => item is ShapeObject { Text: "Stella" });
        Assert.True(suite.Service<IExchangeDragDrop>().IsDragGesture(Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Alt));
    }, CancellationToken.None);

    private static void Activate(ExchangeSuite suite, ModuleKind module) => suite.Activate(module);

    private static T RoundTrip<T>(T model, Func<T, Stream, PulseOffice.Foundation.Diagnostics.OperationResult<bool>> write, Func<Stream, T> read)
    {
        using var stream = new MemoryStream();
        var written = write(model, stream);
        Assert.True(written.Succeeded, string.Join("; ", written.Diagnostics.Select(item => item.Message)));
        stream.Position = 0;
        return read(stream);
    }

    private static string Text(PulseOffice.Documents.Blocks.TableBlock table) =>
        string.Join("|", table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Content.Blocks).OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().Select(paragraph => paragraph.Text));
}
