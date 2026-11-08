using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI.Motion;

public sealed class AnimationDialogView : UserControl, IDialogContent, IDialogFooterOwner
{
    public AnimationDialogView(AnimationPane pane, ITextCatalog texts)
    {
        Pane = pane ?? throw new ArgumentNullException(nameof(pane));
        ArgumentNullException.ThrowIfNull(texts);
        Content = pane;
        Footer = new DialogFooter()
            .WithNote(texts["deck.motion.paneNote"])
            .WithAction(texts["prompt.close"], () => Completed?.Invoke(this, null), primary: true);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public AnimationPane Pane { get; }
}
