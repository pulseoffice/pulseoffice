using PulseOffice.Testing;
using Avalonia.Threading;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Settings;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class PasteFidelityMatrixTests : IDisposable
{
    private static readonly byte[] Pixel = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    private static readonly string[] FolioConstructs =
    [
        "field", "field-range", "insertion", "deletion", "nested-deletion", "format-change", "content-control", "footnote",
        "endnote", "bookmark", "comment", "index-entry", "cross-reference", "picture-bullet", "vertical-cell", "cell-padding",
    ];

    private static readonly string[] TrackedChanges = ["insertion", "deletion", "nested-deletion", "format-change"];

    private static readonly IReadOnlyDictionary<string, string[]> FolioLosses = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["native-same"] = [],
        ["native-other"] = [],
        ["odf"] = [],
        ["ooxml"] = [],
        ["html"] = [.. TrackedChanges, "field-range", "content-control", "comment", "index-entry", "cross-reference", "picture-bullet", "vertical-cell", "cell-padding", "bookmark", "field"],
        ["rtf"] = [.. TrackedChanges, "field-range", "content-control", "comment", "index-entry", "cross-reference", "picture-bullet", "vertical-cell", "cell-padding", "field"],
    };

    private readonly UiThreadSession _session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-paste-fidelity-" + Guid.NewGuid().ToString("N"));

    public PasteFidelityMatrixTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private static TextRun Field(string shown, TextFieldKind kind, string? argument = null, FieldDetail? detail = null) =>
        new(shown) { Field = kind, FieldArgument = argument, FieldDetail = detail };

    private static ParagraphBlock Paragraph(string id, params TextRun[] runs) =>
        new(id, new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.TextBody }.WithRuns(runs));

    private static void Build(TextDocument document)
    {
        var stamp = new DateTimeOffset(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);
        document.Styles.List.Add(new ListStyle("ElencoFigurato", [new ListLevel(NumberFormat.Bullet) { Picture = new ListBulletPicture(Pixel, "image/png", 6, 6) }]));
        var fields = Paragraph("pf", new TextRun("Data "), Field("01/01/2000", TextFieldKind.Date, detail: new FieldDetail { Fixed = true, Value = "2000-01-01" }), new TextRun(" codice 2026 fine"));
        fields.Marks =
        [
            new BookmarkMark(0, "segno", MarkEdge.Start),
            new CommentMark(0, "c1", MarkEdge.Start),
            new BookmarkMark(4, "segno", MarkEdge.End),
            new CommentMark(4, "c1", MarkEdge.End),
            new IndexEntryMark(5, "voce", MarkEdge.Point),
            new FieldCodeMark(23, "field1", FieldCodePart.Begin, " GOTOBUTTON segno Vai "),
            new FieldCodeMark(23, "field1", FieldCodePart.Separate, string.Empty),
            new FieldCodeMark(27, "field1", FieldCodePart.End, string.Empty),
            new NoteMark(32, "fn1"),
            new NoteMark(32, "en1"),
        ];
        var changes = Paragraph("pc", new TextRun("inserito controllo formato") { Bold = true });
        changes.Marks =
        [
            new ChangeMark(0, "ins1", MarkEdge.Start),
            new ChangeMark(4, "del2", MarkEdge.Point),
            new ChangeMark(8, "ins1", MarkEdge.End),
            new ChangeMark(8, "del1", MarkEdge.Point),
            new ContentControlMark(9, "cc1", MarkEdge.Start) { Control = new ContentControl { Kind = ContentControlKind.PlainText, Tag = "etichetta", Title = "Titolo" } },
            new ContentControlMark(18, "cc1", MarkEdge.End),
            new ChangeMark(19, "fmt1", MarkEdge.Start),
            new ChangeMark(26, "fmt1", MarkEdge.End),
        ];
        var reference = new ParagraphBlock("pr", new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.TextBody, ListStyleName = "ElencoFigurato" }
            .WithRuns([new TextRun("Vedi "), Field("Data", TextFieldKind.Bookmark, "segno")]));
        var table = new TableBlock("tb", [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("pt", "cella")]))
        {
            TextDirection = CellTextDirection.TopToBottom,
            Padding = new CellPadding(1, 2, 3, 4),
        }])], [Twip.FromCentimeters(4)]);
        document.Notes.Add(new Note("fn1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("fn1p", "Piede.", DefaultStyleNames.Footnote)])));
        document.Notes.Add(new Note("en1", NoteKind.Endnote, new BlockContainer([new ParagraphBlock("en1p", "Chiusura.", DefaultStyleNames.Endnote)])));
        document.Comments.Add(new Comment("c1", "Anna", stamp, new BlockContainer([new ParagraphBlock("c1p", "Commento")])));
        document.Changes.Add(new Insertion("ins1", "Bruno", stamp));
        document.Changes.Add(new Deletion("del1", "Carla", stamp, new BlockContainer([new ParagraphBlock("d1p", "via")])));
        document.Changes.Add(new Deletion("del2", "Carla", stamp, new BlockContainer([new ParagraphBlock("d2p", "xx")])) { WithinInsertion = "ins1" });
        document.Changes.Add(new FormatChange("fmt1", "Dario", stamp, [new TextRun("formato")]));
        document.Body.Replace([fields, changes, reference, table, new ParagraphBlock("pe", "ultimo")]);
        document.Raise();
    }

    private static IReadOnlyDictionary<string, int> Census(ExchangeSuite suite, TextDocument document)
    {
        var paragraphs = suite.Service<IStoryCatalog>().Paragraphs(document).ToList();
        var marks = paragraphs.SelectMany(paragraph => paragraph.Marks).ToList();
        var runs = paragraphs.SelectMany(paragraph => paragraph.Content.Runs).ToList();
        var bookmarks = marks.OfType<BookmarkMark>().Select(mark => mark.Name).ToHashSet(StringComparer.Ordinal);
        var cells = document.Body.Blocks.OfType<TableBlock>().SelectMany(table => table.Rows).SelectMany(row => row.Cells).ToList();
        int Changes<T>(MarkEdge edge, Func<T, bool> accept) where T : TrackedChange =>
            marks.OfType<ChangeMark>().Count(mark => mark.Edge == edge && document.Changes.Find(mark.ChangeId) is T change && accept(change));
        int Notes(NoteKind kind) => marks.OfType<NoteMark>().Count(mark => document.Notes.Find(mark.NoteId)?.Kind == kind);
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["field"] = runs.Count(run => run.Field == TextFieldKind.Date && run.FieldDetail?.Fixed == true),
            ["field-range"] = marks.OfType<FieldCodeMark>().Count(mark => mark.Part == FieldCodePart.Begin && mark.Code.Contains("GOTOBUTTON", StringComparison.Ordinal))
                + runs.Count(run => run.Field == TextFieldKind.Preserved && run.FieldArgument?.Contains("GOTOBUTTON", StringComparison.Ordinal) == true),
            ["insertion"] = Changes<Insertion>(MarkEdge.Start, change => change.Author == "Bruno"),
            ["deletion"] = Changes<Deletion>(MarkEdge.Point, change => change.WithinInsertion is null && change.Author == "Carla"),
            ["nested-deletion"] = Changes<Deletion>(MarkEdge.Point, change => change.WithinInsertion is { } within && document.Changes.Find(within) is Insertion),
            ["format-change"] = Changes<FormatChange>(MarkEdge.Start, change => change.Author == "Dario"),
            ["content-control"] = marks.OfType<ContentControlMark>().Count(mark => mark.Edge == MarkEdge.Start && mark.Control?.Tag == "etichetta"),
            ["footnote"] = Notes(NoteKind.Footnote),
            ["endnote"] = Notes(NoteKind.Endnote),
            ["bookmark"] = marks.OfType<BookmarkMark>().Count(mark => mark.Edge == MarkEdge.Start),
            ["comment"] = marks.OfType<CommentMark>().Count(mark => mark.Edge == MarkEdge.Start && document.Comments.Find(mark.CommentId)?.Author == "Anna"),
            ["index-entry"] = marks.OfType<IndexEntryMark>().Count(mark => mark.Entry == "voce"),
            ["cross-reference"] = runs.Count(run => run.Field == TextFieldKind.Bookmark && run.FieldArgument is { } name && bookmarks.Contains(name)),
            ["picture-bullet"] = paragraphs.Count(paragraph => paragraph.Content.ListStyleName is { } name && document.Styles.List.Find(name)?.Level(0).Picture is not null),
            ["vertical-cell"] = cells.Count(cell => cell.TextDirection == CellTextDirection.TopToBottom),
            ["cell-padding"] = cells.Count(cell => cell.Padding == new CellPadding(1, 2, 3, 4)),
        };
    }

    private static void CaretAtEnd(PulseOffice.Documents.UI.Editing.FolioEditor editor)
    {
        var last = editor.Document.Body.Blocks.OfType<ParagraphBlock>().Last();
        editor.PlaceCaret(new DocumentPosition(last.Id, last.Length), extend: false);
        Dispatcher.UIThread.RunJobs();
    }

    private static ExchangePackage Alone(ExchangePackage package, ExchangeFlavor flavor, ModuleKind origin) => package with
    {
        Origin = origin,
        Native = null,
        Content = null,
        Representations = [.. package.Representations.Where(item => item.Flavor == flavor)],
    };

    [Fact]
    public Task Ogni_costrutto_di_Folio_sopravvive_a_ogni_percorso_di_incolla_o_la_perdita_e_dichiarata() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        var source = suite.Folio;
        Build(source.Document);
        source.Reflow();
        Assert.All(FolioConstructs, construct => Assert.Equal(1, Census(suite, source.Document)[construct]));
        source.SelectAll();
        Assert.True(await suite.Copy(ModuleKind.Documents));
        var package = suite.Exchange.Last!;

        var report = new List<string>();
        void Check(string path, TextDocument document, int expected)
        {
            var census = Census(suite, document);
            foreach (var construct in FolioConstructs)
            {
                var wanted = FolioLosses[path].Contains(construct) ? 0 : expected;
                if (census[construct] != wanted) report.Add($"{path}/{construct}: {census[construct]} instead of {wanted}");
            }
        }

        CaretAtEnd(source);
        Assert.True(suite.Exchange.Receive(ModuleKind.Documents, package, ExchangePasteMode.Native));
        Check("native-same", source.Document, 2);

        foreach (var (path, flavor, origin, mode) in new[]
        {
            ("native-other", (ExchangeFlavor?)null, ModuleKind.Documents, ExchangePasteMode.Native),
            ("odf", ExchangeFlavor.OpenDocument, ModuleKind.Documents, ExchangePasteMode.Native),
            ("ooxml", ExchangeFlavor.OpenXml, ModuleKind.Sheets, ExchangePasteMode.FormattedText),
            ("html", ExchangeFlavor.Html, ModuleKind.Sheets, ExchangePasteMode.FormattedText),
            ("rtf", ExchangeFlavor.Rtf, ModuleKind.Sheets, ExchangePasteMode.FormattedText),
        })
        {
            var target = suite.FolioOf(await suite.OpenAnother(ModuleKind.Documents));
            var pasted = flavor is { } alone ? Alone(package, alone, origin) : package;
            if (!suite.Exchange.Receive(ModuleKind.Documents, pasted, mode)) report.Add($"{path}: not pasted");
            Dispatcher.UIThread.RunJobs();
            Check(path, target.Document, 1);
        }

        Assert.True(report.Count == 0, string.Join(Environment.NewLine, report));
    }, CancellationToken.None);

    private static readonly string[] DeckConstructs = ["animation", "action", "sound"];

    private static readonly IReadOnlyDictionary<string, string[]> DeckLosses = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["native-same"] = [],
        ["native-other"] = [],
        ["odf"] = [],
        ["ooxml"] = [],
        ["html"] = DeckConstructs,
        ["rtf"] = DeckConstructs,
    };

    private static IReadOnlyDictionary<string, int> DeckCensus(PulseOffice.Presentations.Presentation deck)
    {
        var animations = deck.Slides.SelectMany(slide => slide.Animations.Where(effect => slide.Find(effect.TargetId) is not null)).Where(effect => effect.Preset == "fly-in").ToList();
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["animation"] = animations.Count,
            ["action"] = deck.Slides.SelectMany(slide => slide.Interactions.Where(action => slide.Find(action.TargetId) is not null)).Count(action => action.Kind == PulseOffice.Presentations.InteractionKind.Hyperlink && action.Reference == "https://example.org"),
            ["sound"] = animations.Count(effect => effect.SoundPath is { } path && deck.Sounds.Find(path)?.Data.Length == Chime.Length),
        };
    }

    private static readonly byte[] Chime = [.. "RIFF"u8, 36, 0, 0, 0, .. "WAVEfmt "u8, 16, 0, 0, 0, 1, 0, 1, 0, 68, 172, 0, 0, 136, 88, 1, 0, 2, 0, 16, 0, .. "data"u8, 0, 0, 0, 0];

    [Fact]
    public Task Animazioni_azioni_e_suoni_di_Deck_sopravvivono_a_ogni_percorso_di_incolla_o_la_perdita_e_dichiarata() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        var source = suite.Deck;
        var slide = source.CurrentSlide()!;
        var box = new PulseOffice.Presentations.TextElement
        {
            Id = "casella",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(8), Twip.FromCentimeters(3)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("Animata") { Runs = [new TextRun("Animata") { Bold = true }] }]),
        };
        source.Actions.InsertElement(slide, box);
        source.Deck.Sounds.Add(new PulseOffice.Presentations.EmbeddedSound("Media/chime.wav", "audio/wav", Chime));
        slide.AddAnimation(new PulseOffice.Presentations.AnimationEffect { Id = slide.NextAnimationId(), TargetId = box.Id, Preset = "fly-in", SoundPath = "Media/chime.wav" });
        slide.SetInteraction(new PulseOffice.Presentations.InteractiveAction { TargetId = box.Id, Kind = PulseOffice.Presentations.InteractionKind.Hyperlink, Reference = "https://example.org" });
        source.Select([box]);
        Dispatcher.UIThread.RunJobs();
        Assert.All(DeckConstructs, construct => Assert.Equal(1, DeckCensus(source.Deck)[construct]));
        Assert.True(await suite.Copy(ModuleKind.Presentations));
        var package = suite.Exchange.Last!;

        var report = new List<string>();
        void Check(string path, PulseOffice.Presentations.Presentation deck, int expected)
        {
            var census = DeckCensus(deck);
            foreach (var construct in DeckConstructs)
            {
                var wanted = DeckLosses[path].Contains(construct) ? 0 : expected;
                if (census[construct] != wanted) report.Add($"{path}/{construct}: {census[construct]} instead of {wanted}");
            }
        }

        Assert.True(suite.Exchange.Receive(ModuleKind.Presentations, package, ExchangePasteMode.Native));
        Check("native-same", source.Deck, 2);

        var other = suite.DeckOf(await suite.OpenAnother(ModuleKind.Presentations));
        Assert.True(suite.Exchange.Receive(ModuleKind.Presentations, package, ExchangePasteMode.Native));
        Check("native-other", other.Deck, 1);

        var formats = suite.Service<PulseOffice.Formats.IPresentationFormatRegistry>();
        foreach (var (path, flavor, extension) in new[] { ("odf", ExchangeFlavor.OpenDocument, ".odp"), ("ooxml", ExchangeFlavor.OpenXml, ".pptx") })
        {
            var bytes = package.BytesOf(flavor);
            Assert.NotNull(bytes);
            using var stream = new MemoryStream(bytes);
            var read = formats.ReaderFor("selection" + extension)!.Read(stream);
            Assert.True(read.Succeeded, path);
            Check(path, read.Value!, 1);
        }

        foreach (var (path, flavor) in new[] { ("html", ExchangeFlavor.Html), ("rtf", ExchangeFlavor.Rtf) })
        {
            var target = suite.DeckOf(await suite.OpenAnother(ModuleKind.Presentations));
            if (!suite.Exchange.Receive(ModuleKind.Presentations, Alone(package, flavor, ModuleKind.Sheets), ExchangePasteMode.FormattedText)) report.Add($"{path}: not pasted");
            Dispatcher.UIThread.RunJobs();
            Check(path, target.Deck, 1);
        }

        Assert.True(report.Count == 0, string.Join(Environment.NewLine, report));
    }, CancellationToken.None);
}
