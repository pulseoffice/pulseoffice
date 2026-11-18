using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Document.Tables;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Tables;

public sealed record TableLookOptionsChoice(bool HeaderRow, TableBanding Banding);

public interface ITableLookOptionsDialog
{
    Task<TableLookOptionsChoice?> AskAsync(string titleKey, bool headerRow, TableBanding banding, CancellationToken ct);
}

public sealed class TableLookOptionsDialog(IDialogService dialogs, ITextCatalog texts) : ITableLookOptionsDialog
{
    public async Task<TableLookOptionsChoice?> AskAsync(string titleKey, bool headerRow, TableBanding banding, CancellationToken ct) =>
        await dialogs.ShowAsync<TableLookOptionsChoice>(
            new TableLookOptionsView(texts, headerRow, banding),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 420 },
            ct).ConfigureAwait(true);
}

public sealed class TableLookOptionsView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly TableLookOptions _options;
    private readonly DialogFooter _footer;

    public event EventHandler<object?>? Completed;

    public TableLookOptionsView(ITextCatalog texts, bool headerRow, TableBanding banding, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var metrics = DesignTokens.For(density);
        _options = new TableLookOptions(texts, headerRow, banding, metrics.FontSize, new Thickness(0, 0, metrics.SpacingLarge, metrics.SpacingSmall));
        _footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], Confirm, primary: true);
        Content = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                new TextBlock
                {
                    Text = texts["table.style.optionsExplanation"],
                    FontSize = metrics.FontSize,
                    TextWrapping = TextWrapping.Wrap,
                }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                _options,
            },
        };
    }

    public Control? Footer => _footer;

    public TableLookOptions Options => _options;

    public void Confirm() => Completed?.Invoke(this, new TableLookOptionsChoice(_options.HeaderRow, _options.Banding));
}
