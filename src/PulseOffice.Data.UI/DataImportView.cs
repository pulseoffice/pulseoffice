using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Document.Data;
using PulseOffice.Document.Embedding;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Data.UI;

public sealed record DataImportChoice(
    string DisplayName, DataQuerySource Query, DataSnapshot Preview, bool IncludeHeaders);

public sealed class DataImportView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly IConnectionDirectory _connections;
    private readonly DatabaseProviderRegistry _providers;
    private readonly IDataTableLoader _loader;
    private readonly ITextCatalog _texts;
    private readonly IDiagnosticTexts _diagnostics;
    private readonly DesignTokens.Metrics _metrics;
    private readonly IDialogParts _parts;

    private readonly ListBox _connection = new();
    private readonly IIconCatalog? _icons;
    private readonly IDatabaseProviderNames _providerNames;
    private readonly ListBox _tables = new();
    private readonly TextBox _sql = new() { AcceptsReturn = true, Height = 76 };
    private readonly CheckBox _headers = new() { IsChecked = true };
    private readonly TextBlock _outcome = new() { TextWrapping = TextWrapping.Wrap };
    private readonly DataPreviewControl _preview;

    private DataSnapshot _snapshot = DataSnapshot.Empty;
    private (string Connection, string Sql)? _previewed;
    private CancellationTokenSource? _listing;

    public event EventHandler<object?>? Completed;

    public Avalonia.Controls.Control? Footer { get; private set; }

    private readonly string _confirmKey;

    public DataImportView(
        IConnectionDirectory connections,
        DatabaseProviderRegistry providers,
        IDataTableLoader loader,
        ITextCatalog texts,
        IDiagnosticTexts diagnostics,
        IIconCatalog? icons = null,
        InterfaceDensity density = InterfaceDensity.Compact,
        string confirmKey = "data.import.insert",
        IDatabaseProviderNames? providerNames = null)
    {
        _confirmKey = confirmKey;
        _providerNames = providerNames ?? new DatabaseProviderNames();
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _icons = icons;
        _metrics = DesignTokens.For(density);
        _parts = new DialogParts(_metrics, icons);
        _preview = new DataPreviewControl(new DataTableRenderer(texts));

        Content = Build();
        LoadConnections();
    }

    private Control Build()
    {
        Footer = new DialogFooter()
            .WithCancel(_texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(_texts[_confirmKey], Confirm, primary: true);

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("200,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            ColumnSpacing = _metrics.SpacingMedium,
            Height = 420,
        };

        var sidebar = Sidebar();
        Grid.SetColumn(sidebar, 0);
        Grid.SetRowSpan(sidebar, 2);

        var query = QueryArea();
        Grid.SetColumn(query, 1);

        _outcome.FontSize = _metrics.FontSizeSmall;
        _outcome.Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0);
        _outcome.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        Grid.SetColumn(_outcome, 1);
        Grid.SetRow(_outcome, 1);

        layout.Children.Add(sidebar);
        layout.Children.Add(query);
        layout.Children.Add(_outcome);

        return layout;
    }

    private Control Sidebar()
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };

        var caption = _parts.Caption(_texts["data.connections.list"]);

        _connection.Classes.Add("compact");
        _connection.SelectionChanged += async (_, _) => await ListTablesAsync().ConfigureAwait(true);

        Grid.SetRow(caption, 0);
        Grid.SetRow(_connection, 1);

        layout.Children.Add(caption);
        layout.Children.Add(_connection);

        return layout;
    }

    private Control QueryArea()
    {
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,120,Auto,Auto,Auto,Auto,*"),
        };

        var tables = _parts.Caption(_texts["data.import.tables"]);

        _tables.Classes.Add("compact");
        _tables.SelectionChanged += (_, _) =>
        {
            if (_tables.SelectedItem is string table)
                _sql.Text = $"SELECT * FROM {Quoted(table)}";
        };

        var sqlCaption = _parts.Caption(_texts["data.import.sql"]);
        sqlCaption.Margin = new Thickness(0, _metrics.SpacingMedium, 0, _metrics.SpacingSmall / 2);

        _sql.FontFamily = new FontFamily("Menlo, Consolas, monospace");
        _sql.FontSize = _metrics.FontSize;

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingMedium,
            Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0),
        };

        _headers.Content = _texts["data.import.headers"];
        _headers.VerticalAlignment = VerticalAlignment.Center;

        actions.Children.Add(_parts.Accented(_texts["data.import.load"], () => _ = GuardedPreviewAsync()));
        actions.Children.Add(_headers);

        var previewCaption = _parts.Caption(_texts["data.import.preview"]);
        previewCaption.Margin = new Thickness(0, _metrics.SpacingMedium, 0, _metrics.SpacingSmall / 2);

        var plate = new Border
        {
            Child = _preview,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
        };

        plate.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        plate.Background = Brushes.White;
        plate.ClipToBounds = true;

        Grid.SetRow(tables, 0);
        Grid.SetRow(_tables, 1);
        Grid.SetRow(sqlCaption, 2);
        Grid.SetRow(_sql, 3);
        Grid.SetRow(actions, 4);
        Grid.SetRow(previewCaption, 5);
        Grid.SetRow(plate, 6);

        layout.Children.Add(tables);
        layout.Children.Add(_tables);
        layout.Children.Add(sqlCaption);
        layout.Children.Add(_sql);
        layout.Children.Add(actions);
        layout.Children.Add(previewCaption);
        layout.Children.Add(plate);

        return layout;
    }

    private void LoadConnections()
    {
        _connection.ItemsSource = _connections.All.Select(connection => _parts.ListRow("ledger.data.provider", connection.DisplayName, _providerNames.Of(connection.Provider))).ToList();

        if (_connections.All.Count == 0)
        {
            _outcome.Text = _texts["data.connections.none"];
            return;
        }

        _connection.SelectedIndex = 0;
    }

    private ConnectionDescriptor? Chosen() =>
        _connection.SelectedIndex >= 0 && _connection.SelectedIndex < _connections.All.Count
            ? _connections.All[_connection.SelectedIndex]
            : null;

    private string Quoted(string table)
    {
        if (Chosen() is not { } connection || !_providers.IsAvailable(connection.Provider)) return table;

        var provider = _providers.Get(connection.Provider);
        return provider.QuoteListedTable(table);
    }

    private async Task ListTablesAsync()
    {
        _tables.ItemsSource = null;
        _listing?.Cancel();

        if (Chosen() is not { } connection || !_providers.IsAvailable(connection.Provider)) return;

        using var listing = new CancellationTokenSource();
        _listing = listing;

        try
        {
            var tables = await _providers.Get(connection.Provider)
                .ListTablesAsync(connection, listing.Token).ConfigureAwait(true);

            if (listing.IsCancellationRequested) return;

            _tables.ItemsSource = tables;
            _outcome.Text = string.Empty;
        }
        catch (OperationCanceledException) when (listing.IsCancellationRequested)
        {
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            if (!listing.IsCancellationRequested) _outcome.Text = failure.Message;
        }
        finally
        {
            if (ReferenceEquals(_listing, listing)) _listing = null;
        }
    }

    private async Task GuardedPreviewAsync()
    {
        try
        {
            await PreviewAsync().ConfigureAwait(true);
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            _snapshot = DataSnapshot.Empty;
            _previewed = null;
            _preview.Show(DataSnapshot.Empty);
            _outcome.Text = $"{_texts["data.import.failed"]} {failure.Message}";
        }
    }

    private async Task PreviewAsync()
    {
        if (Chosen() is not { } connection) return;
        if (_sql.Text is not { Length: > 0 } sql) return;

        _outcome.Text = _texts["data.import.loading"];

        var loaded = await _loader.LoadAsync(
            new DataQuerySource { ConnectionId = connection.Id, Sql = sql, MaxRows = 200 }).ConfigureAwait(true);

        if (!loaded.Succeeded)
        {
            _snapshot = DataSnapshot.Empty;
            _previewed = null;
            _preview.Show(DataSnapshot.Empty);
            _outcome.Text = loaded.Diagnostics.FirstOrDefault() is { } diagnostic ? _diagnostics.Describe(diagnostic) : _texts["data.import.failed"];
            return;
        }

        _snapshot = loaded.Value!;
        _previewed = (connection.Id, sql);
        _preview.Show(_snapshot);
        _outcome.Text = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _texts["data.import.rows"],
            _snapshot.Rows.Count,
            _snapshot.Columns.Count);
    }

    private void Confirm() => _ = ConfirmAsync();

    private async Task ConfirmAsync()
    {
        if (Chosen() is not { } connection) return;
        if (_sql.Text is not { Length: > 0 } sql) return;

        if (_previewed != (connection.Id, sql)) await GuardedPreviewAsync().ConfigureAwait(true);
        if (_previewed != (connection.Id, sql) || _sql.Text != sql || Chosen()?.Id != connection.Id) return;

        Completed?.Invoke(this, new DataImportChoice(
            _tables.SelectedItem as string ?? connection.DisplayName,
            new DataQuerySource { ConnectionId = connection.Id, Sql = sql },
            _snapshot,
            _headers.IsChecked ?? true));
    }
}
