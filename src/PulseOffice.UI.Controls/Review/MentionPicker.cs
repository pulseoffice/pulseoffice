using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PulseOffice.UI.Controls.Lists;

namespace PulseOffice.UI.Controls.Review;

public sealed class MentionPicker(IMentionQuery query, IChoiceListPopup popup)
{
    private bool _inserting;

    public void Attach(TextBox box, Func<IReadOnlyList<string>> people)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(people);
        box.TextChanged += (_, _) => Offer(box, people());
        box.AddHandler(InputElement.KeyDownEvent, (_, args) =>
        {
            if (popup.IsOpen && popup.HandleKey(args.Key)) args.Handled = true;
        }, RoutingStrategies.Tunnel);
        box.LostFocus += (_, _) => popup.Close();
    }

    private void Offer(TextBox box, IReadOnlyList<string> people)
    {
        if (_inserting) return;
        var text = box.Text ?? string.Empty;
        if (query.TokenAt(text, box.CaretIndex) is not { } token || query.Candidates(token, people) is not { Count: > 0 } candidates)
        {
            popup.Close();
            return;
        }

        popup.Show(box, new Rect(0, 0, box.Bounds.Width, box.Bounds.Height), candidates, null, person =>
        {
            var current = box.Text ?? string.Empty;
            if (query.TokenAt(current, box.CaretIndex) is not { } live) return;
            var inserted = query.Insert(current, live, box.CaretIndex, person);
            _inserting = true;
            box.Text = inserted.Text;
            box.CaretIndex = inserted.Caret;
            _inserting = false;
            box.Focus();
        });
    }
}
