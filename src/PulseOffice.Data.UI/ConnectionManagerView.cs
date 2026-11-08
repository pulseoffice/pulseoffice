using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Data.UI;

public sealed class ConnectionManagerView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly IConnectionDirectory _connections;
    private readonly DatabaseProviderRegistry _providers;
    private readonly IIconCatalog? _icons;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly IDialogParts _parts;
    private readonly IReadOnlyList<DatabaseProviderKind> _kinds;

    private readonly IDatabaseProviderNames _providerNames;
    private readonly IConnectionIds _ids;

    private readonly ListBox _list = new();
    private readonly TextBox _name = new();
    private readonly ComboBox _provider = new();

    private readonly IReadOnlyDictionary<DatabaseProviderKind, string> _examples = new Dictionary<DatabaseProviderKind, string>
    {
        [DatabaseProviderKind.Odbc] = "DSN={0}",
        [DatabaseProviderKind.SqlServer] = "Server=localhost;Database={0};TrustServerCertificate=True",
        [DatabaseProviderKind.PostgreSql] = "Host=localhost;Port=5432;Database={0}",
        [DatabaseProviderKind.MySql] = "Server=localhost;Port=3306;Database={0}",
        [DatabaseProviderKind.Sqlite] = "Data Source={0}.db",
    };

    private string Hint()
    {
        var kind = _provider.SelectedIndex >= 0 && _provider.SelectedIndex < _kinds.Count ? _kinds[_provider.SelectedIndex] : DatabaseProviderKind.PostgreSql;
        var example = string.Format(System.Globalization.CultureInfo.InvariantCulture, _examples[kind], _texts["data.connections.string.database"]);
        return string.Format(System.Globalization.CultureInfo.CurrentCulture, _texts["data.connections.string.hint"], example);
    }
    private readonly TextBox _connectionString = new();
    private readonly TextBox _user = new();
    private readonly TextBox _password = new() { PasswordChar = '•' };
    private readonly CheckBox _secretInFile = new();
    private readonly TextBlock _secretWarning = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _outcome = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _caption = new();

    private Border? _warningStrip;
    private string? _editing;

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; private set; }

    public ConnectionManagerView(
        IConnectionDirectory connections,
        DatabaseProviderRegistry providers,
        ITextCatalog texts,
        IIconCatalog? icons = null,
        InterfaceDensity density = InterfaceDensity.Compact,
        IDatabaseProviderNames? providerNames = null,
        IConnectionIds? ids = null)
    {
        _providerNames = providerNames ?? new DatabaseProviderNames();
        _ids = ids ?? new ConnectionIds();
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _icons = icons;
        _metrics = DesignTokens.For(density);
        _parts = new DialogParts(_metrics, icons);
        _kinds = [.. Enum.GetValues<DatabaseProviderKind>().Where(_providers.IsAvailable)];

        Content = Build();

        Edit(null);
        Reload();
    }

    private Control Build()
    {
        Footer = new DialogFooter()
            .WithCancel(_texts["data.connections.close"], () => Completed?.Invoke(this, null));

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("200,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            ColumnSpacing = _metrics.SpacingMedium,
            Height = 340,
        };

        var sidebar = Sidebar();
        Grid.SetColumn(sidebar, 0);
        Grid.SetRowSpan(sidebar, 2);

        var editor = Editor();
        Grid.SetColumn(editor, 1);

        _outcome.FontSize = _metrics.FontSizeSmall;
        _outcome.Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0);
        _outcome.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        Grid.SetColumn(_outcome, 1);
        Grid.SetRow(_outcome, 1);

        layout.Children.Add(sidebar);
        layout.Children.Add(editor);
        layout.Children.Add(_outcome);

        return layout;
    }

    private Control Sidebar()
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };

        var caption = new TextBlock
        {
            Text = _texts["data.connections.list"].ToUpperInvariant(),
            FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
            LetterSpacing = 0.6,
            Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        _list.SelectionChanged += (_, _) => Edit(Selected());
        _list.Classes.Add("compact");

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
            Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0),
        };

        actions.Children.Add(_parts.Quiet(_texts["data.connections.new"], "ui.add", () => Edit(null)));
        actions.Children.Add(_parts.Quiet(_texts["data.connections.remove"], "ui.close", () => _ = AttemptAsync(RemoveAsync)));

        Grid.SetRow(caption, 0);
        Grid.SetRow(_list, 1);
        Grid.SetRow(actions, 2);

        layout.Children.Add(caption);
        layout.Children.Add(_list);
        layout.Children.Add(actions);

        return layout;
    }

    private Control Editor()
    {
        var layout = new StackPanel { Spacing = _metrics.SpacingMedium };

        _caption.FontWeight = FontWeight.SemiBold;
        _caption.FontSize = _metrics.FontSize;

        _provider.ItemsSource = _kinds.Select(_providerNames.Of).ToList();
        _provider.SelectedIndex = _kinds.Count > 0 ? 0 : -1;
        _provider.HorizontalAlignment = HorizontalAlignment.Stretch;

        _connectionString.PlaceholderText = Hint();
        _provider.SelectionChanged += (_, _) => _connectionString.PlaceholderText = Hint();

        var fields = new StackPanel { Spacing = _metrics.SpacingSmall };

        fields.Children.Add(_parts.Field(_texts["data.connections.name"], _name));
        fields.Children.Add(_parts.Field(_texts["data.connections.provider"], _provider));
        fields.Children.Add(_parts.Field(_texts["data.connections.string"], _connectionString));

        var credentials = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = _metrics.SpacingMedium,
        };

        var user = _parts.Field(_texts["data.connections.user"], _user);
        var password = _parts.Field(_texts["data.connections.password"], _password);

        Grid.SetColumn(user, 0);
        Grid.SetColumn(password, 1);

        credentials.Children.Add(user);
        credentials.Children.Add(password);

        fields.Children.Add(credentials);

        _secretInFile.Content = _texts["data.connections.infile"];
        _secretInFile.IsCheckedChanged += (_, _) => UpdateWarning();

        _secretWarning.FontSize = _metrics.FontSizeSmall;
        _secretWarning.Text = _texts["data.connections.infile.warning"];
        _secretWarning.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);

        _warningStrip = new Border
        {
            Child = _secretWarning,
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            IsVisible = false,
        };

        _warningStrip.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
        };

        actions.Children.Add(_parts.Accented(_texts["data.connections.save"], () => _ = AttemptAsync(SaveAsync)));
        actions.Children.Add(_parts.Quiet(_texts["data.connections.test"], "ui.check", () => _ = AttemptAsync(TestAsync)));

        layout.Children.Add(_caption);
        layout.Children.Add(fields);
        layout.Children.Add(_secretInFile);
        layout.Children.Add(_warningStrip);
        layout.Children.Add(actions);

        return layout;
    }

    private ConnectionDescriptor? Selected() =>
        _list.SelectedIndex >= 0 && _list.SelectedIndex < _connections.All.Count
            ? _connections.All[_list.SelectedIndex]
            : null;

    private void Edit(ConnectionDescriptor? connection)
    {
        _editing = connection?.Id;
        _caption.Text = connection?.DisplayName ?? _texts["data.connections.newTitle"];
        _name.Text = connection?.DisplayName ?? string.Empty;
        _connectionString.Text = connection?.ConnectionString ?? string.Empty;
        _user.Text = connection?.UserName ?? string.Empty;
        _password.Text = string.Empty;
        _secretInFile.IsChecked = connection?.SecretInFile ?? false;

        UpdateWarning();

        _outcome.Text = connection is null ? string.Empty : _texts["data.connections.secretkept"];

        if (connection is not null)
        {
            var index = _kinds.ToList().IndexOf(connection.Provider);
            if (index >= 0) _provider.SelectedIndex = index;
        }
    }

    private void Reload()
    {
        _list.ItemsSource = _connections.All.Select(connection => _parts.ListRow("ledger.data.provider", connection.DisplayName, _providerNames.Of(connection.Provider))).ToList();

        if (_connections.All.Count == 0) _outcome.Text = _texts["data.connections.none"];
        if (_kinds.Count == 0) _outcome.Text = _texts["data.connections.noprovider"];
    }

    private async Task SaveAsync()
    {
        if (_name.Text is not { Length: > 0 } name || _provider.SelectedIndex < 0) return;

        var id = _editing ?? _ids.NewId(name, _connections.All.Select(known => known.Id));

        await _connections.SaveAsync(
            new ConnectionDescriptor
            {
                Id = id,
                DisplayName = name,
                Provider = _kinds[_provider.SelectedIndex],
                ConnectionString = _connectionString.Text ?? string.Empty,
                UserName = _user.Text is { Length: > 0 } user ? user : null,
                CredentialKey = _connections.Find(id)?.CredentialKey,
                SecretInFile = _secretInFile.IsChecked ?? false,
            },
            _password.Text).ConfigureAwait(true);

        _editing = id;
        _password.Text = string.Empty;
        _caption.Text = name;

        Reload();

        _list.SelectedIndex = _connections.All.ToList().FindIndex(known => known.Id == id);

        _outcome.Text = _secretInFile.IsChecked ?? false
            ? _texts["data.connections.saved.infile"]
            : _texts["data.connections.saved"];
    }

    private void UpdateWarning()
    {
        if (_warningStrip is not null) _warningStrip.IsVisible = _secretInFile.IsChecked ?? false;
    }

    private async Task RemoveAsync()
    {
        if (Selected() is not { } connection) return;

        await _connections.RemoveAsync(connection.Id).ConfigureAwait(true);

        Edit(null);
        Reload();
    }

    private async Task TestAsync()
    {
        if (_provider.SelectedIndex < 0 || _name.Text is not { Length: > 0 }) return;

        await SaveAsync().ConfigureAwait(true);

        if (_editing is not { } id || _connections.Find(id) is not { } descriptor) return;

        _outcome.Text = _texts["data.connections.testing"];

        var reachable = await _providers.Get(descriptor.Provider)
            .TestConnectionAsync(descriptor, CancellationToken.None).ConfigureAwait(true);

        _outcome.Text = reachable ? _texts["data.connections.ok"] : _texts["data.connections.failed"];
    }

    private async Task AttemptAsync(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (Exception failure) when (failure is not OutOfMemoryException and not OperationCanceledException)
        {
            _outcome.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, _texts["data.connections.error"], failure.Message);
        }
    }
}
