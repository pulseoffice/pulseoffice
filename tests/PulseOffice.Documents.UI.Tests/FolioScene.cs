using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout;
using PulseOffice.Documents.Layout.Measurement;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Formats.Interchange.Text;
using PulseOffice.Shell;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Tests;

public sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class SwapCorrector : IAutoCorrect
{
    public string Apply(string text, AutoCorrectOptions options) => text.Replace("teh", "the", StringComparison.Ordinal);
}

public sealed class NoSpelling : ISpellChecker
{
    public bool HasDictionary(LanguageTag language) => false;

    public IReadOnlyList<SpellingMistake> Check(string text, LanguageTag language, int suggestions = 6) => [];

    public bool IsCorrect(string word, LanguageTag language) => true;
}

public sealed class NoWords : IUserDictionary
{
    public IReadOnlyCollection<string> Words => [];

    public event EventHandler? Changed;

    public bool Knows(string word) => false;

    public void Add(string word) => Changed?.Invoke(this, EventArgs.Empty);

    public bool Remove(string word) => false;
}

public sealed record RichClipboardContent
{
    public string? Text { get; init; }

    public string? Html { get; init; }

    public string? Rtf { get; init; }

    public byte[]? Png { get; init; }
}

public sealed class MemoryClipboard : ISystemClipboardText, PulseOffice.Exchange.IExchangeClipboard
{
    private IReadOnlyList<PulseOffice.Exchange.ExchangeRepresentation> _content = [];

    public bool IsAvailable => true;

    public IReadOnlyList<PulseOffice.Exchange.ExchangeRepresentation> Representations => _content;

    public RichClipboardContent Rich => new()
    {
        Text = Text,
        Html = Find(PulseOffice.Exchange.ExchangeFlavor.Html)?.AsText(),
        Rtf = Find(PulseOffice.Exchange.ExchangeFlavor.Rtf)?.AsText(),
        Png = Image,
    };

    public string? Text => Find(PulseOffice.Exchange.ExchangeFlavor.Text)?.AsText();

    public byte[]? Image => Find(PulseOffice.Exchange.ExchangeFlavor.Png)?.Data.ToArray();

    public Task SetAsync(RichClipboardContent content)
    {
        var representations = new List<PulseOffice.Exchange.ExchangeRepresentation>();
        if (content.Text is { } text) representations.Add(PulseOffice.Exchange.ExchangeRepresentation.OfText(PulseOffice.Exchange.ExchangeFlavor.Text, text));
        if (content.Html is { } html) representations.Add(PulseOffice.Exchange.ExchangeRepresentation.OfText(PulseOffice.Exchange.ExchangeFlavor.Html, html));
        if (content.Rtf is { } rtf) representations.Add(PulseOffice.Exchange.ExchangeRepresentation.OfText(PulseOffice.Exchange.ExchangeFlavor.Rtf, rtf));
        if (content.Png is { } png) representations.Add(new PulseOffice.Exchange.ExchangeRepresentation(PulseOffice.Exchange.ExchangeFlavor.Png, png));
        _content = representations;
        return Task.CompletedTask;
    }

    public Task SetImageAsync(byte[] image) => SetAsync(new RichClipboardContent { Png = image });

    public Task<string?> GetTextAsync() => Task.FromResult(Text);

    public Task SetTextAsync(string text) => SetAsync(new RichClipboardContent { Text = text });

    public Task WriteAsync(IReadOnlyList<PulseOffice.Exchange.ExchangeRepresentation> representations, CancellationToken ct = default)
    {
        _content = [.. representations];
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PulseOffice.Exchange.ExchangeRepresentation>> ReadAsync(CancellationToken ct = default) => Task.FromResult(_content);

    private PulseOffice.Exchange.ExchangeRepresentation? Find(PulseOffice.Exchange.ExchangeFlavor flavor) =>
        _content.FirstOrDefault(representation => representation.Flavor == flavor);
}

public sealed class NoScriptCommands : PulseOffice.Scripting.Editor.Facades.IScriptCommands
{
    public bool Run(string command) => false;
}

public sealed class FolioScene
{
    internal static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(TestApp));

    internal static Task RunAsync(Func<Task> body) => Session.Dispatch(body, CancellationToken.None);

    public FolioScene(Action<TextDocument>? prepare = null, bool focus = true, ITextMeasurer? measurer = null, Action<IServiceCollection>? configure = null)
    {
        var collection = new ServiceCollection();
        collection
            .AddDocumentsModel()
            .AddTextInterchange()
            .AddSingleton(measurer ?? new FixedPitchMeasurer())
            .AddDocumentsLayout()
            .AddSingleton<IGlyphRunPainter, FormattedRunPainter>()
            .AddSingleton<ISpellChecker, NoSpelling>()
            .AddSingleton<PulseOffice.Platform.IAtomicFileWriter, PulseOffice.Platform.AtomicFileWriter>()
            .AddSingleton<IGrammarRule, RepeatedWordRule>()
            .AddSingleton<IGrammarChecker, GrammarChecker>()
            .AddSingleton<ITextLanguageResolver, TextLanguageResolver>()
            .AddSingleton<IAutoSpellSettings, AutoSpellSettings>()
            .AddSingleton<IUserDictionary, NoWords>()
            .AddSingleton<PulseOffice.Text.Spelling.Dictionaries.IDictionaryChanges, PulseOffice.Text.Spelling.Dictionaries.DictionaryChanges>()
            .AddSingleton<IAutoCorrect, SwapCorrector>()
            .AddSingleton<IWordBoundaryCorrection, WordBoundaryCorrection>()
            .AddSingleton<IAutoCorrectSettings, AutoCorrectSettings>()
            .AddSingleton<PulseOffice.Text.Rich.ICaretWord, PulseOffice.Text.Rich.CaretWord>()
            .AddSingleton<ISystemClipboardText>(Clipboard)
            .AddSingleton<PulseOffice.Exchange.IExchangeClipboard>(Clipboard)
            .AddSingleton<PulseOffice.Localization.ITextCatalog>(new PulseOffice.Localization.TextCatalog())
            .AddSingleton<PulseOffice.Scripting.IMacroFingerprints, PulseOffice.Scripting.Sha256MacroFingerprints>()
            .AddSingleton<PulseOffice.Scripting.IMacroWriteGrants, NoMacroWriteGrants>()
            .AddSingleton<PulseOffice.Scripting.IMacroPermissionPolicy, PulseOffice.Scripting.MacroPermissionPolicy>()
            .AddSingleton<PulseOffice.UI.Commands.ICommandEffectChecks, PulseOffice.UI.Commands.CommandEffectChecks>()
            .AddSingleton<PulseOffice.Scripting.Editor.IGuardedMacroRuns, PulseOffice.Scripting.Editor.GuardedMacroRuns>()
            .AddSingleton<PulseOffice.Scripting.IMacroFileFolder>(new PulseOffice.Scripting.MacroFileFolder(Path.Combine(Path.GetTempPath(), "pulseoffice-folio-macros")))
            .AddSingleton<PulseOffice.Platform.IFileSystemLinks, PulseOffice.Platform.FileSystemLinks>()
            .AddSingleton<PulseOffice.Platform.IConfinedFileWriter, PulseOffice.Platform.ConfinedFileWriter>()
            .AddSingleton<PulseOffice.Scripting.ScriptFilesFacade>()
            .AddSingleton<IDocumentWorkspace, DocumentWorkspace>()
            .AddSingleton<IRibbonStateNotifier, RibbonStateNotifier>()
            .AddSingleton<PulseOffice.UI.Controls.Prompts.IZoomPrompt>(_ => null!)
            .AddSingleton<PulseOffice.UI.Controls.Zoom.IZoomChoice, PulseOffice.UI.Controls.Zoom.ZoomChoice>()
            .AddSingleton<PulseOffice.Formats.ITextDocumentFormatRegistry>(_ => new PulseOffice.Formats.TextDocumentFormatRegistry([new PulseOffice.Formats.Odf.Odt.OdtReader()], [new PulseOffice.Formats.Odf.Odt.OdtWriter()], new PulseOffice.Formats.Intake.DocumentIntake()))
            .AddSingleton<IContextMenuFactory>(_ => null!)
            .AddSingleton<PulseOffice.UI.Controls.Objects.IRasterImageFiles>(_ => null!)
            .AddSingleton(TimeProvider.System)
            .AddSingleton<PulseOffice.UI.Controls.Forms.IFormDesignMode, PulseOffice.UI.Controls.Forms.FormDesignMode>()
            .AddSingleton<PulseOffice.UI.Controls.Macros.IDocumentMacroGate, ConsentingMacroGate>()
            .AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>))
            .AddSingleton<PulseOffice.UI.Controls.Notifications.INotificationCenter>(_ => new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System))
            .AddSingleton<PulseOffice.UI.Controls.Notifications.INotificationHistory>(_ => new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System))
            .AddSingleton<IStatusBarService, StatusBarService>()
            .AddSingleton<PulseOffice.Platform.IShellOpener>(Opener)
            .AddSingleton<PulseOffice.Scripting.IScriptOutput, PulseOffice.Scripting.ScriptOutput>()
            .AddSingleton<PulseOffice.Scripting.Editor.Facades.IScriptDispatcher, PulseOffice.Scripting.Editor.Facades.UiThreadScriptDispatcher>()
            .AddSingleton<PulseOffice.Scripting.Editor.Facades.IScriptCommands, NoScriptCommands>()
            .AddSingleton<PulseOffice.Scripting.Editor.Facades.IScriptClipboardOperations, PulseOffice.Scripting.Editor.Facades.ScriptClipboardOperations>()
            .AddSingleton<PulseOffice.Scripting.IScriptEngine, PulseOffice.Scripting.JavaScript.JavaScriptEngine>()
            .AddFolioUI()
            .AddSingleton<PulseOffice.UI.Controls.Pages.IPageNumberPositionGalleries>(_ => null!)
            .AddSingleton(new PulseOffice.Documents.UI.Commands.FolioToolViews(null!, null!, null!, null!, null!))
            .AddSingleton<PulseOffice.Documents.UI.Commands.IFolioForms>(new AnsweringForms(new Dictionary<string, object>()))
            .AddSingleton<PulseOffice.UI.Controls.Themes.IThemePaletteGallery>(new NoThemeGallery());
        configure?.Invoke(collection);
        var provider = collection.BuildServiceProvider();
        Services = provider;
        Document = provider.GetRequiredService<ITextDocumentFactory>().Create();
        prepare?.Invoke(Document);
        History = new UndoStack();
        Editor = new FolioEditor(Document, History, provider.GetRequiredService<FolioEditingServices>(), () => ReadOnly);
        View = new FolioView(Editor, provider.GetRequiredService<FolioViewServices>());
        Window = new Window { Width = 900, Height = 700, Content = View };
        Window.Show();
        Dispatcher.UIThread.RunJobs();
        if (focus) View.FocusDocument();
        Dispatcher.UIThread.RunJobs();
    }

    public IServiceProvider Services { get; }

    public MemoryClipboard Clipboard { get; } = new();

    public RecordingOpener Opener { get; } = new();

    public TextDocument Document { get; }

    public UndoStack History { get; }

    public FolioEditor Editor { get; }

    public FolioView View { get; }

    public Window Window { get; }

    public bool ReadOnly { get; set; }

    public IReadOnlyList<string> Paragraphs => [.. Document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text)];

    public void Type(string text)
    {
        Window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    public void Press(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }
}

public sealed class ConsentingMacroGate : PulseOffice.UI.Controls.Macros.IDocumentMacroGate
{
    public Task<bool> AllowAsync(PulseOffice.Security.IMacroSignatureHolder document, string macroName, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class NoThemeGallery : PulseOffice.UI.Controls.Themes.IThemePaletteGallery
{
    public Task<PulseOffice.Document.Themes.ThemePalette?> AskAsync(string titleKey, string explanationKey, string? currentKey, CancellationToken ct) =>
        Task.FromResult<PulseOffice.Document.Themes.ThemePalette?>(null);

    public PulseOffice.UI.Controls.Galleries.GalleryPickerView<PulseOffice.Document.Themes.ThemePalette> Create(string explanation, string? currentKey) =>
        throw new NotSupportedException();
}

internal sealed class NoMacroWriteGrants : PulseOffice.Scripting.IMacroWriteGrants
{
    public bool IsGranted(PulseOffice.Scripting.MacroIdentity macro) => false;

    public bool Grant(PulseOffice.Scripting.MacroIdentity macro) => false;

    public bool Revoke(PulseOffice.Scripting.MacroIdentity macro) => true;
}

public sealed class RecordingOpener : PulseOffice.Platform.IShellOpener
{
    public List<string> Opened { get; } = [];

    public PulseOffice.Foundation.Diagnostics.OperationResult<bool> OpenUrl(string url)
    {
        if (!url.StartsWith("http", StringComparison.Ordinal))
            return PulseOffice.Foundation.Diagnostics.OperationResult<bool>.Fail(PulseOffice.Foundation.Diagnostics.Diagnostic.Error("TEST", "blocked"));
        Opened.Add(url);
        return PulseOffice.Foundation.Diagnostics.OperationResult<bool>.Ok(true);
    }

    public PulseOffice.Foundation.Diagnostics.OperationResult<bool> OpenFile(string path) =>
        PulseOffice.Foundation.Diagnostics.OperationResult<bool>.Fail(PulseOffice.Foundation.Diagnostics.Diagnostic.Error("TEST", "no files"));
}
