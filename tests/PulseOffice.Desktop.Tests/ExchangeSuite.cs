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

public sealed class ExchangeSuite(ServiceProvider provider, CommandRegistry registry, ShellWindow window, IReadOnlyDictionary<ModuleKind, OpenDocument> documents) : IAsyncDisposable
{
    public T Service<T>() where T : notnull => provider.GetRequiredService<T>();

    public IObjectExchange Exchange => Service<IObjectExchange>();

    public OpenDocument Document(ModuleKind module) => documents[module];

    public void Activate(ModuleKind module) => Activate(documents[module]);

    public void Activate(OpenDocument document)
    {
        Service<IDocumentWorkspace>().Activate(document);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    public PulseOffice.Documents.UI.Editing.FolioEditor Folio
    {
        get
        {
            Activate(ModuleKind.Documents);
            return Service<IActiveFolioContext>().Target!.Editor;
        }
    }

    public PulseOffice.Documents.UI.Editing.FolioEditor FolioOf(OpenDocument document)
    {
        Activate(document);
        return Service<IActiveFolioContext>().Target!.Editor;
    }

    public DeckTarget DeckOf(OpenDocument document)
    {
        Activate(document);
        return Service<IActiveDeckContext>().Target!;
    }

    public async Task<OpenDocument> OpenAnother(ModuleKind module)
    {
        var name = module switch
        {
            ModuleKind.Documents => "documents",
            ModuleKind.Presentations => "presentations",
            _ => "sheets",
        };
        await Run("shell.file.new." + name);
        var opened = Service<IDocumentWorkspace>().Active!;
        Assert.Equal(module, opened.Module);
        return opened;
    }

    public DeckTarget Deck
    {
        get
        {
            Activate(ModuleKind.Presentations);
            return Service<IActiveDeckContext>().Target!;
        }
    }

    public IActiveSheetContext Ledger
    {
        get
        {
            Activate(ModuleKind.Sheets);
            return Service<IActiveSheetContext>();
        }
    }

    public Worksheet Sheet => Ledger.Workbook!.Sheets[Ledger.SheetIndex];

    public async Task<bool> Copy(ModuleKind module)
    {
        Activate(module);
        var package = await Exchange.CopyAsync(module);
        Dispatcher.UIThread.RunJobs();
        return package is not null;
    }

    public async Task<bool> Paste(ModuleKind module, ExchangePasteMode? mode = null)
    {
        Activate(module);
        var pasted = await Exchange.PasteAsync(module, mode);
        Dispatcher.UIThread.RunJobs();
        return pasted;
    }

    public async Task Run(string id)
    {
        var command = registry.Find(new CommandId(id));
        Assert.NotNull(command);
        await Service<CommandExecutor>().ExecuteAsync(command, Service<IDocumentWorkspace>().Context);
        Dispatcher.UIThread.RunJobs();
    }

    public void Undo(ModuleKind module)
    {
        Activate(module);
        documents[module].History.Undo();
        Dispatcher.UIThread.RunJobs();
    }

    public async ValueTask DisposeAsync()
    {
        window.Close();
        await provider.DisposeAsync();
    }

    public PlacedObject PlaceInLedger(EmbeddedObject content)
    {
        var context = Ledger;
        var placed = new PlacedObject
        {
            Id = content.Id,
            Content = content,
            Anchor = new CellAddress(5, 5),
            Size = new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(5)),
        };
        Sheet.Objects.Add(placed);
        context.SelectObjects([placed]);
        Dispatcher.UIThread.RunJobs();
        return placed;
    }

    public ObjectElement PlaceInDeck(EmbeddedObject content)
    {
        var target = Deck;
        var slide = target.CurrentSlide()!;
        var element = new ObjectElement
        {
            Id = content.Id,
            Content = content,
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(6), Twip.FromCentimeters(4)),
        };
        target.Actions.InsertElement(slide, element);
        target.Select([element]);
        Dispatcher.UIThread.RunJobs();
        return element;
    }

    public void PlaceInFolio(EmbeddedObject content)
    {
        var editor = Folio;
        var id = editor.Document.NextId("o");
        editor.Insert((documents, at) => documents.InsertObject(editor.Document, editor.History,
            new AnchoredObject(id, new AsCharacterAnchor(at.ParagraphId), new EmbeddedContent(content), new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(4))), at), "insert");
        editor.SelectObject(id);
        Dispatcher.UIThread.RunJobs();
    }

    public IReadOnlyList<EmbeddedObject> FolioObjects() =>
        [.. Folio.Document.Objects.All.Select(item => item.Content).OfType<EmbeddedContent>().Select(content => content.Value)];

    public IReadOnlyList<EmbeddedObject> DeckObjects()
    {
        var slide = Deck.CurrentSlide()!;
        var units = new SlideGroupUnits();
        var editing = new SlideGroupEditing(units);
        var grouped = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(int Z, EmbeddedObject Content)>();
        foreach (var group in editing.Outermost(slide.Groups))
        {
            var leaves = units.Leaves(slide, group).Select(slide.Find).OfType<ObjectElement>().ToList();
            if (new DeckGrouping().Group(leaves, group.Id) is not { } composed) continue;
            foreach (var leaf in leaves) grouped.Add(leaf.Id);
            result.Add((composed.ZOrder, composed.Content));
        }
        result.AddRange(slide.Elements.OfType<ObjectElement>().Where(element => !grouped.Contains(element.Id)).Select(element => (element.ZOrder, element.Content)));
        return [.. result.OrderBy(item => item.Z).Select(item => item.Content)];
    }

    public IReadOnlyList<EmbeddedObject> LedgerObjects() =>
        [.. Sheet.Objects.Select(placed => placed.Content)];

    public IReadOnlyList<EmbeddedObject> Objects(ModuleKind module) => module switch
    {
        ModuleKind.Documents => FolioObjects(),
        ModuleKind.Presentations => DeckObjects(),
        _ => LedgerObjects(),
    };

    public void Place(ModuleKind module, EmbeddedObject content)
    {
        switch (module)
        {
            case ModuleKind.Sheets:
                PlaceInLedger(content);
                break;
            case ModuleKind.Documents:
                PlaceInFolio(content);
                break;
            default:
                PlaceInDeck(content);
                break;
        }
    }
}

public sealed class ExchangeSuiteFactory
{
    public async Task<ExchangeSuite> Open()
    {
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var documents = new Dictionary<ModuleKind, OpenDocument>();
        foreach (var (module, command) in new[] { (ModuleKind.Sheets, "sheets"), (ModuleKind.Documents, "documents"), (ModuleKind.Presentations, "presentations") })
        {
            await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new." + command))!, workspace.Context);
            Dispatcher.UIThread.RunJobs();
            documents[module] = workspace.Active!;
            Assert.Equal(module, workspace.Active!.Module);
        }
        return new ExchangeSuite(provider, registry, window, documents);
    }
}
