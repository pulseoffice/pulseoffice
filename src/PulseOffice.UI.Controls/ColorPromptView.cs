using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls;

public sealed class ColorPromptView : UserControl, IDialogContent, IDialogFooterOwner
{
    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public ColorPromptView(string? initial, bool allowNone, ITextCatalog texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var picker = new ColorPickerView(initial, 1, texts, allowNone);
        Content = picker;

        Footer = new DialogFooter()
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"],
                () => Completed?.Invoke(this, new RibbonColor(picker.IsNone ? null : picker.Value)), primary: true);
    }
}

public interface IColorDialog
{
    Task<RibbonColor?> ChooseAsync(string? initial, bool allowNone, string titleKey, CancellationToken ct);
}

public sealed class ColorDialog(IDialogService dialogs, ITextCatalog texts) : IColorDialog
{
    public Task<RibbonColor?> ChooseAsync(string? initial, bool allowNone, string titleKey, CancellationToken ct) =>
        dialogs.ShowAsync<RibbonColor>(
            new ColorPromptView(initial, allowNone, texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 400 }, ct);
}
