using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Shell.Updates;

public enum UpdateChoice
{
    Later,
    Skip,
    Installed,
    OpenPage,
}

public sealed record UpdateOffer(string Version, string CurrentVersion, string Notes, bool Installable);

public delegate Task<string?> UpdateInstallation(IProgress<double> progress, CancellationToken ct);

public sealed class UpdateOfferView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly ITextCatalog _texts;
    private readonly UpdateInstallation _install;
    private readonly ProgressBar _progress;
    private readonly TextBlock _status;
    private readonly CancellationTokenSource _cancel = new();
    private Button? _primary;
    private bool _busy;

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public bool CanClose => !_busy;

    public UpdateOfferView(UpdateOffer offer, ITextCatalog texts, UpdateInstallation install, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(offer);
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _install = install ?? throw new ArgumentNullException(nameof(install));

        var metrics = DesignTokens.For(density);
        var layout = new StackPanel { Spacing = metrics.SpacingMedium };

        layout.Children.Add(new TextBlock
        {
            Text = texts.Format("updates.offer.headline", offer.Version),
            FontSize = metrics.FontSize * 1.5,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));

        layout.Children.Add(Line(texts.Format("updates.offer.current", offer.CurrentVersion), metrics, Palette.Keys.TextSecondary));

        if (offer.Notes.Trim() is { Length: > 0 } notes)
            layout.Children.Add(new Border
            {
                Child = new ScrollViewer
                {
                    MaxHeight = 260,
                    Content = new SelectableTextBlock { Text = notes, TextWrapping = TextWrapping.Wrap, FontSize = metrics.FontSize }
                        .Themed(SelectableTextBlock.ForegroundProperty, Palette.Keys.TextPrimary),
                },
                Padding = new Thickness(metrics.SpacingMedium),
                CornerRadius = new CornerRadius(metrics.RadiusTight),
            }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken));

        layout.Children.Add(Line(texts[offer.Installable ? "updates.offer.installNote" : "updates.offer.manualNote"], metrics, Palette.Keys.TextSecondary));

        _progress = new ProgressBar { Minimum = 0, Maximum = 1, Height = 6, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        _status = Line(string.Empty, metrics, Palette.Keys.TextSecondary);
        _status.IsVisible = false;
        layout.Children.Add(_progress);
        layout.Children.Add(_status);

        Content = layout;

        var footer = new DialogFooter(density)
            .WithCancel(texts["updates.offer.skip"], () => Finish(UpdateChoice.Skip))
            .WithAction(texts["updates.offer.later"], () => Finish(UpdateChoice.Later));
        Footer = offer.Installable
            ? footer.WithAction(texts["updates.offer.install"], () => _ = InstallAsync(), out _primary, primary: true)
            : footer.WithAction(texts["updates.offer.download"], () => Finish(UpdateChoice.OpenPage), primary: true);
    }

    private async Task InstallAsync()
    {
        if (_busy) return;
        _busy = true;
        if (_primary is not null) _primary.IsEnabled = false;
        _progress.IsVisible = true;
        _status.IsVisible = true;
        _status.Text = _texts["updates.offer.downloading"];

        var progress = new Progress<double>(value => Dispatcher.UIThread.Post(() => _progress.Value = Math.Clamp(value, 0, 1)));
        string? problem;
        try
        {
            problem = await _install(progress, _cancel.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            problem = _texts["updates.offer.cancelled"];
        }

        _busy = false;
        if (problem is null)
        {
            Finish(UpdateChoice.Installed);
            return;
        }

        _progress.IsVisible = false;
        _status.Text = _texts.Format("updates.offer.failed", problem);
        if (_primary is not null) _primary.IsEnabled = true;
    }

    private void Finish(UpdateChoice choice)
    {
        if (_busy) return;
        Completed?.Invoke(this, choice);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _cancel.Cancel();
        base.OnDetachedFromVisualTree(e);
    }

    private static TextBlock Line(string text, DesignTokens.Metrics metrics, string brush) =>
        new TextBlock { Text = text, FontSize = metrics.FontSize, TextWrapping = TextWrapping.Wrap }.Themed(TextBlock.ForegroundProperty, brush);
}
