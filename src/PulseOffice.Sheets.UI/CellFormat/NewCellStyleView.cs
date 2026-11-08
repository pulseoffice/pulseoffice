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

public sealed class NewCellStyleView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly TextBox _name;
    private readonly TextBlock _message;
    private readonly CellStyleThumbnail _preview;
    private readonly Func<string, bool> _exists;
    private readonly ITextCatalog _texts;
    private readonly DialogFooter _footer;

    public NewCellStyleView(CellStyle style, string initialName, Func<string, bool> exists, ITextCatalog texts)
    {
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
        Completed?.Invoke(this, name);
    }
}
