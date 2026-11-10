using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.CellFormat;

public sealed record NewCellStyleInheritance(CellStyle Inherited, CellStyleProperties Explicit, ICellStyleLayering Layering);

public sealed record NewCellStyleChoice(string Name, CellStyle Style, CellStyleProperties? Explicit);

public sealed class NewCellStyleView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const int Inherit = 0;
    private const int Own = 1;
    private const int Off = 2;

    private readonly CellStyle _basis;
    private readonly NewCellStyleInheritance? _inheritance;
    private readonly List<(CellStyleProperties Property, ComboBox Choice)> _choices = [];

    private readonly TextBox _name;
    private readonly TextBlock _message;
    private readonly CellStyleThumbnail _preview;
    private readonly Func<string, bool> _exists;
    private readonly ITextCatalog _texts;
    private readonly DialogFooter _footer;

    public NewCellStyleView(CellStyle style, string initialName, Func<string, bool> exists, ITextCatalog texts, NewCellStyleInheritance? inheritance = null)
    {
        _basis = style ?? throw new ArgumentNullException(nameof(style));
        _inheritance = inheritance;
        _exists = exists ?? throw new ArgumentNullException(nameof(exists));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        var metrics = DesignTokens.For(InterfaceDensity.Compact);

        _preview = new CellStyleThumbnail(style, Sample(initialName)) { Width = 180, Height = 44 };
        AutomationProperties.SetName(_preview, texts["format.preview"]);

        _name = new TextBox { Text = initialName, PlaceholderText = texts["style.name"] };
        AutomationProperties.SetName(_name, texts["style.name"]);
        _name.TextChanged += (_, _) =>
        {
            _preview.InvalidateVisual();
            Check();
        };
        _name.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Confirm();
            e.Handled = true;
        };

        _message = new TextBlock { FontSize = metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap, IsVisible = false }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.Warning);

        var layout = new TwoPaneDialogLayout(metrics) { Explanation = texts["style.create.explanation"], Preview = _preview };
        var section = layout.AddSection((string?)null);
        section.Add(texts["style.name"], _name, editorWidth: 240);
        section.Add(null, _message, DialogRowKind.Indented, editorWidth: 240);
        if (inheritance is not null) AddInheritance(layout.AddSection(texts["style.inherit.section"]), inheritance);

        _footer = new DialogFooter()
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], Confirm, primary: true);

        Loaded += (_, _) =>
        {
            _name.Focus();
            _name.SelectAll();
        };
        Content = layout;
        Check();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer => _footer;

    public TextBox NameBox => _name;

    public string? Message => _message.IsVisible ? _message.Text : null;

    public IReadOnlyList<(CellStyleProperties Property, ComboBox Choice)> InheritanceChoices => _choices;

    public NewCellStyleChoice Choice(string name)
    {
        if (_inheritance is not { } inheritance) return new NewCellStyleChoice(name, _basis, null);

        var style = _basis;
        var declared = CellStyleProperties.None;
        foreach (var (property, choice) in _choices)
        {
            if (choice.SelectedIndex == Inherit) continue;
            declared |= property;
            if (inheritance.Layering.IsFlag(property)) style = inheritance.Layering.WithFlag(style, property, choice.SelectedIndex == Own);
        }
        return new NewCellStyleChoice(name, style, declared);
    }

    private void AddInheritance(DialogSection section, NewCellStyleInheritance inheritance)
    {
        foreach (var property in inheritance.Layering.Properties)
        {
            var flag = inheritance.Layering.IsFlag(property);
            string[] items = flag
                ? [Inherited(inheritance, property), _texts["style.inherit.on"], _texts["style.inherit.off"]]
                : [_texts["style.inherit.inherited"], _texts["style.inherit.own"]];
            var selected = (inheritance.Explicit & property) == 0 ? Inherit
                : flag && !inheritance.Layering.Flag(_basis, property) ? Off
                : Own;
            var choice = new ComboBox { ItemsSource = items, SelectedIndex = selected, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
            var label = _texts[$"style.property.{property}"];
            AutomationProperties.SetName(choice, label);
            _choices.Add((property, choice));
            section.Add(label, choice, editorWidth: 200);
        }
    }

    private string Inherited(NewCellStyleInheritance inheritance, CellStyleProperties property) =>
        _texts.Format("style.inherit.inheritedValue",
            inheritance.Layering.Flag(inheritance.Inherited, property) ? _texts["style.inherit.on"] : _texts["style.inherit.off"]);

    private string Sample(string name) => name.Trim().Length > 0 ? name.Trim() : _texts["conditional.sample"];

    private void Check()
    {
        var name = (_name.Text ?? string.Empty).Trim();
        _preview.Sample = Sample(name);
        _message.IsVisible = name.Length > 0 && _exists(name);
        _message.Text = _texts.Format("style.create.replaces", name);
    }

    private void Confirm()
    {
        var name = (_name.Text ?? string.Empty).Trim();
        if (name.Length == 0) return;
        Completed?.Invoke(this, Choice(name));
    }
}
