using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.Settings;
using PulseOffice.Settings.Navigation;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Settings;

public sealed record SettingEditorContext(
    SettingDefinition Definition,
    ISettingsService Settings,
    ITextCatalog Texts,
    IIconCatalog Icons,
    DesignTokens.Metrics Metrics,
    Action<object> Apply)
{
    public double FieldWidth => 220;

    public string Text(string key) => Texts[key];

    public string? Optional(string key) => Texts.Contains(key) ? Texts[key] : null;
}

public abstract class SettingField(SettingEditorContext context)
{
    private int _refreshing;

    protected SettingEditorContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

    protected SettingDefinition Definition => Context.Definition;

    public abstract Control View { get; }

    public virtual string? Hint => null;

    public virtual bool IsWide => false;

    public void Refresh()
    {
        _refreshing++;
        try
        {
            Show();
        }
        finally
        {
            _refreshing--;
        }
    }

    protected abstract void Show();

    protected void Commit(object value)
    {
        if (_refreshing == 0) Context.Apply(value);
    }
}

public interface ISettingEditor
{
    bool Handles(SettingDefinition definition);

    SettingField Create(SettingEditorContext context);
}

public interface ISettingEditorFactory
{
    SettingField Create(SettingEditorContext context);
}

public sealed class SettingEditorFactory : ISettingEditorFactory
{
    private readonly IReadOnlyList<ISettingEditor> _editors;

    public SettingEditorFactory(IEnumerable<ISettingEditor> editors)
    {
        ArgumentNullException.ThrowIfNull(editors);

        var all = editors.ToList();
        if (!all.Any(editor => editor is TextSettingEditor))
            throw new InvalidOperationException("The setting editors must include the text editor, which handles every setting.");

        _editors = [.. all.Where(editor => editor is not TextSettingEditor), .. all.Where(editor => editor is TextSettingEditor)];
    }

    public SettingField Create(SettingEditorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var field = _editors.First(editor => editor.Handles(context.Definition)).Create(context);
        field.Refresh();

        return field;
    }
}

public sealed class SwitchSettingEditor : ISettingEditor
{
    public bool Handles(SettingDefinition definition) => definition.Kind == SettingKind.Boolean;

    public SettingField Create(SettingEditorContext context) => new Field(context);

    private sealed class Field : SettingField
    {
        private readonly SwitchToggle _switch = new() { VerticalAlignment = VerticalAlignment.Center };

        public Field(SettingEditorContext context) : base(context) =>
            _switch.Toggled += (_, _) => Commit(_switch.IsOn);

        public override Control View => _switch;

        protected override void Show() => _switch.IsOn = Context.Settings.Get<bool>(Definition.Key);
    }
}

public sealed record SettingChoiceItem(string Value, string Title, string? Description);

public abstract class ChoiceFieldBase : SettingField
{
    private readonly ComboBox _combo;
    private IReadOnlyList<SettingChoiceItem> _items = [];

    protected ChoiceFieldBase(SettingEditorContext context) : base(context)
    {
        _combo = new ComboBox
        {
            Width = context.FieldWidth,
            VerticalAlignment = VerticalAlignment.Center,
            ItemTemplate = new FuncDataTemplate<SettingChoiceItem>((item, _) => Item(item), supportsRecycling: false),
            SelectionBoxItemTemplate = new FuncDataTemplate<SettingChoiceItem>((item, _) => new TextBlock
            {
                Text = item?.Title,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            }, supportsRecycling: false),
        };

        _combo.SelectionChanged += (_, _) =>
        {
            if (_combo.SelectedItem is SettingChoiceItem item) Commit(item.Value);
        };
    }

    public override Control View => _combo;

    protected void Fill(IReadOnlyList<SettingChoiceItem> items)
    {
        _items = items;
        _combo.ItemsSource = items;
    }

    protected override void Show()
    {
        var current = Context.Settings.Get<object>(Definition.Key)?.ToString();
        _combo.SelectedItem = _items.FirstOrDefault(item => item.Value == current);
    }

    private Control Item(SettingChoiceItem? item)
    {
        var stack = new StackPanel { Spacing = 1, Margin = new Thickness(0, 2) };
        if (item is null) return stack;

        stack.Children.Add(new TextBlock { Text = item.Title });

        if (item.Description is { } description)
            stack.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = Context.Metrics.FontSizeSmall,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 280,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        return stack;
    }
}

public sealed class ChoiceSettingEditor : ISettingEditor
{
    public bool Handles(SettingDefinition definition) => definition.Kind == SettingKind.Choice;

    public SettingField Create(SettingEditorContext context) => new Field(context);

    private sealed class Field : ChoiceFieldBase
    {
        public Field(SettingEditorContext context) : base(context) =>
            Fill([.. context.Definition.Choices.Select(choice => new SettingChoiceItem(
                choice.Value,
                context.Text(context.Definition.ChoiceKey(choice.Value)),
                context.Optional(context.Definition.ChoiceDescriptionKey(choice.Value))))]);
    }
}

public sealed class OptionSourceSettingEditor(ISettingOptionSources sources) : ISettingEditor
{
    private readonly ISettingOptionSources _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    public bool Handles(SettingDefinition definition) => _sources.Find(definition.OptionsSource) is not null;

    public SettingField Create(SettingEditorContext context) =>
        new Field(context, _sources.Find(context.Definition.OptionsSource)!);

    private sealed class Field : ChoiceFieldBase
    {
        private string? _hint;

        public Field(SettingEditorContext context, ISettingOptionSource source) : base(context)
        {
            Fill(Items([]));
            _ = LoadAsync(source);
        }

        public override string? Hint => _hint;

        private async Task LoadAsync(ISettingOptionSource source)
        {
            IReadOnlyList<SettingOption> options;

            try
            {
                options = await source.LoadAsync().ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                options = [];
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _hint = options.Count == 0 ? Context.Text("settings.options.none") : null;
                Fill(Items(options));
                Refresh();
            });
        }

        private List<SettingChoiceItem> Items(IReadOnlyList<SettingOption> options)
        {
            var items = new List<SettingChoiceItem> { new(string.Empty, Context.Text("settings.option.none"), null) };
            items.AddRange(options.Where(option => option.Value.Length > 0)
                .Select(option => new SettingChoiceItem(option.Value, option.Label, null)));

            var current = Context.Settings.Get<object>(Definition.Key)?.ToString() ?? string.Empty;
            if (items.All(item => item.Value != current))
                items.Add(new SettingChoiceItem(current, Context.Texts.Format("settings.option.unavailable", current), null));

            return items;
        }
    }
}

public sealed class NumberSettingEditor : ISettingEditor
{
    public bool Handles(SettingDefinition definition) =>
        definition.Kind is SettingKind.Integer or SettingKind.Decimal;

    public SettingField Create(SettingEditorContext context) => new Field(context);

    private sealed class Field : SettingField
    {
        private readonly bool _integer;
        private readonly NumberBox _box;

        public Field(SettingEditorContext context) : base(context)
        {
            _integer = context.Definition.Kind == SettingKind.Integer;
            var entry = new NumberEntry(
                new NumericBounds(
                    context.Definition.Minimum ?? (_integer ? int.MinValue : double.MinValue),
                    context.Definition.Maximum ?? (_integer ? int.MaxValue : double.MaxValue),
                    _integer ? 1 : 0.1),
                _integer,
                CultureInfo.CurrentCulture);

            _box = new NumberBox(entry, Current(), context.Metrics, context.Icons, context.Optional(context.Definition.UnitKey))
            {
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            _box.ValueCommitted += (_, value) =>
            {
                Commit(_integer ? (object)(long)value : value);
                Refresh();
            };
        }

        public override Control View => _box;

        public override string? Hint =>
            Definition.Minimum is { } min && Definition.Maximum is { } max
                ? Context.Texts.Format("settings.range", Format(min), Format(max))
                : null;

        protected override void Show() => _box.Value = Current();

        private double Current() => _integer
            ? Context.Settings.Get<long>(Definition.Key)
            : Context.Settings.Get<double>(Definition.Key);

        private string Format(double value) => value.ToString(_integer ? "0" : "0.##", CultureInfo.CurrentCulture);
    }
}

public sealed class ColorSettingEditor : ISettingEditor
{
    public bool Handles(SettingDefinition definition) => definition.Kind == SettingKind.Color;

    public SettingField Create(SettingEditorContext context) => new Field(context);

    private sealed class Field : SettingField
    {
        private readonly Button _button;
        private readonly Border _swatch;
        private readonly TextBlock _code = new() { VerticalAlignment = VerticalAlignment.Center };

        public Field(SettingEditorContext context) : base(context)
        {
            _swatch = new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new CornerRadius(context.Metrics.RadiusTight),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
            }.Themed(Border.BorderBrushProperty, Palette.Keys.BorderStrong);

            _button = new Button
            {
                Width = context.FieldWidth,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = context.Metrics.SpacingSmall,
                    Children = { _swatch, _code },
                },
            };

            _button.Click += (_, _) => Pick();
        }

        public override Control View => _button;

        protected override void Show()
        {
            var value = Context.Settings.Get<string>(Definition.Key);
            _code.Text = value;
            _swatch.Background = Color.TryParse(value, out var colour) ? new SolidColorBrush(colour) : Brushes.Transparent;
        }

        private void Pick()
        {
            var picker = new ColorPickerView(Context.Settings.Get<string>(Definition.Key), 1, Context.Texts);
            picker.Changed += (_, _) => Commit(picker.Value);

            var flyout = new Flyout { Content = picker, Placement = PlacementMode.BottomEdgeAlignedRight };
            flyout.Closed += (_, _) => Refresh();
            flyout.ShowAt(_button);
        }
    }
}

public sealed class PathSettingEditor : ISettingEditor
{
    public bool Handles(SettingDefinition definition) => definition.Kind == SettingKind.Path;

    public SettingField Create(SettingEditorContext context) => new Field(context);

    private sealed class Field : SettingField
    {
        private readonly TextBox _box = new() { VerticalContentAlignment = VerticalAlignment.Center };
        private readonly Grid _grid = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        public Field(SettingEditorContext context) : base(context)
        {
            _grid.Width = context.FieldWidth;
            _box.LostFocus += (_, _) => Commit((_box.Text ?? string.Empty).Trim());
            _box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) Commit((_box.Text ?? string.Empty).Trim());
                else if (e.Key == Key.Escape) Refresh();
                else return;

                e.Handled = true;
            };

            var browse = new Button
            {
                Content = context.Icons.Find("ui.folder") is { } geometry ? new IconView(geometry, 14) : "…",
                Margin = new Thickness(context.Metrics.SpacingSmall, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            ToolTip.SetTip(browse, context.Text("settings.browse"));
            Avalonia.Automation.AutomationProperties.SetName(browse, context.Text("settings.browse"));
            browse.Click += async (_, _) => await BrowseAsync().ConfigureAwait(true);

            Grid.SetColumn(browse, 1);
            _grid.Children.Add(_box);
            _grid.Children.Add(browse);
        }

        public override Control View => _grid;

        protected override void Show() => _box.Text = Context.Settings.Get<string>(Definition.Key);

        private async Task BrowseAsync()
        {
            if (TopLevel.GetTopLevel(_grid)?.StorageProvider is not { CanOpen: true } storage) return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false }).ConfigureAwait(true);
            if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path) return;

            Commit(path);
            Refresh();
        }
    }
}

public sealed class TextSettingEditor : ISettingEditor
{
    public bool Handles(SettingDefinition definition) => true;

    public SettingField Create(SettingEditorContext context) => new Field(context);

    private sealed class Field : SettingField
    {
        private readonly TextBox _box = new() { VerticalContentAlignment = VerticalAlignment.Center };

        public Field(SettingEditorContext context) : base(context)
        {
            _box.Width = context.FieldWidth;
            _box.VerticalAlignment = VerticalAlignment.Center;
            _box.LostFocus += (_, _) => Commit(_box.Text ?? string.Empty);
            _box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) Commit(_box.Text ?? string.Empty);
                else if (e.Key == Key.Escape) Refresh();
                else return;

                e.Handled = true;
            };
        }

        public override Control View => _box;

        protected override void Show() => _box.Text = Context.Settings.Get<object>(Definition.Key)?.ToString();
    }
}
