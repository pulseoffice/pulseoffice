using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Macros;

public sealed class MacroEventsView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly MacroEventsPanel _panel;
    private readonly Button _apply;

    public MacroEventsView(MacroEventsPanel panel, ITextCatalog texts)
    {
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
        ArgumentNullException.ThrowIfNull(texts);

        Footer = new DialogFooter()
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["macroEvents.apply"], Apply, out _apply, primary: true);

        _panel.AssignmentsChanged += (_, _) => Update();
        Update();

        Content = _panel;
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    private void Update() => _apply.IsEnabled = !_panel.ReadOnly && _panel.Assignments.IsChanged;

    private void Apply()
    {
        if (!_apply.IsEnabled) return;

        Completed?.Invoke(this, _panel.Assignments.Current);
    }
}
