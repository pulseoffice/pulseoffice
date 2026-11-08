using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Highlighting;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Scripting.Editor;

public sealed class MacroEditorView : UserControl, PulseOffice.UI.Controls.IDialogFooterOwner, PulseOffice.UI.Controls.Text.ITextEditingTarget
{
    private readonly IReadOnlyList<IScriptEngine> _engines;
    private readonly Func<IScriptEngine, string, ScriptContext> _contexts;
    private readonly IMacroWriteAccess? _writeAccess;
    private MacroWriteAccessButton? _writeButton;
    private readonly IReadOnlyDictionary<string, string> _texts;
    private readonly ScriptApiCatalog _api;
    private readonly IScriptDocumentation _documentation;
    private readonly IScriptOutput _scriptOutput;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);

    private readonly ComboBox _language = new();
    private readonly TextEditor _editor = new();
    private readonly TextBox _output = new();
    private readonly TextBlock _status = new();
    private readonly TextBox _search = new();
    private readonly ListBox _topics = new();
    private readonly DocumentationPanel _detail;
    private readonly ListBox _macros = new();
    private readonly TextBox _name = new();
    private readonly TextBlock _empty = new();
    private readonly IMacroLibrary? _library;
    private readonly Func<IDisposable>? _runScope;

    private IReadOnlyList<DocumentMacro> _stored = [];
    private string? _open;
    private bool _switching;

    private IReadOnlyList<DocumentationTopic> _shown = [];
    private CompletionWindow? _completion;

    public event EventHandler? CloseRequested;

    public Control? Footer { get; private set; }

    public MacroEditorView(
        IEnumerable<IScriptEngine> engines,
        Func<IScriptEngine, string, ScriptContext> contexts,
        ScriptApiCatalog api,
        IScriptDocumentation documentation,
        IScriptOutput scriptOutput,
        IReadOnlyDictionary<string, string> texts,
        IMacroLibrary? library = null,
        Func<IDisposable>? runScope = null,
        IMacroWriteAccess? writeAccess = null)
    {
        ArgumentNullException.ThrowIfNull(engines);

        _engines = [.. engines.OrderByDescending(engine => engine.Language == ScriptLanguage.JavaScript)];
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _detail = new DocumentationPanel(_texts, InterfaceDensity.Compact);
        _documentation = documentation ?? throw new ArgumentNullException(nameof(documentation));
        _scriptOutput = scriptOutput ?? throw new ArgumentNullException(nameof(scriptOutput));
        _library = library;
        _runScope = runScope;
        _writeAccess = writeAccess;

        Content = Build();

        Reload();
        ReloadMacros();
    }

    public string Source
    {
        get => _editor.Text;
        set => _editor.Text = value;
    }

    public IReadOnlyDictionary<ScriptLanguage, string> Samples { get; init; } = new Dictionary<ScriptLanguage, string>();

    private readonly ModuleKind _module = ModuleKind.Sheets;

    public string? OpenMacro { get; init; }

    public ModuleKind Module
    {
        get => _module;
        init
        {
            _module = value;
            Reload();
        }
    }

    private IScriptEngine Engine => _engines[Math.Max(0, _language.SelectedIndex)];

    private Control Build()
    {
        _language.ItemsSource = _engines
            .Select(engine => engine.Language == ScriptLanguage.JavaScript
                ? $"{engine.DisplayName} — {_texts["default"]}"
                : engine.DisplayName)
            .ToList();

        _language.SelectedIndex = 0;
        _language.SelectionChanged += (_, _) => ApplyLanguage();

        _editor.ShowLineNumbers = true;
        _editor.Options.ShowEndOfLine = false;
        _editor.Options.AllowScrollBelowDocument = false;
        _editor.FontFamily = new FontFamily("Menlo, Consolas, monospace");
        _editor.FontSize = _metrics.FontSize;
        _editor.Options.ConvertTabsToSpaces = true;
        _editor.Options.IndentationSize = 2;
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.HighlightCurrentLine = true;
        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.KeyDown += OnEditorKeyDown;

        _output.AcceptsReturn = true;
        _output.IsReadOnly = true;
        _output.FontFamily = _editor.FontFamily;
        _output.FontSize = _metrics.FontSizeSmall;
        _output.MinHeight = 110;

        _status.FontSize = _metrics.FontSizeSmall;
        var run = Button(_texts["run"], async () => await RunAsync());
        _runLabel.Text = _texts["run"];
        _stopLabel.Text = _texts["stop"];
        _stopLabel.Opacity = 0;
        run.Content = new Grid { Children = { _runLabel, _stopLabel } };
        _run = run;
        Avalonia.Automation.AutomationProperties.SetName(run, _texts["run"]);
        var check = Button(_texts["check"], Analyze);
        var guide = Button(_texts["guide"], ShowGuide);
        guide.HorizontalAlignment = HorizontalAlignment.Stretch;
        guide.HorizontalContentAlignment = HorizontalAlignment.Center;
        Footer = new PulseOffice.UI.Controls.DialogFooter()
            .WithCancel(_texts["close"], () => CloseRequested?.Invoke(this, EventArgs.Empty));

        var bar = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var control in new Control[] { _language, run, check })
        {
            control.Margin = new Thickness(0, 0, _metrics.SpacingSmall, _metrics.SpacingSmall);
            bar.Children.Add(control);
        }

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { bar } };
        if (_writeAccess is not null)
        {
            _writeButton = new MacroWriteAccessButton(_writeAccess, _texts, () => Engine.Language, () => _editor.Text,
                new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall))
            {
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(_metrics.SpacingSmall, 0, 0, _metrics.SpacingSmall),
            };
            _editor.TextChanged += (_, _) => _writeButton.Refresh();
            Grid.SetColumn(_writeButton, 1);
            header.Children.Add(_writeButton);
        }

        if (_library is not null)
        {
            _name.PlaceholderText = _texts["name"];
            _name.Width = 180;
            _name.VerticalAlignment = VerticalAlignment.Center;
            _name.Margin = new Thickness(0, 0, _metrics.SpacingSmall, _metrics.SpacingSmall);

            var save = Button(_texts["save"], Save);
            save.Margin = _name.Margin;

            bar.Children.Insert(0, _name);
            bar.Children.Insert(1, save);
        }

        var left = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto") };

        var editorFrame = new Border
        {
            Child = _editor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            Margin = new Thickness(0, _metrics.SpacingSmall),
        };

        var outputLabel = new TextBlock
        {
            Text = _texts["output"],
            FontSize = _metrics.FontSizeSmall,
        };

        Grid.SetRow(header, 0);
        Grid.SetRow(editorFrame, 1);
        Grid.SetRow(outputLabel, 2);
        Grid.SetRow(_output, 3);
        Grid.SetRow(_status, 4);

        left.Children.Add(header);
        left.Children.Add(editorFrame);
        left.Children.Add(outputLabel);
        left.Children.Add(_output);
        left.Children.Add(_status);

        var right = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,2*,3*"),
            Margin = new Thickness(_metrics.SpacingMedium, 0, 0, 0),
        };

        _search.PlaceholderText = _texts["search"];
        _search.TextChanged += (_, _) => Reload();

        _topics.SelectionChanged += (_, _) => ShowDetail();
        _topics.DoubleTapped += (_, _) => InsertTopic();
        _topics.Classes.Add("compact");

        _detail.MinHeight = 240;

        Grid.SetRow(guide, 0);
        Grid.SetRow(_search, 1);
        Grid.SetRow(_topics, 2);
        Grid.SetRow(_detail, 3);

        right.Children.Add(guide);
        right.Children.Add(_search);
        right.Children.Add(_topics);
        right.Children.Add(_detail);

        _search.Margin = new Thickness(0, _metrics.SpacingSmall);
        _topics.Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall);

        var layout = _library is null
            ? new Grid { ColumnDefinitions = new ColumnDefinitions("*,320") }
            : new Grid { ColumnDefinitions = new ColumnDefinitions("240,*,320") };

        if (_library is not null)
        {
            var library = BuildLibrary();
            Grid.SetColumn(library, 0);
            layout.Children.Add(library);
        }

        Grid.SetColumn(left, _library is null ? 0 : 1);
        Grid.SetColumn(right, _library is null ? 1 : 2);

        layout.Children.Add(left);
        layout.Children.Add(right);

        ApplyLanguage();

        return layout;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_library is null || _open is not null || _stored.Count == 0) return;

        var wanted = OpenMacro is null ? -1 : _stored.ToList().FindIndex(macro => string.Equals(macro.Name, OpenMacro, StringComparison.OrdinalIgnoreCase));

        _macros.SelectedIndex = Math.Max(0, wanted);
    }

    private Control BuildLibrary()
    {
        var caption = new TextBlock
        {
            Text = _texts["macros"].ToUpperInvariant(),
            FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
            LetterSpacing = 0.6,
            Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall / 2),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        _macros.Classes.Add("compact");
        _macros.FontSize = _metrics.FontSizeSmall;
        _macros.SelectionChanged += (_, _) => OpenSelected();

        var actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0),
        };

        var buttons = new[]
        {
            Button(_texts["newMacro"], NewMacro),
            Button(_texts["duplicate"], Duplicate),
            Button(_texts["delete"], Delete),
        };

        for (var index = 0; index < buttons.Length; index++)
        {
            buttons[index].Padding = new Thickness(_metrics.SpacingSmall, _metrics.SpacingSmall / 2);
            buttons[index].FontSize = _metrics.FontSizeSmall;
            buttons[index].HorizontalAlignment = HorizontalAlignment.Stretch;
            buttons[index].HorizontalContentAlignment = HorizontalAlignment.Center;
            buttons[index].Margin = new Thickness(index == 0 ? 0 : _metrics.SpacingSmall / 2, 0, 0, 0);
            Grid.SetColumn(buttons[index], index);
            actions.Children.Add(buttons[index]);
        }

        var column = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 0, _metrics.SpacingMedium, 0),
        };

        _empty.Text = _texts["empty"];
        _empty.FontSize = _metrics.FontSizeSmall;
        _empty.TextWrapping = TextWrapping.Wrap;
        _empty.Margin = new Thickness(_metrics.SpacingMedium);
        _empty.VerticalAlignment = VerticalAlignment.Top;
        _empty.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        Grid.SetRow(caption, 0);
        Grid.SetRow(_macros, 1);
        Grid.SetRow(_empty, 1);
        Grid.SetRow(actions, 2);

        column.Children.Add(caption);
        column.Children.Add(_macros);
        column.Children.Add(_empty);
        column.Children.Add(actions);

        return column;
    }

    private void ReloadMacros()
    {
        if (_library is null) return;

        _stored = _library.All;

        _switching = true;
        _macros.ItemsSource = _stored.Select(macro => macro.Name).ToList();
        _macros.SelectedIndex = _open is null
            ? -1
            : _stored.Select((macro, index) => (macro, index))
                .Where(pair => string.Equals(pair.macro.Name, _open, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.index)
                .DefaultIfEmpty(-1)
                .First();
        _switching = false;

        _empty.IsVisible = _stored.Count == 0;
    }

    private void OpenSelected()
    {
        if (_switching || _library is null) return;
        if (_macros.SelectedIndex < 0 || _macros.SelectedIndex >= _stored.Count) return;

        Commit();

        var macro = _stored[_macros.SelectedIndex];

        _open = macro.Name;
        _name.Text = macro.Name;
        _editor.Text = macro.Source;
        _language.SelectedIndex = IndexOf(macro.Language);
    }

    private int IndexOf(MacroLanguage language)
    {
        var wanted = language == MacroLanguage.Python ? ScriptLanguage.Python : ScriptLanguage.JavaScript;

        var index = _engines.Select((engine, position) => (engine, position))
            .Where(pair => pair.engine.Language == wanted)
            .Select(pair => pair.position)
            .DefaultIfEmpty(0)
            .First();

        return index;
    }

    private void Commit()
    {
        if (_library is null || _open is null) return;

        _library.Store(new DocumentMacro(_open, Language(), _editor.Text));
    }

    private MacroLanguage Language() =>
        Engine.Language == ScriptLanguage.Python ? MacroLanguage.Python : MacroLanguage.JavaScript;

    private void Save()
    {
        if (_library is null) return;

        var name = (_name.Text ?? string.Empty).Trim();

        if (name.Length == 0)
        {
            _status.Text = _texts["unnamed"];
            return;
        }

        if (_open is not null && !string.Equals(_open, name, StringComparison.OrdinalIgnoreCase))
            _library.Remove(_open);

        _library.Store(new DocumentMacro(name, Language(), _editor.Text));

        _open = name;

        ReloadMacros();

        _status.Text = string.Format(
            System.Globalization.CultureInfo.CurrentCulture, _texts["saved"], name);
    }

    private void NewMacro()
    {
        if (_library is null) return;

        Commit();

        _open = null;
        _name.Text = Available(_texts["untitled"]);
        _editor.Text = string.Empty;

        _switching = true;
        _macros.SelectedIndex = -1;
        _switching = false;

        _name.Focus();
    }

    private void Duplicate()
    {
        if (_library is null || _macros.SelectedIndex < 0 || _macros.SelectedIndex >= _stored.Count) return;

        Commit();

        var source = _stored[_macros.SelectedIndex];
        var copy = Available(source.Name);

        _library.Store(source with { Name = copy });

        _open = copy;

        ReloadMacros();
        OpenSelected();
    }

    private void Delete()
    {
        if (_library is null || _macros.SelectedIndex < 0 || _macros.SelectedIndex >= _stored.Count) return;

        var gone = _stored[_macros.SelectedIndex].Name;

        _library.Remove(gone);

        _open = null;
        _name.Text = string.Empty;
        _editor.Text = string.Empty;

        ReloadMacros();

        _status.Text = string.Format(
            System.Globalization.CultureInfo.CurrentCulture, _texts["deleted"], gone);
    }

    private string Available(string wanted)
    {
        if (_library is null) return wanted;

        if (_library.Find(wanted) is null) return wanted;

        for (var index = 2; ; index++)
        {
            var candidate = $"{wanted} {index}";
            if (_library.Find(candidate) is null) return candidate;
        }
    }

    private Button Button(string label, Action run)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(_metrics.SpacingLarge, _metrics.SpacingSmall),
        };

        button.Click += (_, _) => run();

        return button;
    }

    private void ApplyLanguage()
    {
        if (Samples.Values.Contains(_editor.Text) && Samples.TryGetValue(Engine.Language, out var sample))
            _editor.Text = sample;

        _editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition(
            Engine.Language == ScriptLanguage.Python ? "Python" : "JavaScript");

        _status.Text = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            Engine.IsSandboxed ? _texts["runtime"] : _texts["runtimeUnsandboxed"], Engine.DisplayName, Engine.RuntimeVersion);

        _writeButton?.Refresh();
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (e.Text is "." or "(") ShowCompletion();
        else if (_completion is not null && e.Text is " " or ";" or ")") _completion.Close();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

        ShowCompletion();
        e.Handled = true;
    }

    private void ShowCompletion()
    {
        var caret = _editor.TextArea.Caret;

        var items = Engine.Complete(
            _editor.Text,
            new ScriptPosition(caret.Line, caret.Column),
            _api,
            Module);

        if (items.Count == 0) return;

        _completion?.Close();

        var window = new CompletionWindow(_editor.TextArea)
        {
            CloseAutomatically = true,
            MaxHeight = 260,
            StartOffset = WordStart(_editor.Text, _editor.CaretOffset),
        };

        foreach (var item in items.OrderByDescending(item => item.IsApiSymbol).ThenBy(item => item.Label, StringComparer.Ordinal))
            window.CompletionList.CompletionData.Add(new ScriptCompletionData(item));

        window.Closed += (_, _) => _completion = null;

        _completion = window;

        window.Show();
    }

    private int WordStart(string text, int offset)
    {
        var start = Math.Clamp(offset, 0, text.Length);
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] is '_' or '$')) start--;
        return start;
    }

    private void Reload()
    {
        _shown = _documentation.Search(Module, _search.Text ?? string.Empty);

        _topics.ItemsSource = _shown.Select(topic => topic.Title).ToList();
        _topics.FontSize = _metrics.FontSizeSmall;

        if (_shown.Count > 0) _topics.SelectedIndex = 0;
        else _detail.ShowEmpty(_texts["noTopic"]);
    }

    private void ShowDetail()
    {
        if (_topics.SelectedIndex < 0 || _topics.SelectedIndex >= _shown.Count) return;

        _detail.Show(_shown[_topics.SelectedIndex]);
    }

    private void ShowGuide()
    {
        _topics.SelectedIndex = -1;
        _detail.ShowGuide();
    }

    private void InsertTopic()
    {
        if (_topics.SelectedIndex < 0 || _topics.SelectedIndex >= _shown.Count) return;

        var topic = _shown[_topics.SelectedIndex];

        _editor.Document.Insert(_editor.CaretOffset, topic.Insert);
        _editor.Focus();
    }

    private void Analyze()
    {
        var problems = Engine.Analyze(_editor.Text);

        _output.Text = problems.Count == 0
            ? _texts["noProblems"]
            : string.Join('\n', problems.Select(Describe));
    }

    private string Describe(ScriptDiagnostic diagnostic)
    {
        var message = diagnostic.MessageKey is { } key && _texts.TryGetValue(key, out var template)
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, template, [.. diagnostic.MessageArguments])
            : diagnostic.Message;

        return diagnostic.Start.Line > 0 ? $"{diagnostic.Start.Line}:{diagnostic.Start.Column}  {message}" : message;
    }

    private bool _running;

    private Button? _run;

    private readonly TextBlock _runLabel = new() { HorizontalAlignment = HorizontalAlignment.Center };

    private readonly TextBlock _stopLabel = new() { HorizontalAlignment = HorizontalAlignment.Center };

    private CancellationTokenSource? _stop;

    private async Task RunAsync()
    {
        if (_running)
        {
            _stop?.Cancel();
            return;
        }

        _running = true;
        using var stop = new CancellationTokenSource();
        _stop = stop;
        ShowStop(true);

        try
        {
            await RunOnceAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _output.Text = ex.Message;
        }
        finally
        {
            _running = false;
            _stop = null;
            ShowStop(false);
        }
    }

    private void ShowStop(bool stopping)
    {
        _runLabel.Opacity = stopping ? 0 : 1;
        _stopLabel.Opacity = stopping ? 1 : 0;
        if (_run is not null) Avalonia.Automation.AutomationProperties.SetName(_run, stopping ? _texts["stop"] : _texts["run"]);
    }

    private async Task RunOnceAsync()
    {
        var engine = Engine;
        var transcript = new ScriptTranscript();
        var context = _contexts(engine, _editor.Text) with { OnOutput = transcript.Add };

        _output.Text = _texts["running"];
        _status.Text = _texts["running"];

        ScriptResult result;

        using (_scriptOutput.Capture(transcript.Add))
        using (_runScope?.Invoke())
            result = await engine.ExecuteAsync(_editor.Text, context, _stop?.Token ?? CancellationToken.None).ConfigureAwait(true);

        List<string> shown = [.. transcript.Lines];
        if (transcript.Truncated) shown.Add(_texts["outputTruncated"]);

        shown.AddRange(result.Diagnostics.Select(Describe));

        if (result.Value is { } value) shown.Add(value.ToString() ?? string.Empty);

        _output.Text = shown.Count == 0 ? _texts["noOutput"] : string.Join('\n', shown);

        _status.Text = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            result.Succeeded ? _texts["done"] : _texts["failed"],
            result.Elapsed.TotalMilliseconds);
    }

    public void Perform(PulseOffice.UI.Controls.Text.TextEditingAction action)
    {
        switch (action)
        {
            case PulseOffice.UI.Controls.Text.TextEditingAction.SelectAll: _editor.SelectAll(); break;
            case PulseOffice.UI.Controls.Text.TextEditingAction.Copy: _editor.Copy(); break;
            case PulseOffice.UI.Controls.Text.TextEditingAction.Cut: _editor.Cut(); break;
            case PulseOffice.UI.Controls.Text.TextEditingAction.Paste: _editor.Paste(); break;
            case PulseOffice.UI.Controls.Text.TextEditingAction.Undo: _editor.Undo(); break;
            case PulseOffice.UI.Controls.Text.TextEditingAction.Redo: _editor.Redo(); break;
        }
    }
}
