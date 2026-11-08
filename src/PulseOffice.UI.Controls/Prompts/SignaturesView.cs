using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Prompts;

public enum SignatureHealth { Valid, Warning, Broken }

public sealed record SignatureRow(string Signer, string Issuer, DateTimeOffset SignedOn, DateTimeOffset NotAfter, SignatureHealth Health, string StatusText);

public enum SignatureRequestKind { Sign, RemoveAll }

public sealed record SignatureRequest(SignatureRequestKind Kind, int Certificate, string File, string Password);

public sealed class SignaturesView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly ComboBox _certificates;
    private readonly TextBox _file;
    private readonly TextBox _password = new() { PasswordChar = '•' };
    private readonly RadioButton _installed;
    private readonly Func<IReadOnlyList<string>> _names;
    private bool _loaded;

    public SignaturesView(IReadOnlyList<SignatureRow> rows, Func<IReadOnlyList<string>> certificates, Func<Task<string?>> browse, ITextCatalog texts,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(browse);
        ArgumentNullException.ThrowIfNull(texts);
        _names = certificates ?? throw new ArgumentNullException(nameof(certificates));
        var metrics = DesignTokens.For(density);

        var layout = new TwoPaneDialogLayout(metrics) { Header = new HelpTip(texts["signature.view.explanation"], texts, metrics) };

        var present = layout.AddSection(texts["signature.view.present"]);
        if (rows.Count == 0)
        {
            present.Add(null, new TextBlock { Text = texts["signature.none"], FontSize = metrics.FontSizeSmall }
                .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary), DialogRowKind.Wide);
        }
        else
        {
            var list = new StackPanel { Spacing = 2, Margin = new Thickness(metrics.SpacingSmall) };
            foreach (var row in rows) list.Children.Add(Row(row, metrics, texts));
            present.Add(null, new PanelListFrame(metrics, list), DialogRowKind.Wide, editorWidth: 520);
        }

        var sign = layout.AddSection(texts["signature.view.add"]);
        _installed = new RadioButton { Content = texts["signature.source.installed"], GroupName = "signatureSource", IsChecked = true };
        var fromFile = new RadioButton { Content = texts["signature.source.file"], GroupName = "signatureSource" };
        var sources = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingLarge, Children = { _installed, fromFile } };
        sign.Add(texts["signature.source"], sources);

        _certificates = new ComboBox { PlaceholderText = texts["signature.certificate.placeholder"], HorizontalAlignment = HorizontalAlignment.Stretch };
        _certificates.DropDownOpened += (_, _) => Load();
        var certificateRow = sign.Add(texts["signature.certificate"], _certificates);

        _file = new TextBox { PlaceholderText = texts["signature.fromFile.placeholder"] };
        var choose = new Button { Content = texts["signature.fromFile.browse"], VerticalAlignment = VerticalAlignment.Center };
        choose.Click += async (_, _) =>
        {
            choose.IsEnabled = false;
            try
            {
                if (await browse().ConfigureAwait(true) is { Length: > 0 } path) _file.Text = path;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or InvalidOperationException or NotSupportedException or OperationCanceledException)
            {
            }
            finally
            {
                choose.IsEnabled = true;
            }
        };
        var picker = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = metrics.SpacingSmall };
        Grid.SetColumn(choose, 1);
        picker.Children.Add(_file);
        picker.Children.Add(choose);
        var fileRow = sign.Add(texts["signature.fromFile"], picker);

        void Show()
        {
            var installed = _installed.IsChecked == true;
            certificateRow.IsVisible = installed;
            fileRow.IsVisible = !installed;
        }

        _installed.IsCheckedChanged += (_, _) => Show();
        Show();

        sign.Add(texts["signature.password"], _password);

        Content = layout;

        var footer = new DialogFooter(density).WithCancel(texts["prompt.close"], () => Completed?.Invoke(this, null));
        if (rows.Count > 0) footer = footer.WithAction(texts["signature.removeAll"], () => Completed?.Invoke(this, new SignatureRequest(SignatureRequestKind.RemoveAll, -1, string.Empty, string.Empty)));
        Footer = footer.WithAction(texts["signature.add"], Sign, primary: true);
    }

    private void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _certificates.ItemsSource = _names();
        if (_certificates.ItemCount > 0 && _certificates.SelectedIndex < 0) _certificates.SelectedIndex = 0;
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public void Sign()
    {
        var installed = _installed.IsChecked == true;
        Completed?.Invoke(this, new SignatureRequest(
            SignatureRequestKind.Sign,
            installed ? _certificates.SelectedIndex : -1,
            installed ? string.Empty : _file.Text?.Trim() ?? string.Empty,
            _password.Text ?? string.Empty));
    }

    private Control Row(SignatureRow row, DesignTokens.Metrics metrics, ITextCatalog texts)
    {
        var colour = row.Health switch
        {
            SignatureHealth.Valid => Palette.Keys.Success,
            SignatureHealth.Warning => Palette.Keys.Warning,
            _ => Palette.Keys.Danger,
        };
        var badge = new Ellipse { Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) }
            .Themed(Shape.FillProperty, colour);

        var text = new StackPanel { Spacing = 1 };
        text.Children.Add(new TextBlock { Text = row.Signer, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = row.StatusText, FontSize = metrics.FontSizeSmall }.Themed(TextBlock.ForegroundProperty, colour));
        text.Children.Add(new TextBlock
        {
            Text = texts.Format("signature.view.details",
                row.SignedOn.LocalDateTime.ToString("g", CultureInfo.CurrentCulture), row.Issuer,
                row.NotAfter.LocalDateTime.ToString("d", CultureInfo.CurrentCulture)),
            FontSize = metrics.FontSizeSmall,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = metrics.SpacingMedium, Margin = new Thickness(metrics.SpacingSmall) };
        Grid.SetColumn(text, 1);
        grid.Children.Add(badge);
        grid.Children.Add(text);
        AutomationProperties.SetName(grid, $"{row.Signer}: {row.StatusText}");
        return grid;
    }
}
