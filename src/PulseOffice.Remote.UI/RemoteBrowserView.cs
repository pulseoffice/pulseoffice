using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Remote.UI;

public sealed record RemoteChoice(RemoteConnection Connection, string Path);

public sealed class RemoteBrowserView : UserControl, IDialogContent, IDialogFooterOwner
{
    private static readonly RemoteProtocol[] Protocols =
    [
        RemoteProtocol.WebDav, RemoteProtocol.OneDrive, RemoteProtocol.GoogleDrive,
        RemoteProtocol.Dropbox, RemoteProtocol.ICloudDrive,
    ];

    private readonly IRemoteConnectionDirectory _connections;
    private readonly IRemoteStorageProvider _storages;
    private readonly IRemoteSignIn? _signIn;
    private readonly IRemoteCredentials _credentials;
    private readonly IRemoteFileNames _fileNames;
    private readonly IIconCatalog? _icons;
    private readonly ITextCatalog _texts;
    private readonly IDiagnosticTexts _diagnostics;
    private readonly DesignTokens.Metrics _metrics;
    private readonly IDialogParts _parts;
    private readonly bool _saving;

    private readonly ListBox _archives = new();
    private readonly ListBox _entries = new();
    private readonly TextBox _name = new();
    private readonly TextBlock _place = new();
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _outcome = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Panel _right = new();

    private readonly ComboBox _protocol = new();
    private readonly TextBox _url = new();
    private readonly TextBox _user = new();
    private readonly TextBox _password = new() { PasswordChar = '•' };
    private readonly TextBox _title = new();

    private readonly List<RemoteEntry> _listed = [];

    private Control? _browser;
    private Control? _editor;
    private Button? _signInButton;
    private Button? _forgetButton;
    private Border? _hintStrip;

    private string _path = "/";
    private string? _editing;

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; private set; }

    public RemoteBrowserView(
        IRemoteConnectionDirectory connections,
        IRemoteStorageProvider storages,
        IRemoteCredentials credentials,
        IRemoteFileNames fileNames,
        ITextCatalog texts,
        IDiagnosticTexts diagnostics,
        bool saving,
        string? suggestedName = null,
        IRemoteSignIn? signIn = null,
        IIconCatalog? icons = null,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _storages = storages ?? throw new ArgumentNullException(nameof(storages));
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _fileNames = fileNames ?? throw new ArgumentNullException(nameof(fileNames));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _signIn = signIn;
        _icons = icons;
        _metrics = DesignTokens.For(density);
        _parts = new DialogParts(_metrics, icons);
        _saving = saving;
        _name.Text = suggestedName ?? string.Empty;

        Content = Build();

        Reload();
        ShowBrowser();
    }

    private Control Build()
    {
        Footer = new DialogFooter()
            .WithCancel(_texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(_texts["prompt.confirm"], Confirm, primary: true);

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("200,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            ColumnSpacing = _metrics.SpacingMedium,
            Height = 380,
        };

        var sidebar = Sidebar();
        Grid.SetColumn(sidebar, 0);
        Grid.SetRow(sidebar, 0);
        Grid.SetRowSpan(sidebar, 2);

        Grid.SetColumn(_right, 1);
        Grid.SetRow(_right, 0);

        _outcome.FontSize = _metrics.FontSizeSmall;
        _outcome.Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0);
        _outcome.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        Grid.SetColumn(_outcome, 1);
        Grid.SetRow(_outcome, 1);

        layout.Children.Add(sidebar);
        layout.Children.Add(_right);
        layout.Children.Add(_outcome);

        _browser = Browser();
        _editor = Editor();

        return layout;
    }

    private Control Sidebar()
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };

        var caption = new TextBlock
        {
            Text = _texts["remote.archives"].ToUpperInvariant(),
            FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
            LetterSpacing = 0.6,
            Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        _archives.SelectionChanged += (_, _) => Chosen();
        _archives.DoubleTapped += (_, _) => EditCurrent();
        _archives.Classes.Add("compact");

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
            Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0),
        };

        actions.Children.Add(_parts.Quiet(_texts["remote.newArchive"], "ui.add", StartNew));
        actions.Children.Add(_parts.Quiet(_texts["remote.edit"], null, EditCurrent));

        Grid.SetRow(caption, 0);
        Grid.SetRow(_archives, 1);
        Grid.SetRow(actions, 2);

        layout.Children.Add(caption);
        layout.Children.Add(_archives);
        layout.Children.Add(actions);

        return layout;
    }

    private Control Browser()
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto") };

        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        var up = _parts.Quiet(string.Empty, "ui.back", Up);
        ToolTip.SetTip(up, _texts["remote.up"]);

        _place.VerticalAlignment = VerticalAlignment.Center;
        _place.Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0);
        _place.FontSize = _metrics.FontSize;

        _signInButton = _parts.Quiet(_texts["remote.signIn"], "ui.cloud", SignIn);

        Grid.SetColumn(up, 0);
        Grid.SetColumn(_place, 1);
        Grid.SetColumn(_signInButton, 2);

        bar.Children.Add(up);
        bar.Children.Add(_place);
        bar.Children.Add(_signInButton);

        var list = new Border
        {
            Child = _entries,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0),
        };

        list.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        list.Themed(Border.BackgroundProperty, Palette.Keys.Surface);

        _entries.Classes.Add("compact");
        _entries.DoubleTapped += (_, _) => Activate();
        _entries.BorderThickness = default;
        _entries.Background = Brushes.Transparent;

        _hint.FontSize = _metrics.FontSizeSmall;
        _hint.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        _hintStrip = new Border
        {
            Child = _hint,
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0),
            IsVisible = false,
        };

        _hintStrip.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        var naming = _parts.Field(_texts["remote.name"], _name);
        naming.IsVisible = _saving;
        naming.Margin = new Thickness(0, _metrics.SpacingMedium, 0, 0);

        Grid.SetRow(bar, 0);
        Grid.SetRow(list, 1);
        Grid.SetRow(_hintStrip, 2);
        Grid.SetRow(naming, 3);

        layout.Children.Add(bar);
        layout.Children.Add(list);
        layout.Children.Add(_hintStrip);
        layout.Children.Add(naming);

        return layout;
    }

    private Control Editor()
    {
        var layout = new StackPanel { Spacing = _metrics.SpacingMedium };

        var caption = new TextBlock
        {
            Text = _texts["remote.newArchive.title"],
            FontWeight = FontWeight.SemiBold,
            FontSize = _metrics.FontSize,
        };

        _protocol.ItemsSource = Protocols.Select(protocol => _texts[$"remote.protocol.{protocol}"]).ToList();
        _protocol.SelectedIndex = 0;
        _protocol.SelectionChanged += (_, _) => ShowFieldsForProtocol();
        _protocol.HorizontalAlignment = HorizontalAlignment.Stretch;

        var fields = new StackPanel { Spacing = _metrics.SpacingSmall };

        fields.Children.Add(_parts.Field(_texts["remote.archiveName"], _title));
        fields.Children.Add(_parts.Field(_texts["remote.protocol"], _protocol));
        fields.Children.Add(_parts.Field(_texts["remote.url"], _url));
        fields.Children.Add(_parts.Field(_texts["remote.user"], _user));
        fields.Children.Add(_parts.Field(_texts["remote.password"], _password));

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
        };

        _forgetButton = _parts.Quiet(_texts["remote.forget"], "ui.close", ForgetConnection);

        buttons.Children.Add(_parts.Accented(_texts["remote.save"], SaveConnection));
        buttons.Children.Add(_forgetButton);
        buttons.Children.Add(_parts.Quiet(_texts["prompt.cancel"], null, ShowBrowser));

        layout.Children.Add(caption);
        layout.Children.Add(fields);
        layout.Children.Add(buttons);

        return layout;
    }

    private void ShowBrowser()
    {
        _editing = null;
        _right.Children.Clear();
        if (_browser is not null) _right.Children.Add(_browser);

        UpdateHint();
    }

    private void StartNew()
    {
        _editing = string.Empty;
        _archives.SelectedIndex = -1;

        _title.Text = string.Empty;
        _url.Text = string.Empty;
        _user.Text = string.Empty;
        _password.Text = string.Empty;
        _protocol.SelectedIndex = 0;

        _outcome.Text = string.Empty;

        _right.Children.Clear();
        if (_editor is not null) _right.Children.Add(_editor);

        if (_forgetButton is not null) _forgetButton.IsVisible = false;

        ShowFieldsForProtocol();
    }

    private void EditCurrent()
    {
        if (Current() is not { } connection) return;

        _editing = connection.Name;
        _title.Text = connection.Name;
        _url.Text = connection.Url;
        _user.Text = connection.User;
        _password.Text = string.Empty;
        _protocol.SelectedIndex = Math.Max(0, Array.IndexOf(Protocols, connection.Protocol));

        _outcome.Text = string.Empty;

        _right.Children.Clear();
        if (_editor is not null) _right.Children.Add(_editor);

        if (_forgetButton is not null) _forgetButton.IsVisible = true;

        ShowFieldsForProtocol();
    }

    private RemoteProtocol ChosenProtocol =>
        Protocols[Math.Clamp(_protocol.SelectedIndex, 0, Protocols.Length - 1)];

    private void ShowFieldsForProtocol()
    {
        if (_editor is not StackPanel layout) return;
        if (layout.Children[1] is not StackPanel fields) return;

        var addressed = ChosenProtocol.NeedsAddress();

        for (var index = 2; index < fields.Children.Count; index++)
            fields.Children[index].IsVisible = addressed;
    }

    private void UpdateHint()
    {
        var connection = Current();

        var message = connection?.Protocol switch
        {
            { } protocol when protocol.NeedsSignIn() && _signIn?.IsSignedIn(connection!) == false
                => _texts["remote.signInNeeded"],
            _ => null,
        };

        _hint.Text = message ?? string.Empty;
        if (_hintStrip is not null) _hintStrip.IsVisible = message is not null;

        if (_signInButton is not null)
            _signInButton.IsVisible = connection is not null
                && connection.Protocol.NeedsSignIn()
                && _signIn is not null;
    }

    private void Reload()
    {
        var selected = Current()?.Name;

        _archives.ItemsSource = _connections.All.Select(connection => _parts.ListRow(connection.Protocol == RemoteProtocol.WebDav ? "ui.server" : "ui.cloud", connection.Name, _texts[$"remote.protocol.{connection.Protocol}"])).ToList();

        if (_connections.All.Count == 0)
        {
            _outcome.Text = _texts["remote.noArchives"];
            return;
        }

        var index = selected is null
            ? 0
            : Math.Max(0, _connections.All.ToList().FindIndex(known => known.Name == selected));

        _archives.SelectedIndex = index;
    }

    private RemoteConnection? Current() =>
        _archives.SelectedIndex >= 0 && _archives.SelectedIndex < _connections.All.Count
            ? _connections.All[_archives.SelectedIndex]
            : null;

    private void Chosen()
    {
        if (_editing is not null) return;

        UpdateHint();

        if (Current() is null)
        {
            _outcome.Text = _texts["remote.chooseArchive"];
            return;
        }

        Browse("/");
    }

    private void SaveConnection() => _ = GuardedAsync(SaveConnectionAsync);

    private async Task SaveConnectionAsync()
    {
        var protocol = ChosenProtocol;
        var url = (_url.Text ?? string.Empty).Trim();

        if (protocol.NeedsAddress() && url.Length == 0)
        {
            _outcome.Text = _texts["remote.urlMissing"];
            return;
        }

        var name = _title.Text is { Length: > 0 } declared
            ? declared
            : protocol.NeedsAddress()
                ? Uri.TryCreate(url, UriKind.Absolute, out var address) && address.Host.Length > 0 ? address.Host : url
                : _texts[$"remote.protocol.{protocol}"];

        var connection = new RemoteConnection
        {
            Name = name,
            Protocol = protocol,
            Url = url,
            User = protocol.NeedsAddress() ? _user.Text ?? string.Empty : string.Empty,
        };

        var renamed = _editing is { Length: > 0 } previous && previous != name ? _connections.Find(previous) : null;
        var secret = _password.Text is { Length: > 0 } typed
            ? typed
            : renamed is not null
                ? await _credentials.PasswordAsync(renamed).ConfigureAwait(true)
                : null;

        if (renamed is not null) _connections.Remove(renamed.Name);
        _connections.Save(connection);

        if (renamed is not null) await _credentials.ForgetAsync(renamed).ConfigureAwait(true);
        if (secret is { Length: > 0 }) await _credentials.KeepPasswordAsync(connection, secret).ConfigureAwait(true);

        _editing = null;
        Reload();

        _archives.SelectedIndex = _connections.All.ToList().FindIndex(known => known.Name == name);

        _outcome.Text = _texts.Format("remote.saved", name);

        ShowBrowser();
        await BrowseAsync("/").ConfigureAwait(true);
    }

    private void ForgetConnection()
    {
        if (_editing is not { Length: > 0 } name)
        {
            ShowBrowser();
            return;
        }

        if (_connections.Find(name) is { } connection)
        {
            _connections.Remove(connection.Name);
            _ = GuardedAsync(() => _credentials.ForgetAsync(connection));
            _outcome.Text = _texts.Format("remote.forgotten", connection.Name);
        }

        _editing = null;
        Reload();
        ShowBrowser();
    }

    private void SignIn() => _ = GuardedAsync(SignInAsync);

    private void Browse(string path) => _ = GuardedAsync(() => BrowseAsync(path));

    private async Task GuardedAsync(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _outcome.Text = _texts.Format("remote.unexpected", ex.Message);
        }
    }

    private async Task SignInAsync()
    {
        if (_signIn is null || Current() is not { } connection) return;

        _outcome.Text = _texts["remote.signingIn"];

        var outcome = await _signIn.SignInAsync(connection).ConfigureAwait(true);

        if (outcome.Succeeded)
        {
            _outcome.Text = _texts.Format("remote.signedIn", connection.Name);
            UpdateHint();
            await BrowseAsync("/").ConfigureAwait(true);
            return;
        }

        _outcome.Text = _diagnostics.Describe(outcome.Diagnostics[0]);
    }

    private async Task BrowseAsync(string path)
    {
        if (Current() is not { } connection)
        {
            _outcome.Text = _texts["remote.chooseArchive"];
            return;
        }

        _path = path;
        _place.Text = Breadcrumb(connection, path);

        var listing = await _storages.For(connection.Protocol)
            .ListAsync(connection, path).ConfigureAwait(true);

        _listed.Clear();

        if (!listing.Succeeded)
        {
            _entries.ItemsSource = Array.Empty<Control>();
            _outcome.Text = _diagnostics.Describe(listing.Diagnostics[0]);
            return;
        }

        _listed.AddRange(listing.Value!.OrderByDescending(entry => entry.IsFolder)
            .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase));

        _entries.ItemsSource = _listed.Select(EntryRow).ToList();

        _outcome.Text = _listed.Count == 0
            ? _texts["remote.empty"]
            : _texts.Format("remote.listed", _listed.Count);
    }

    private static string Breadcrumb(RemoteConnection connection, string path) =>
        string.Join(" › ",
            [connection.Name, .. path.Split('/', StringSplitOptions.RemoveEmptyEntries)]);

    private Control EntryRow(RemoteEntry entry)
    {
        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            ColumnSpacing = _metrics.SpacingMedium,
        };

        if (_icons?.Find(entry.IsFolder ? "ui.folder" : "ui.file") is { } geometry)
        {
            var icon = new IconView(geometry, _metrics.IconSize)
            {
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetColumn(icon, 0);
            layout.Children.Add(icon);
        }

        var name = new TextBlock
        {
            Text = entry.Name,
            FontSize = _metrics.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var size = new TextBlock
        {
            Text = entry.IsFolder ? _texts["remote.folder"] : Size(entry.Size),
            FontSize = _metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var moment = new TextBlock
        {
            Text = entry.Modified is { } when
                ? when.LocalDateTime.ToString("d MMM yyyy", System.Globalization.CultureInfo.CurrentCulture)
                : string.Empty,
            FontSize = _metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 96,
            TextAlignment = TextAlignment.Right,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        Grid.SetColumn(name, 1);
        Grid.SetColumn(size, 2);
        Grid.SetColumn(moment, 3);

        layout.Children.Add(name);
        layout.Children.Add(size);
        layout.Children.Add(moment);

        return layout;
    }

    private string Size(long bytes) => bytes switch
    {
        < 1024 => _texts.Format("remote.size.bytes", bytes),
        < 1024 * 1024 => _texts.Format("remote.size.kilobytes", Math.Round(bytes / 1024.0)),
        _ => _texts.Format("remote.size.megabytes", Math.Round(bytes / (1024.0 * 1024), 1)),
    };

    private void Up()
    {
        var trimmed = _path.TrimEnd('/');
        var separator = trimmed.LastIndexOf('/');

        Browse(separator <= 0 ? "/" : trimmed[..separator]);
    }

    private void Activate()
    {
        if (Selected() is not { } entry) return;

        if (entry.IsFolder) Browse(entry.Path);
        else Confirm();
    }

    private RemoteEntry? Selected() =>
        _entries.SelectedIndex >= 0 && _entries.SelectedIndex < _listed.Count
            ? _listed[_entries.SelectedIndex]
            : null;

    private void Confirm()
    {
        if (_editing is not null)
        {
            SaveConnection();
            return;
        }

        if (Current() is not { } connection)
        {
            _outcome.Text = _texts["remote.chooseArchive"];
            return;
        }

        if (_saving)
        {
            if (_name.Text is not { Length: > 0 } name)
            {
                _outcome.Text = _texts["remote.nameMissing"];
                return;
            }

            if (!_fileNames.IsValid(name))
            {
                _outcome.Text = _texts["remote.nameInvalid"];
                return;
            }

            Completed?.Invoke(this, new RemoteChoice(connection, $"{_path.TrimEnd('/')}/{name}"));
            return;
        }

        if (Selected() is not { IsFolder: false } file)
        {
            _outcome.Text = _texts["remote.chooseFile"];
            return;
        }

        Completed?.Invoke(this, new RemoteChoice(connection, file.Path));
    }
}
