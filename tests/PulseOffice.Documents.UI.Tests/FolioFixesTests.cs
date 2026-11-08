using PulseOffice.UI.Controls.Fonts;
using System.Text;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Fonts;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Documents.UI.Shell;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Printing;
using PulseOffice.Shell;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using Xunit;
using LayoutLineBox = PulseOffice.Documents.Layout.Tree.LineBox;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioFixesTests
{
    [Fact]
    public Task LaVistaPrendeIlFuocoAllApertura() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(focus: false);
        Dispatcher.UIThread.RunJobs();

        Assert.True(scene.View.InputSink.IsFocused);
        scene.Type("Subito");
        Assert.Equal(["Subito"], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task LaPaginaECentrataDalPrimoPassaggio() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var slot = scene.View.Canvas.Slots[0];
        var width = scene.View.Canvas.Bounds.Width;

        Assert.True(width > slot.Width);
        Assert.Equal((width - slot.Width) / 2, slot.X, 1);
    }, CancellationToken.None);

    [Fact]
    public Task LaDomandaDiChiusuraNonOffreSalvaSeNessunoSaSalvare() => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene();
        var open = new OpenDocument(ModuleKind.Documents, "Folio1", new Border(), scene.History);
        scene.Type("Testo");
        var dialogs = new CapturingDialogs(PulseOffice.Shell.CloseDecision.Discard);
        var guard = new UnsavedChangesGuard(new DocumentWorkspace(), [], dialogs, new Texts());

        var closed = await guard.ConfirmCloseAsync([open], CancellationToken.None);

        Assert.True(closed);
        Assert.DoesNotContain("close.save", dialogs.Buttons);
        Assert.Contains("close.discard", dialogs.Buttons);
        Assert.Contains("close.cancel", dialogs.Buttons);
    });

    [Fact]
    public Task ConUnFolioNonSalvabileGliAltriDocumentiSiSalvanoAncora() => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene();
        var folio = new OpenDocument(ModuleKind.Documents, "Folio1", new Border(), scene.History);
        scene.Type("Testo");
        Assert.True(folio.IsModified);
        var sheetHistory = new UndoStack();
        var sheet = new OpenDocument(ModuleKind.Sheets, "Ledger1", new Border(), sheetHistory);
        sheetHistory.Push(new DelegateAction("x", () => { }, () => { }));
        var saver = new RecordingSaver();
        var dialogs = new CapturingDialogs(PulseOffice.Shell.CloseDecision.Save);
        var guard = new UnsavedChangesGuard(new DocumentWorkspace(), [saver], dialogs, new Texts());

        var closed = await guard.ConfirmCloseAsync([folio, sheet], CancellationToken.None);

        Assert.Contains("close.save", dialogs.Buttons);
        Assert.True(closed);
        Assert.Equal([sheet], saver.Saved);
    });

    private sealed class RecordingSaver : IDocumentSaver
    {
        public List<OpenDocument> Saved { get; } = [];

        public bool CanSave(OpenDocument document) => document.Module == ModuleKind.Sheets;

        public Task<bool> SaveAsync(OpenDocument document, CancellationToken ct = default)
        {
            Saved.Add(document);
            document.MarkSaved("/tmp/x.ods", "x.ods");
            return Task.FromResult(true);
        }
    }

    [Fact]
    public void ISostitutiDiLiberationSonoMetricamenteCompatibili()
    {
        var substitutes = new MetricCompatibleSubstitutes();

        Assert.Contains("Times New Roman", substitutes.For("Liberation Serif"));
        Assert.Contains("Arial", substitutes.For("liberation sans"));
        Assert.Contains("Courier New", substitutes.For("Liberation Mono"));
        Assert.Empty(substitutes.For("Pacifico"));
    }

    [Fact]
    public Task CursoreEDisegnoUsanoLaStessaGeometriaDiRiga() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Geometria condivisa");
        var offsets = scene.Services.GetRequiredService<PulseOffice.Documents.Layout.Queries.ILineOffsets>();
        var queries = scene.Services.GetRequiredService<PulseOffice.Documents.Layout.Queries.ILayoutQueries>();
        var layout = scene.Editor.Layout;
        var line = layout.Pages[0].AllLines.First();

        for (var offset = 0; offset <= 19; offset++)
        {
            var caret = queries.Caret(layout, new DocumentPosition(line.ParagraphId, offset))!.Value;
            Assert.Equal(offsets.XOf(line, offset), caret.X, 6);
            Assert.Equal(offset, offsets.OffsetAt(line, offsets.XOf(line, offset) + 0.01));
        }
    }, CancellationToken.None);

    [Fact]
    public Task IlClicNonCadeMaiDentroUnGrafema() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        const string text = "ae\u0301 \U0001F468\u200D\U0001F469\u200D\U0001F467 \U0001F1EE\U0001F1F9 \U0001F44D\U0001F3FD z";
        scene.Type(text);
        var offsets = scene.Services.GetRequiredService<PulseOffice.Documents.Layout.Queries.ILineOffsets>();
        var line = scene.Editor.Layout.Pages[0].AllLines.First();
        var boundaries = new HashSet<int>();
        for (var at = 0; at < text.Length; at += System.Globalization.StringInfo.GetNextTextElementLength(text, at)) boundaries.Add(at);
        boundaries.Add(text.Length);
        var right = offsets.XOf(line, text.Length) + 5;
        for (var x = line.Left - 2; x <= right; x += 0.25)
            Assert.Contains(offsets.OffsetAt(line, x), boundaries);
    }, CancellationToken.None);

    [Fact]
    public Task FrecciaGiuNellaTabellaRestaNellaColonna() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document =>
        {
            TableCell Cell(params string[] paragraphs) =>
                new(new BlockContainer(paragraphs.Select(text => (Block)new ParagraphBlock(document.NextId("p"), text))));
            var table = new TableBlock(document.NextId("t"),
                [
                    new TableRow([Cell("A1"), Cell("B1 uno", "B1 due", "B1 tre")]),
                    new TableRow([Cell("A2"), Cell("B2")]),
                ],
                [Twip.FromCentimeters(5), Twip.FromCentimeters(5)]);
            document.Body.Replace([new ParagraphBlock(document.NextId("p"), "Prima"), table, new ParagraphBlock(document.NextId("p"), "Dopo")]);
        });
        var table = scene.Document.Body.Blocks.OfType<TableBlock>().Single();
        var a1 = (ParagraphBlock)table.Cell(0, 0).Content[0];
        var a2 = (ParagraphBlock)table.Cell(1, 0).Content[0];
        var b1 = table.Cell(0, 1).Content.Blocks.OfType<ParagraphBlock>().ToList();

        scene.Editor.PlaceCaret(new DocumentPosition(a1.Id, 1), extend: false);
        scene.Editor.Move(CaretMotion.Down, extend: false);
        Assert.Equal(a2.Id, scene.Editor.Caret.ParagraphId);

        scene.Editor.PlaceCaret(new DocumentPosition(b1[0].Id, 1), extend: false);
        scene.Editor.Move(CaretMotion.Down, extend: false);
        Assert.Equal(b1[1].Id, scene.Editor.Caret.ParagraphId);

        scene.Editor.Move(CaretMotion.Up, extend: false);
        Assert.Equal(b1[0].Id, scene.Editor.Caret.ParagraphId);
    }, CancellationToken.None);

    [Fact]
    public Task LaComposizioneImeEntraNellaRigaImpaginata() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("ab");
        scene.Press(Avalonia.Input.Key.Left);

        scene.View.SetPreedit("かな", 1);
        Dispatcher.UIThread.RunJobs();

        var line = scene.Editor.DisplayLayout.Pages[0].AllLines.First();
        var text = string.Concat(line.Line.Runs.Select(run => run.Text));
        Assert.Contains("aかなb", text);
        Assert.Contains(line.Line.Runs, run => run.Text.Contains('か') && run.Style?.Underline == true);
        Assert.Equal(["ab"], scene.Paragraphs);
        Assert.Equal(2, scene.Editor.DisplayCaret.Offset);

        scene.View.SetPreedit(null, null);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(scene.Editor.DisplayLayout.Pages[0].AllLines, candidate => candidate.Line.Runs.Any(run => run.Text.Contains('か')));
    }, CancellationToken.None);

    [Fact]
    public Task LIstantaneaDiRipristinoConservaTuttoIlModello() => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene();
        var document = Rich(scene.Document);
        var codec = scene.Services.GetRequiredService<IFolioSnapshotCodec>();

        var before = Describe(document);
        using var first = new MemoryStream();
        await codec.WriteAsync(document, first, CancellationToken.None);
        first.Position = 0;
        var restored = await codec.ReadAsync(first, CancellationToken.None);

        Assert.NotNull(restored);
        Assert.Equal(before, Describe(restored));
        using var second = new MemoryStream();
        await codec.WriteAsync(restored, second, CancellationToken.None);
        Assert.Equal(Encoding.UTF8.GetString(first.ToArray()), Encoding.UTF8.GetString(second.ToArray()));
        Assert.NotEqual("p1", restored.NextId("p"));
        Assert.DoesNotContain(document.Body.Blocks.Select(block => block.Id), id => id == restored.NextId("p"));
    });

    [Fact]
    public Task UnIstantaneaDiAltroFormatoVieneRifiutata() => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene();
        var codec = scene.Services.GetRequiredService<IFolioSnapshotCodec>();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"format\":\"altro\",\"version\":2}"));

        Assert.Null(await codec.ReadAsync(stream, CancellationToken.None));
    });

    [Fact]
    public Task LaStampaDisegnaLePagineDelLayout() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Ciao");
        var open = new OpenDocument(ModuleKind.Documents, "Folio1", scene.View, scene.History) { Model = scene.Document };
        var operations = scene.Services.GetServices<IDocumentOperations>().Single(candidate => candidate.Module == ModuleKind.Documents);

        var printable = operations.BuildPrintable(open);

        Assert.NotNull(printable);
        Assert.Equal(scene.Editor.Layout.Pages.Count, printable.PageCount);
        Assert.InRange(printable.Setup.PageWidth.Value - Twip.FromPoints(scene.Editor.Layout.Pages[0].Width).Value, -1, 1);
        var canvas = new RecordingCanvas(new DocumentSize(printable.Setup.PageWidth, printable.Setup.PageHeight));
        printable.PaintPage(0, canvas);
        Assert.Equal("Ciao", string.Concat(canvas.Texts));
        var line = scene.Editor.Layout.Pages[0].AllLines.First();
        Assert.InRange(canvas.Lefts[0].Value - Twip.FromPoints(line.Left + line.Line.Runs[0].X).Value, -2, 2);
    }, CancellationToken.None);

    [Fact]
    public Task SchermoEStampaPassanoDalloStessoDisegnatore() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Uno");
        var renderer = scene.Services.GetRequiredService<IPageRenderer>();
        var recording = new RecordingSurface();

        renderer.Paint(recording, scene.Editor.Layout.Pages[0]);

        Assert.Equal("Uno", string.Concat(recording.Texts));
    }, CancellationToken.None);

    internal static TextDocument Rich(TextDocument document)
    {
        document.Styles.Character.Add(new CharacterStyle("Enfasi") { Character = new CharacterProperties { Italic = true, Color = 0xFF2563EB } });
        var page = document.Styles.Page.Find(DefaultStyleNames.Standard)!;
        page.Header.Enabled = true;
        page.Header.Default.Replace([new ParagraphBlock(document.NextId("p"), "Intestazione")]);
        page.Footer.Enabled = true;
        page.Footer.First = new BlockContainer([new ParagraphBlock(document.NextId("p"), "Piè primo")]);
        var runs = new List<TextRun> { new("Grassetto ") { Bold = true }, new("e colore") { Color = 0xFFDC2626, Font = "Liberation Sans", FontSize = 14 } };
        var first = new ParagraphBlock(document.NextId("p"), new TextParagraph("Grassetto e colore") { Runs = runs, StyleName = "Heading 1", Align = TextAlign.Center })
        {
            Marks = [new BookmarkMark(0, "inizio", MarkEdge.Point), new NoteMark(4, "n1"), new CommentMark(0, "c1", MarkEdge.Start), new CommentMark(9, "c1", MarkEdge.End), new ObjectMark(2, "o2")],
        };
        var table = new TableBlock(document.NextId("t"),
            [
                new TableRow([new TableCell(new BlockContainer([new ParagraphBlock(document.NextId("p"), "A1")])) { ColumnSpan = 2, Background = 0xFFEEEEEE }, new TableCell(new BlockContainer()) { Covered = true }]) { Height = Twip.FromCentimeters(1) },
                new TableRow([new TableCell(new BlockContainer([new ParagraphBlock(document.NextId("p"), "A2")])) { VerticalAlign = PulseOffice.Documents.Blocks.CellVerticalAlign.Bottom }, new TableCell(new BlockContainer([new ParagraphBlock(document.NextId("p"), "B2")]))]),
            ],
            [Twip.FromCentimeters(4), Twip.FromCentimeters(6)]) { Name = "Tabella1", HeaderRows = 1 };
        var section = new SectionBlock(document.NextId("s"), "Sezione1", new BlockContainer([new ParagraphBlock(document.NextId("p"), "Dentro")]))
        {
            Columns = new SectionColumns(2, Twip.FromCentimeters(1)) { Separator = true },
        };
        document.Body.Replace([first, table, section]);
        document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock(document.NextId("p"), "Nota")])) { Citation = "*" });
        document.Comments.Add(new Comment("c1", "Ada", new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.FromHours(2)), new BlockContainer([new ParagraphBlock(document.NextId("p"), "Da rivedere")])) { Initials = "AL" });
        document.Comments.Add(new Comment("c2", "Bruno", DateTimeOffset.UnixEpoch, new BlockContainer()) { ParentId = "c1", Resolved = true });
        document.Objects.Add(new AnchoredObject("o1", new ParagraphAnchor(first.Id), new TextBoxContent(new BlockContainer([new ParagraphBlock(document.NextId("p"), "Cornice")])) { AutoGrowHeight = false },
            new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(3))) { Wrap = WrapMode.Left, ZOrder = 3, Name = "Cornice1", RotationDegrees = 15 });
        document.Objects.Add(new AnchoredObject("o2", new AsCharacterAnchor(first.Id), new EmbeddedContent(new FormulaObject { Id = "f1", Expression = "a^2" }),
            new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(1))));
        document.Changes.Add(new Insertion("ch1", "Ada", DateTimeOffset.UnixEpoch) { Comment = "nuovo" });
        document.Changes.Add(new Deletion("ch2", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock(document.NextId("p"), "Tolto")])));
        document.Changes.Add(new FormatChange("ch3", "Bruno", DateTimeOffset.UnixEpoch, [new TextRun("x") { Italic = true }]));
        document.Changes.IsRecording = true;
        document.Variables["cliente"] = "Rossi";
        document.Properties.Title = "Relazione";
        document.Properties.Keywords.Add("prova");
        document.Properties.Custom["Reparto"] = "Vendite";
        return document;
    }

    private static string Describe(TextDocument document)
    {
        var text = new StringBuilder();
        void Container(string name, BlockContainer? container)
        {
            text.Append('[').Append(name).Append(':');
            foreach (var block in container?.Blocks ?? []) Block(block);
            text.Append(']');
        }
        void Block(Block block)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    text.Append("P(").Append(paragraph.Id).Append('|').Append(paragraph.Content.StyleName).Append('|').Append(paragraph.Content.Align)
                        .Append('|').Append(string.Join(';', paragraph.Content.Runs)).Append('|').Append(string.Join(';', paragraph.Marks)).Append(')');
                    break;
                case TableBlock table:
                    text.Append("T(").Append(table.Id).Append('|').Append(table.Name).Append('|').Append(table.HeaderRows).Append('|').Append(string.Join(',', table.Columns));
                    foreach (var row in table.Rows)
                    {
                        text.Append("{R").Append(row.Height);
                        foreach (var cell in row.Cells)
                        {
                            text.Append("{C").Append(cell.ColumnSpan).Append(cell.Covered).Append(cell.Background).Append(cell.VerticalAlign);
                            Container("c", cell.Content);
                            text.Append('}');
                        }
                        text.Append('}');
                    }
                    text.Append(')');
                    break;
                case SectionBlock section:
                    text.Append("S(").Append(section.Id).Append(section.Name).Append(section.Columns);
                    Container("s", section.Content);
                    text.Append(')');
                    break;
            }
        }
        Container("body", document.Body);
        foreach (var note in document.Notes.All) { text.Append("N").Append(note.Id).Append(note.Kind).Append(note.Citation); Container("n", note.Content); }
        foreach (var comment in document.Comments.All) { text.Append("K").Append(comment.Id).Append(comment.Author).Append(comment.Initials).Append(comment.Date.ToString("O")).Append(comment.ParentId).Append(comment.Resolved); Container("k", comment.Content); }
        foreach (var item in document.Objects.All)
        {
            text.Append("O").Append(item.Id).Append(item.Anchor).Append(item.Size).Append(item.Wrap).Append(item.ZOrder).Append(item.Name).Append(item.RotationDegrees);
            if (item.Content is TextBoxContent box) { text.Append(box.AutoGrowHeight); Container("o", box.Content); }
            if (item.Content is EmbeddedContent embedded) text.Append(embedded.Value);
        }
        foreach (var change in document.Changes.All)
        {
            text.Append("X").Append(change.Kind).Append(change.Id).Append(change.Author).Append(change.Comment);
            if (change is Deletion deletion) Container("x", deletion.Removed);
            if (change is FormatChange format) text.Append(string.Join(';', format.PreviousRuns));
        }
        text.Append(document.Changes.IsRecording).Append(string.Join(',', document.Variables)).Append(document.Properties.Title)
            .Append(string.Join(',', document.Properties.Keywords)).Append(string.Join(',', document.Properties.Custom));
        foreach (var style in document.Styles.Character.All) text.Append("CS").Append(style.Name).Append(style.Character);
        foreach (var style in document.Styles.Paragraph.All) text.Append("PS").Append(style.Name).Append(style.ParentName).Append(style.Paragraph).Append(style.Character);
        foreach (var page in document.Styles.Page.All)
        {
            text.Append("PG").Append(page.Name).Append(page.Size).Append(page.Margins).Append(page.Header.Enabled).Append(page.Footer.Enabled);
            Container("h", page.Header.Default);
            Container("f1", page.Footer.First);
        }
        text.Append(document.Styles.List.All.Count).Append(document.Styles.Frame.All.Count).Append(document.Styles.Table.All.Count);
        return System.Text.RegularExpressions.Regex.Replace(text.ToString(), @"(<>z__ReadOnlyArray|<>z__ReadOnlySingleElementList|System\.Collections\.Generic\.List)`1\[[^\]]*\]", "list");
    }

    private sealed class Texts : ITextCatalog
    {
        public UiLanguage Language => UiLanguage.Italian;

        public event EventHandler? LanguageChanged;

        public string this[string key] => key;

        public string Format(string key, params object?[] arguments) => key;

        public bool Contains(string key) => true;

        public void Use(UiLanguage language) => LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class CapturingDialogs(object answer) : IDialogService
    {
        public List<string> Buttons { get; } = [];

        public bool IsDialogOpen => false;

        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default)
        {
            if (content is IDialogFooterOwner { Footer: { } footer })
                Buttons.AddRange(footer.GetLogicalDescendants().OfType<Button>().Select(button => button.Content?.ToString() ?? string.Empty));
            return Task.FromResult((TResult?)answer);
        }

        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result = null)
        {
        }
    }

    private sealed class RecordingCanvas(DocumentSize size) : IPageCanvas
    {
        public List<string> Texts { get; } = [];

        public List<Twip> Lefts { get; } = [];

        public DocumentSize PageSize { get; } = size;

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints(text.Length * font.SizePoints / 2);

        public void FillRectangle(DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, PulseOffice.Printing.TextAlignment alignment = PulseOffice.Printing.TextAlignment.Left, PulseOffice.Printing.TextBaseline baseline = PulseOffice.Printing.TextBaseline.Middle)
        {
            Texts.Add(text);
            Lefts.Add(bounds.Left);
        }

        public void DrawPath(PrintPath path)
        {
        }

        public void DrawImage(PageImage image, DocumentRect bounds)
        {
        }
    }

    private sealed class RecordingSurface : IPageSurface
    {
        public List<string> Texts { get; } = [];

        public void Fill(PulseOffice.Documents.Layout.Tree.LayoutRect rect, uint argb)
        {
        }

        public void Line(double x1, double y1, double x2, double y2, uint argb, double thickness, PageStroke stroke = PageStroke.Solid)
        {
        }

        public void Frame(PulseOffice.Documents.Layout.Tree.LayoutRect rect, uint argb, double thickness, PageStroke stroke = PageStroke.Solid)
        {
        }

        public void Glyphs(PulseOffice.Documents.Layout.Tree.GlyphRun run, double x, double baseline, uint argb) => Texts.Add(run.Text);

        public void RotatedGlyphs(PulseOffice.Documents.Layout.Tree.GlyphRun run, double x, double baseline, uint argb, double degrees) => Texts.Add(run.Text);

        public void Embedded(PulseOffice.Document.Embedding.EmbeddedObject content, PulseOffice.Documents.Layout.Tree.LayoutRect bounds, ObjectTransform transform)
        {
        }
    }
}
