using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Shell;

public sealed class DocumentTabsView : ContentControl
{
    private readonly IDocumentWorkspace _workspace;
    private readonly DesignTokens.Metrics _metrics;
    private readonly StackPanel _tabs;

    private readonly ITextCatalog _texts;

    public DocumentTabsView(
        IDocumentWorkspace workspace, ITextCatalog texts,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = DesignTokens.For(density);

        _tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1 };

        this.Themed(BackgroundProperty, Palette.Keys.SurfaceRaised);

        Content = new ScrollViewer
        {
            Content = _tabs,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        Height = _metrics.ControlHeight + 4;
        _workspace.ActiveChanged += (_, _) => Refresh();
        _workspace.ActiveUpdated += (_, _) => Refresh();
        _workspace.DocumentClosed += (_, _) => Refresh();
        _workspace.DocumentOpened += (_, _) => Refresh();

        Refresh();
    }

    public event EventHandler<OpenDocument>? CloseRequested;

    public void Refresh()
    {
        _tabs.Children.Clear();

        foreach (var document in _workspace.Documents)
            _tabs.Children.Add(BuildTab(document));
    }

    private string LabelOf(OpenDocument document)
    {
        var shared = _workspace.Documents.Any(other => !ReferenceEquals(other, document)
            && string.Equals(other.Title, document.Title, StringComparison.CurrentCultureIgnoreCase));
        if (!shared || string.IsNullOrEmpty(document.Path)) return document.DisplayName;

        var named = document.Title + System.IO.Path.GetExtension(document.Path);
        return document.IsModified ? named + " •" : named;
    }

    private Control BuildTab(OpenDocument document)
    {
        var isActive = ReferenceEquals(_workspace.Active, document);
        var accent = Color.Parse(document.Module == ModuleKind.Shell
            ? DesignTokens.ShellAccent
            : ModuleIdentity.For(document.Module).AccentHex);

        var label = new TextBlock
        {
            Text = LabelOf(document),
            FontSize = _metrics.FontSize,
            FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Normal,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(
            TextBlock.ForegroundProperty,
            isActive ? Palette.Keys.TextPrimary : Palette.Keys.TextSecondary);

        var close = new Button
        {
            Content = "×",
            FontSize = _metrics.FontSize,
            Padding = new Thickness(4, 0),
            Background = Brushes.Transparent,
            BorderThickness = default,
            VerticalAlignment = VerticalAlignment.Center,
        };

        close.Click += (_, _) => CloseRequested?.Invoke(this, document);
        ToolTip.SetTip(close, _texts["document.close"]);
        AutomationProperties.SetName(close, _texts.Format("document.closeNamed", document.Title));

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
        };

        if (!string.IsNullOrEmpty(document.Path)) ToolTip.SetTip(label, document.Path);
        content.Children.Add(label);
        content.Children.Add(close);

        var tab = new Border
        {
            Child = content,
            Padding = new Thickness(_metrics.SpacingMedium, 2),

            BorderThickness = new Thickness(0, 2, 0, 0),
            BorderBrush = isActive ? new SolidColorBrush(accent) : Brushes.Transparent,
            Background = isActive
                ? new SolidColorBrush(accent, DesignTokens.SubtleAccentOpacity)
                : Brushes.Transparent,
        };

        tab.PointerPressed += (_, e) =>
        {
            if (e.Source is Button) return;
            _workspace.Activate(document);
        };

        return tab;
    }
}
