using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Tabs;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Review;

public sealed class ReviewPanel : UserControl
{
    private readonly IReviewSource _source;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly PageTabStrip _tabs;
    private readonly PanelChoiceBox _authors;
    private readonly PanelSearchBox _filter;
    private readonly ListBox _list = new() { SelectionMode = SelectionMode.Multiple };
    private readonly TextBlock _empty = new() { TextWrapping = TextWrapping.Wrap };
    private readonly PanelActionBar _changeButtons;
    private readonly PanelActionBar _commentButtons;
    private readonly TextBox _reply = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 48, MaxHeight = 120 };
    private readonly Button _send = new();
    private readonly Grid _replyRow = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
    private readonly IReviewPanelRequests? _requests;
    private bool _comments;
    private string? _editing;
    private bool _filling;
    private string? _author;
    private bool _refillPending;

    public ReviewPanel(IReviewSource source, IIconCatalog icons, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact, IReviewPanelRequests? requests = null)
    {
        _requests = requests;
        ArgumentNullException.ThrowIfNull(icons);
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = DesignTokens.For(density);

        _tabs = new PageTabStrip(_metrics, [texts["review.panel.changes"], texts["review.panel.comments"]]);
        _tabs.SelectionChanged += (_, index) => Show(index == 1);

        _authors = new PanelChoiceBox(icons, _metrics) { Tip = texts["review.panel.author"] };
        _authors.Chosen += (_, key) =>
        {
            if (_filling) return;
            _author = key.Length == 0 ? null : key[1..];
            Refill();
        };
        _filter = new PanelSearchBox(icons, _metrics, texts["review.panel.filter"]);
        _filter.TextChanged += (_, _) => Refill();
        _empty.FontSize = _metrics.FontSizeSmall;
        _empty.Margin = new Thickness(_metrics.SpacingMedium);
        _empty.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _list.ItemTemplate = new FuncDataTemplate<ReviewItem>((entry, _) => entry is null ? null : Card(entry));
        _list.ContainerPrepared += (_, args) => args.Container.SetValue(PaddingProperty, new Thickness(0));
        _list.DoubleTapped += (_, _) => GoSelected();
        _list.SelectionChanged += (_, _) => Update();
        _list.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (args.Key == Key.Enter) GoSelected();
            else if (args.Key == Key.Delete && _comments && _source.Abilities.Comment) Run(_source.Delete, Selected());
            else return;
            args.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        _changeButtons = new PanelActionBar(icons, _metrics,
        [
            new PanelAction("accept", "panel.review.accept", texts["review.panel.accept.tip"]),
            new PanelAction("reject", "panel.review.reject", texts["review.panel.reject.tip"]),
            new PanelAction("acceptAll", "panel.review.acceptAll", texts["review.panel.acceptAll.tip"]) { Trailing = true },
            new PanelAction("rejectAll", "panel.review.rejectAll", texts["review.panel.rejectAll.tip"]) { Trailing = true },
        ]);
        _commentButtons = new PanelActionBar(icons, _metrics,
        [
            new PanelAction("resolve", "panel.review.resolve", texts["review.panel.resolve.tip"]),
            new PanelAction("edit", "panel.style.edit", texts["review.panel.edit.tip"]),
            new PanelAction("delete", "panel.delete", texts["review.panel.delete.tip"]),
            new PanelAction("deleteAuthor", "panel.review.deleteAuthor", texts["review.panel.deleteAuthor.tip"]) { Trailing = true },
            new PanelAction("deleteAll", "panel.review.deleteAll", texts["review.panel.deleteAll.tip"]) { Trailing = true },
        ]);
        _changeButtons.Invoked += (_, key) => Invoke(key);
        _commentButtons.Invoked += (_, key) => Invoke(key);

        _reply.PlaceholderText = texts["review.panel.reply.placeholder"];
        _send.Content = texts["review.panel.reply"];
        _send.Classes.Add("dialogPrimary");
        _send.VerticalAlignment = VerticalAlignment.Bottom;
        _send.Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0);
        _send.Click += (_, _) => SendReply();
        _reply.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter || !args.KeyModifiers.HasFlag(KeyModifiers.Control) && !args.KeyModifiers.HasFlag(KeyModifiers.Meta)) return;
            SendReply();
            args.Handled = true;
        };
        Grid.SetColumn(_send, 1);
        _replyRow.Children.Add(_reply);
        _replyRow.Children.Add(_send);

        var frame = new PanelListFrame(_metrics, _list, _empty);

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto,Auto"),
            RowSpacing = _metrics.SpacingSmall,
            Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0),
        };
        Control[] rows = [_tabs, _authors, _filter, frame, _changeButtons, _commentButtons, _replyRow];
        for (var i = 0; i < rows.Length; i++)
        {
            Grid.SetRow(rows[i], i);
            layout.Children.Add(rows[i]);
        }
        Content = layout;

        Show(false);
        AttachedToVisualTree += (_, _) =>
        {
            _source.Changed += OnChanged;
            Refill();
        };
        DetachedFromVisualTree += (_, _) => _source.Changed -= OnChanged;
        _reply.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape || _editing is null) return;
            StopEditing();
            args.Handled = true;
        };
        if (_requests is not null)
        {
            _requests.Requested += OnRequested;
            DetachedFromVisualTree += (_, _) => _requests.Requested -= OnRequested;
            AttachedToVisualTree += (_, _) =>
            {
                _requests.Requested -= OnRequested;
                _requests.Requested += OnRequested;
                Apply();
            };
            Apply();
        }
    }

    public bool ShowingComments => _comments;

    public IReadOnlyList<ReviewItem> Items => [.. _list.Items.OfType<ReviewItem>()];

    public void Show(bool comments)
    {
        _comments = comments;
        _tabs.Select(comments ? 1 : 0);
        _changeButtons.IsVisible = !comments;
        _commentButtons.IsVisible = comments;
        _replyRow.IsVisible = comments && (_source.Abilities.Replies || _editing is not null);
        Refill();
    }

    public void SelectAll() => _list.SelectAll();

    public void Select(string id)
    {
        foreach (var item in _list.Items.OfType<ReviewItem>())
            if (item.Id == id) _list.SelectedItem = item;
    }

    public void Press(string key)
    {
        if (!_changeButtons.Press(key)) _commentButtons.Press(key);
    }

    public PanelActionBar ChangeActions => _changeButtons;

    public PanelActionBar CommentActions => _commentButtons;

    private void Invoke(string key)
    {
        switch (key)
        {
            case "accept": Run(_source.Accept, Selected()); break;
            case "reject": Run(_source.Reject, Selected()); break;
            case "acceptAll": Run(_source.Accept, Visible()); break;
            case "rejectAll": Run(_source.Reject, Visible()); break;
            case "resolve":
                foreach (var id in Selected()) _source.ToggleResolved(id);
                break;
            case "edit": BeginEdit(); break;
            case "delete": Run(_source.Delete, Selected()); break;
            case "deleteAuthor": Run(_source.Delete, AuthorOfSelection()); break;
            case "deleteAll": Run(_source.Delete, Visible()); break;
        }
    }

    public bool IsEditing => _editing is not null;

    public TextBox ReplyBox => _reply;

    public void Apply()
    {
        if (_requests?.Take() is not { } request) return;
        Show(true);
        Select(request.Id);
        if (request.Kind == ReviewPanelRequestKind.Edit && _source.Abilities.Edit)
        {
            _editing = request.Id;
            _reply.Text = request.Text;
            _send.Content = _texts["review.panel.save"];
        }
        else StopEditing();
        _replyRow.IsVisible = true;
        Dispatcher.UIThread.Post(() => _reply.Focus(NavigationMethod.Tab), DispatcherPriority.Input);
    }

    private void OnRequested(object? sender, EventArgs args) => Apply();

    private void BeginEdit()
    {
        if (!_source.Abilities.Edit || Selected() is not [var id]) return;
        if (_source.Comments.FirstOrDefault(item => item.Id == id) is not { } comment) return;
        _editing = id;
        _reply.Text = comment.Text;
        _send.Content = _texts["review.panel.save"];
        Update();
        Dispatcher.UIThread.Post(() =>
        {
            _reply.Focus(NavigationMethod.Tab);
            _reply.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void StopEditing()
    {
        _editing = null;
        _reply.Text = string.Empty;
        _send.Content = _texts["review.panel.reply"];
        Update();
    }

    public void Reply(string text)
    {
        _reply.Text = text;
        SendReply();
    }

    private void Run(Action<IReadOnlyList<string>> action, IReadOnlyList<string> ids)
    {
        if (ids.Count > 0) action(ids);
    }

    private void SendReply()
    {
        var text = _reply.Text?.Trim();
        if (_editing is { } edited)
        {
            if (!string.IsNullOrEmpty(text) && _source.Abilities.Edit) _source.Edit(edited, text);
            StopEditing();
            return;
        }
        if (string.IsNullOrEmpty(text) || !_source.Abilities.Comment || Selected() is not [var id, ..]) return;
        _source.Reply(id, text);
        _reply.Text = string.Empty;
    }

    private void GoSelected()
    {
        if (Selected() is [var id, ..]) _source.Go(id);
    }

    private void OnChanged(object? sender, EventArgs args)
    {
        if (_refillPending) return;
        _refillPending = true;
        Dispatcher.UIThread.Post(Refill, DispatcherPriority.Background);
    }

    private IReadOnlyList<ReviewItem> Source() => _comments ? _source.Comments : _source.Changes;

    private void Refill()
    {
        _refillPending = false;
        var all = Source();
        _filling = true;
        var authors = all.Select(item => item.Author).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.CurrentCulture).ToList();
        if (_author is not null && !authors.Contains(_author)) _author = null;
        var choices = new List<PanelChoice> { new(string.Empty, _texts["review.panel.allAuthors"], "panel.author") };
        choices.AddRange(authors.Select(author => new PanelChoice("@" + author, author.Length == 0 ? _texts["review.panel.unknownAuthor"] : author, "panel.author")));
        _authors.Show(choices, _author is null ? string.Empty : "@" + _author);
        _filling = false;

        var keep = Selected().ToHashSet(StringComparer.Ordinal);
        if (keep.Count == 0 && _source.Current is { } current) keep.Add(current);
        var filter = _filter.Text.Trim();
        List<ReviewItem> items = [.. all.Where(entry => (_author is null || entry.Author == _author)
            && (filter.Length == 0 || entry.Text.Contains(filter, StringComparison.CurrentCultureIgnoreCase) || entry.Author.Contains(filter, StringComparison.CurrentCultureIgnoreCase)))];
        if (_list.ItemsSource is not IReadOnlyList<ReviewItem> shown || !shown.SequenceEqual(items))
        {
            _list.ItemsSource = items;
            foreach (var item in items.Where(item => keep.Contains(item.Id))) _list.SelectedItems?.Add(item);
        }
        _empty.Text = _texts[_comments ? "review.panel.noComments" : "review.panel.noChanges"];
        _empty.IsVisible = items.Count == 0;
        Update();
    }

    private Control Card(ReviewItem entry)
    {
        var color = Color.FromUInt32(entry.Color);
        var header = new TextBlock
        {
            Text = Header(entry),
            FontSize = _metrics.FontSizeSmall,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        var body = new TextBlock
        {
            Text = entry.Text.Length > 240 ? entry.Text[..240] + "…" : entry.Text,
            FontSize = _metrics.FontSize,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 4,
            TextDecorations = entry.Kind == ReviewItemKind.Deletion ? TextDecorations.Strikethrough : null,
            Opacity = entry.Resolved ? 0.55 : 1,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);
        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(header);
        content.Children.Add(body);
        if (entry.Note is { Length: > 0 } note)
        {
            content.Children.Add(new TextBlock
            {
                Text = note,
                FontSize = _metrics.FontSizeSmall,
                FontStyle = FontStyle.Italic,
                TextWrapping = TextWrapping.Wrap,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        }
        var row = new DockPanel { Margin = new Thickness(entry.Depth * 14, 0, 0, 0) };
        var stripe = new Border { Width = 4, Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 2, 8, 2) };
        DockPanel.SetDock(stripe, Dock.Left);
        row.Children.Add(stripe);
        row.Children.Add(content);
        var card = new Border { Padding = new Thickness(_metrics.SpacingSmall, 6), Child = row };
        ToolTip.SetTip(card, entry.Text);
        return card;
    }

    private string Header(ReviewItem entry)
    {
        var kind = _texts[$"review.kind.{entry.Kind.ToString().ToLowerInvariant()}"];
        var author = entry.Author.Length == 0 ? _texts["review.panel.unknownAuthor"] : entry.Author;
        var date = entry.Date == default ? string.Empty : " · " + entry.Date.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
        var place = entry.Place is { Length: > 0 } where ? " · " + where : string.Empty;
        var resolved = entry.Resolved ? " · " + _texts["review.panel.resolved"] : string.Empty;
        return (entry.Depth > 0 ? "↳ " : string.Empty) + kind + " · " + author + date + place + resolved;
    }

    private IReadOnlyList<string> Selected() =>
        [.. (_list.SelectedItems?.OfType<ReviewItem>() ?? []).Select(item => item.Id)];

    private IReadOnlyList<string> Visible() => [.. _list.Items.OfType<ReviewItem>().Select(item => item.Id)];

    private IReadOnlyList<string> AuthorOfSelection()
    {
        var selected = _list.SelectedItems?.OfType<ReviewItem>().FirstOrDefault();
        return selected is null ? [] : [.. Source().Where(item => item.Author == selected.Author && item.Depth == 0).Select(item => item.Id)];
    }

    private void Update()
    {
        var abilities = _source.Abilities;
        var any = Selected().Count > 0;
        var visible = _list.ItemCount > 0;
        _changeButtons.SetEnabled("accept", abilities.Resolve && any);
        _changeButtons.SetEnabled("reject", abilities.Resolve && any);
        _changeButtons.SetEnabled("acceptAll", abilities.Resolve && visible);
        _changeButtons.SetEnabled("rejectAll", abilities.Resolve && visible);
        _commentButtons.SetEnabled("resolve", abilities.ResolveComments && any);
        _commentButtons.SetVisible("resolve", abilities.ResolveComments);
        _commentButtons.SetVisible("edit", abilities.Edit);
        _commentButtons.SetEnabled("edit", abilities.Edit && Selected().Count == 1);
        _commentButtons.SetEnabled("delete", abilities.Comment && any);
        _commentButtons.SetEnabled("deleteAuthor", abilities.Comment && any);
        _commentButtons.SetEnabled("deleteAll", abilities.Comment && visible);
        _replyRow.IsVisible = _comments && (abilities.Replies || _editing is not null);
        _send.IsEnabled = _editing is not null || abilities.Comment && any;
    }
}
