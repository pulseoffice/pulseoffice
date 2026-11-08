using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Data.UI;

public sealed class ExternalQueryConsent(
    IExternalQueryConsentLedger ledger,
    IConnectionDirectory connections,
    IDialogService dialogs,
    ITextCatalog texts) : IExternalQueryGate
{
    public async Task<bool> AllowAsync(object document, IReadOnlyList<ExternalQueryRequest> requests, CancellationToken ct = default)
    {
        var pending = ledger.Pending(document, requests);
        if (pending.Count == 0) return true;

        var described = pending
            .Select(request => new ExternalQueryConsentView.Entry(connections.Find(request.ConnectionId)?.DisplayName ?? request.ConnectionId, request.Sql))
            .ToList();
        var allowed = await dialogs.ShowAsync<bool?>(
            new ExternalQueryConsentView(described, texts),
            new DialogOptions { TitleKey = "data.consent.title", PreferredWidth = 640 },
            ct).ConfigureAwait(true) == true;

        if (allowed) ledger.Grant(document, pending);
        return allowed;
    }

    public void Grant(object document, ExternalQueryRequest request) => ledger.Grant(document, [request]);
}

public sealed class ExternalQueryConsentView : UserControl, IDialogContent, IDialogFooterOwner
{
    public sealed record Entry(string ConnectionName, string Sql);

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public ExternalQueryConsentView(IReadOnlyList<Entry> entries, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(texts);

        var metrics = DesignTokens.For(density);
        var layout = new StackPanel { Spacing = metrics.SpacingMedium };

        layout.Children.Add(new TextBlock
        {
            Text = texts["data.consent.explanation"],
            FontSize = metrics.FontSize,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        foreach (var entry in entries)
            layout.Children.Add(Card(entry, texts, metrics));

        Content = new ScrollViewer { Content = layout, MaxHeight = 460 };

        Footer = new DialogFooter(density)
            .WithCancel(texts["data.consent.cancel"], () => Completed?.Invoke(this, false))
            .WithAction(texts["data.consent.run"], () => Completed?.Invoke(this, true), primary: true);
    }

    private static Border Card(Entry entry, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var card = new StackPanel { Spacing = metrics.SpacingSmall };

        card.Children.Add(new TextBlock
        {
            Text = texts.Format("data.consent.connection", entry.ConnectionName),
            FontSize = metrics.FontSize,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));

        card.Children.Add(new SelectableTextBlock
        {
            Text = entry.Sql,
            FontFamily = new FontFamily("Menlo, Consolas, DejaVu Sans Mono, monospace"),
            FontSize = metrics.FontSizeSmall,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 160,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        return new Border
        {
            Child = card,
            Padding = new Thickness(metrics.SpacingMedium),
            CornerRadius = new CornerRadius(metrics.RadiusTight),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
    }
}
